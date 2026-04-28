using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameManager : NetworkBehaviour
{
    public static InGameManager Instance;

    List<NetworkClient> netPlayers;
    public NetworkVariable<int> playersAlive = new NetworkVariable<int>();
    private HashSet<ulong> readyClients = new HashSet<ulong>();
    public NetworkList<PlayerScore> scores = new NetworkList<PlayerScore>();

    public bool roundEnding, clientsReady;

    [SerializeField] TextMeshProUGUI roomNameText;
    [SerializeField] TextMeshProUGUI roomCodeText;
    [SerializeField] TextMeshProUGUI winScreenText;

    [SerializeField] Button startGameButton;

    bool canStartGame;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public override void OnNetworkSpawn()
    {
        scores.OnListChanged += OnScoresChanged;

        LogRankings();

        if (IsHost)
        {
            playersAlive.Value = NetworkManager.Singleton.ConnectedClients.Count;

            // Add all already-connected clients (including host) that joined before this object spawned
            foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
            {
                scores.Add(new PlayerScore { clientId = client.ClientId, points = 0 });
            }
        }

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    void Start()
    {
        netPlayers = new List<NetworkClient>();

        netPlayers = new List<NetworkClient>(NetworkManager.Singleton.ConnectedClientsList);

        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            // Set up lobby visuals
            roomNameText.text = LobbyManager.Instance.currentLobby.Name;
            roomCodeText.text = LobbyManager.Instance.currentLobby.LobbyCode;

            startGameButton.onClick.AddListener(StartGame);

            if (IsHost)
            {
                SwitchAllPlayerMovementRPC(true, 0.1f);
                clientsReady = true;
            }
        }
        else if (SceneManager.GetActiveScene().name == "RanGen")
        {
            SwitchAllPlayerMovementRPC(false, 0.1f);
        }
        else // win screen
        {
            if (IsHost)
            {
                // turn movement back on after a short time
                SwitchAllPlayerMovementRPC(false, 0.1f);
                SwitchAllPlayerMovementRPC(true, 1f);

                StartCoroutine(ReturnToLobby());
            }

            //winScreenText = GameObject.Find("USER WINS").GetComponent<TextMeshProUGUI>();

            bool winnerFound = false;

            for (int i = 0; i < netPlayers.Count; i++)
            {
                PlayerMovement playerScript = netPlayers[i].PlayerObject.GetComponent<PlayerMovement>();

                if (playerScript.isWinner.Value)
                {
                    winScreenText.text = playerScript.GetComponent<NameTagDisplay>().usernameText.text + " WINS!";
                    winnerFound = true;
                    break;
                }
            }

            if (!winnerFound)
            {
                winScreenText.text = "NULL WINS!";
            }
        }

        TransitionManager.Instance.EndTransition();

        UpdateLayerOrder();
    }

    public override void OnDestroy()
    {
        scores.OnListChanged -= OnScoresChanged;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    void HandleClientConnected(ulong clientId)
    {
        netPlayers = new List<NetworkClient>(NetworkManager.Singleton.ConnectedClientsList);

        if (IsHost) 
            scores.Add(new PlayerScore { clientId = clientId, points = 0 });

        // if in the lobby, set the players movement to true, unlocking control
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            SwitchAllPlayerMovementRPC(true, 0.1f);
        }

        UpdateLayerOrder();
    }

    void HandleClientDisconnected(ulong clientId)
    {
        if (IsHost)
        {
            foreach (PlayerScore score in scores)
            {
                if (score.clientId == clientId)
                {
                    scores.Remove(score);
                }
            }
        }
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            if (IsHost && !canStartGame && netPlayers.Count > 1)
            {
                canStartGame = true;

                startGameButton.interactable = true;
            }
        }
        else
        {
            // Win condition for rounds
            if (playersAlive.Value <= 1 && IsHost && !roundEnding)
            {
                roundEnding = true;
                StartCoroutine(RoundEndDelay());
            }
        }

        #region testing

        if (Keyboard.current.gKey.wasPressedThisFrame && IsHost)
        {
            StartNewRound();
        }

        #endregion
    }

    #region Round System

    // Called by each client once their tilemap RPC has finished rendering
    [Rpc(SendTo.Server)]
    public void ClientReadyRPC(ulong clientId)
    {
        readyClients.Add(clientId);

        if (readyClients.Count >= NetworkManager.Singleton.ConnectedClients.Count)
        {
            readyClients.Clear();
            Debug.Log("All clients ready, starting round...");
            clientsReady = true;
            StartCoroutine(RoundStartCountdown());
        }
    }

    void StartGame()
    {
        for (int i = 0; i < netPlayers.Count; i++)
        {
            PlayerHealth healthScript = netPlayers[i].PlayerObject.GetComponent<PlayerHealth>();

            healthScript.ModifyAliveStateRPC(false);

            if (!healthScript.IsHost)
            {
                TransitionManager.Instance.StartTransitionManually();
            }
        }

        TransitionManager.Instance.LoadScene("RanGen");

        //NetworkManager.Singleton.SceneManager.LoadScene("RanGen", LoadSceneMode.Single);
    }

    IEnumerator RoundEndDelay()
    {
        // One second check for if the last player alive also dies
        // If no one is alive after the one second delay then it counts as a draw
        yield return new WaitForSeconds(1);

        bool isDraw = playersAlive.Value <= 0;

        if (!isDraw)
        {
            for (int i = 0; i < netPlayers.Count; i++)
            {
                PlayerMovement moveScript = netPlayers[i].PlayerObject.GetComponent<PlayerMovement>();
                PlayerHealth healthScript = moveScript.GetComponent<PlayerHealth>();

                if (healthScript.isAlive.Value)
                {
                    // Last player alive awarded one point
                    AwardPoint(healthScript.OwnerClientId);
                }
            }
        }

        else
        {
            Debug.Log("Is a draw, no points awarded");
        }

        yield return new WaitForSeconds(2);

        bool matchOver = false;

        foreach (PlayerScore score in GetRankedPlayers())
        {
            if (score.points >= 3)
            {
                matchOver = true;

                EndMatch(score.clientId);
                break;
            }
        }

        if (!matchOver)
        {
            clientsReady = false;
            StartNewRound();
            roundEnding = false;
        }
    }

    public void StartNewRound()
    {
        playersAlive.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
        GameObject[] missiles = GameObject.FindGameObjectsWithTag("Missile");

        SwitchAllPlayerMovementRPC(false, 0.1f);

        foreach (GameObject missile in missiles)
        {
            DestroyImmediate(missile);
        }

        TilemapGen.Instance.GenerateAutoSmooth();
    }

    IEnumerator RoundStartCountdown()
    {
        yield return new WaitForSeconds(0.5f);

        // first countdown sprite
        Debug.Log("READY");

        yield return new WaitForSeconds(1);

        // second countdown sprite
        Debug.Log("SET");

        yield return new WaitForSeconds(1);

        // third countdown sprite
        Debug.Log("ROLL!!");

        yield return new WaitForSeconds(1);

        // allow players to move
        SwitchAllPlayerMovementRPC(true, 0);
    }

    public void EndMatch(ulong winningID)
    {
        // resets all scores
        for (int i = 0; i < scores.Count; i++)
        {
            PlayerScore score = scores[i];
            score.points = 0;
            scores[i] = score;
        }

        for (int i = 0; i < netPlayers.Count; i++)
        {
            PlayerMovement player = netPlayers[i].PlayerObject.GetComponent<PlayerMovement>();

            Debug.Log("Player id: " + player.playerId.ToString());
            Debug.Log("winning id: " + winningID.ToString());

            if (player.playerId == winningID)
            {
                player.isWinner.Value = true;
            }
        }

        TransitionManager.Instance.LoadScene("WinScreen");
    }

    IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(5);

        for (int i = 0;i < netPlayers.Count; i++)
        {
            PlayerMovement player = netPlayers[i].PlayerObject.GetComponent<PlayerMovement>();

            player.isWinner.Value = false;
        }

        TransitionManager.Instance.LoadScene("Lobby");
    }

    #endregion

    #region Player

    [Rpc(SendTo.Server)]
    public void SwitchAllPlayerMovementRPC(bool state, float delay)
    {
        StartCoroutine(DelayMovementEnabling(state, delay));
    }
    IEnumerator DelayMovementEnabling(bool state, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (IsHost)
        {
            for (int i = 0; i < netPlayers.Count; i++)
            {
                PlayerMovement moveScript = netPlayers[i].PlayerObject.gameObject.GetComponent<PlayerMovement>();

                moveScript.ModifyCanMoveRPC(state);
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void ModifyPlayersAliveRPC(int addAmount)
    {
        playersAlive.Value += addAmount;
    }

    void UpdateLayerOrder()
    {
        List<Player> players = new List<Player>();

        if (LobbyManager.Instance.currentLobby != null)
            players = LobbyManager.Instance.currentLobby.Players;

        for (int i = 0; i < netPlayers.Count; i++)
        {
            NetworkObject clientObj = netPlayers[i].PlayerObject;

            // Assign player id
            clientObj.GetComponent<PlayerMovement>().playerId = clientObj.OwnerClientId;

            int clientOrder;

            if (clientObj.GetComponent<PlayerMovement>().IsOwner)
            {
                clientOrder = (netPlayers.Count + 5) * 5;
            }
            else
            {
                // Set based on player join ranking
                clientOrder = (i * 5);
            }

            Renderer[] children = clientObj.GetComponentsInChildren<Renderer>();

            foreach (Renderer childRend in children)
            {
                if (!clientObj.GetComponent<PlayerMovement>().layerUpdated)
                {
                    childRend.sortingOrder += clientOrder;
                }
            }

            clientObj.GetComponent<PlayerMovement>().layerUpdated = true;
        }
    }

    #endregion

    #region Points Display

    void AwardPoint(ulong clientId)
    {
        for (int i = 0; i < scores.Count; i++)
        {
            if (scores[i].clientId == clientId)
            {
                PlayerScore score = scores[i];
                score.points++;
                scores[i] = score;
                break;
            }
        }
    }

    public List<PlayerScore> GetRankedPlayers()
    {
        List<PlayerScore> ranked = new List<PlayerScore>();

        foreach (PlayerScore score in scores)
        {
            ranked.Add(score);
        }

        ranked.Sort((a, b) => b.points.CompareTo(a.points)); // descending
        return ranked;
    }

    void OnScoresChanged(NetworkListEvent<PlayerScore> changeEvent)
    {
        // Type tells you what happened
        switch (changeEvent.Type)
        {
            case NetworkListEvent<PlayerScore>.EventType.Add:
                Debug.Log($"Player {changeEvent.Value.clientId} joined the scoreboard");
                break;

            case NetworkListEvent<PlayerScore>.EventType.Value:
                Debug.Log($"Player {changeEvent.Value.clientId} now has {changeEvent.Value.points} points");
                break;

            case NetworkListEvent<PlayerScore>.EventType.Remove:
                Debug.Log($"Player {changeEvent.Value.clientId} removed from scoreboard");
                break;
        }

        // Log full rankings after any change
        LogRankings();
    }

    void LogRankings()
    {
        List<PlayerScore> ranked = GetRankedPlayers();

        for (int i = 0; i < ranked.Count; i++)
        {
            Debug.Log($"#{i + 1} - Client {ranked[i].clientId}: {ranked[i].points} pts");
        }
    }

    #endregion
}

[System.Serializable]
public struct PlayerScore : INetworkSerializeByMemcpy, System.IEquatable<PlayerScore>
{
    public ulong clientId;
    public int points;

    public bool Equals(PlayerScore other) => clientId == other.clientId && points == other.points;
}

