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

    List<Player> players;
    List<NetworkClient> netPlayers;
    public NetworkVariable<int> playersAlive = new();
    public bool gameStarted, roundEnding;

    [SerializeField] TextMeshProUGUI roomNameText;
    [SerializeField] TextMeshProUGUI roomCodeText;

    [SerializeField] Button startGameButton;
    [SerializeField] GameObject pointsInfoPrefab, pointsDisplay;

    List<PointsDisplayScript> pointsDisplays;

    bool canStartGame;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        players = new List<Player>();
        netPlayers = new List<NetworkClient>();

        UpdatePlayerLists();

        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            // Set up lobby visuals
            roomNameText.text = LobbyManager.Instance.currentLobby.Name;
            roomCodeText.text = LobbyManager.Instance.currentLobby.LobbyCode;

            startGameButton.onClick.AddListener(StartGame);
        }

        UpdatePlayerInfo();

        NetworkManager.Singleton.OnClientConnectedCallback += (ulong clientId) => UpdatePlayerInfo();
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

        // Win condition for rounds
        if (playersAlive.Value <= 1 && IsHost && gameStarted && !roundEnding)
        {
            roundEnding = true;
            StartCoroutine(RoundEndDelay());
        }

        #region testing

        if (SceneManager.GetActiveScene().name == "RanGen")
        {
            //Debug.Log(playersAlive.Value + " / " + netPlayers.Count + " players alive");
        }

        if (Keyboard.current.gKey.wasPressedThisFrame && IsHost)
        {
            StartNewRound();
        }

        #endregion
    }

    void StartGame()
    {
        for (int i = 0; i < netPlayers.Count; i++)
        {
            PlayerHealth healthScript = netPlayers[i].PlayerObject.GetComponent<PlayerHealth>();

            healthScript.ModifyAliveStateRPC(false);
        }

        NetworkManager.Singleton.SceneManager.LoadScene("RanGen", LoadSceneMode.Single);
    }

    IEnumerator RoundEndDelay()
    {
        Debug.Log("Round Finished! Starting next round...");

        for (int i = 0; i < netPlayers.Count; i++)
        {
            PlayerMovement moveScript = netPlayers[i].PlayerObject.GetComponent<PlayerMovement>();
            PlayerHealth healthScript = moveScript.GetComponent<PlayerHealth>();

            if (healthScript.isAlive.Value)
            {
                // Last player alive awarded one point
                moveScript.ModifyPointsRPC(1);
            }
        }

        yield return new WaitForSeconds(3);

        StartNewRound();
        roundEnding = false;
    }

    public void StartNewRound()
    {
        if (SceneManager.GetActiveScene().name == "RanGen")
        {
            playersAlive.Value = netPlayers.Count;

            TilemapGen.Instance.GenerateAutoSmooth();

            Debug.Log("Generated map");
        }
    }

    [Rpc(SendTo.Server)]
    public void ModifyPlayersAliveRPC(int addAmount)
    {
        playersAlive.Value += addAmount;
    }

    void UpdatePlayerLists()
    {
        if (LobbyManager.Instance.currentLobby != null)
        {
            players = LobbyManager.Instance.currentLobby.Players;
        }

        netPlayers = (List<NetworkClient>)NetworkManager.Singleton.ConnectedClientsList;
    }

    void UpdatePlayerInfo()
    {
        // Loads values of every player to get new joiner up to date and old joiners to track new player
        UpdatePlayerLists();

        for (int i = 0; i < netPlayers.Count; i++)
        {
            GameObject clientObj = netPlayers[i].PlayerObject.gameObject;

            // Assign player id
            clientObj.GetComponent<PlayerMovement>().playerId = players[i].Id;

            #region layerOrdering

            int clientOrder;

            if (clientObj.GetComponent<PlayerMovement>().IsOwner)
            {
                clientOrder = 12;
            }
            else
            {
                // Set based on player join ranking
                clientOrder = ((int)clientObj.GetComponent<PlayerMovement>().NetworkObjectId - 1);
            }

            //SetOrder(clientObj, clientOrder, 2);

            //for (int j = 0; j < clientObj.transform.childCount; j++)
            //{
            //    GameObject childObj = clientObj.transform.GetChild(j).gameObject;

            //    if (childObj.transform.childCount > 0)
            //    {
            //        for (int k = 0; k < childObj.transform.childCount; k++)
            //        {
            //            GameObject _childObj = childObj.transform.GetChild(k).gameObject;

            //            if (_childObj.transform.childCount > 0)
            //            {
            //                for (int l = 0; l < _childObj.transform.childCount; l++)
            //                {
            //                    GameObject __childObj = _childObj.transform.GetChild(l).gameObject;

            //                    SetOrder(__childObj, clientOrder, 5);
            //                }
            //            }

            //            SetOrder(_childObj, clientOrder, 5);
            //        }
            //    }

            //    SetOrder(childObj, clientOrder, 5);
            //}

            Renderer[] children = clientObj.GetComponentsInChildren<Renderer>();

            foreach (Renderer childRend in children)
            {
                if (childRend.sortingOrder < 5)
                {
                    childRend.sortingOrder += clientOrder;
                }
            }

            #endregion layerOrdering
        }
    }

    #region Points Display

    void UpdateDisplay()
    {

    }

    void ModifyPointDisplay()
    {

    }

    #endregion

    //void SetOrder(GameObject obj, int orderIncrement, int maxOrder)
    //{
    //    if (obj.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
    //    {
    //        SpriteRenderer _sr = sr;

    //        if (_sr.sortingOrder < maxOrder)
    //        {
    //            _sr.sortingOrder += orderIncrement;
    //        }
    //    }
    //    else if (obj.TryGetComponent<Canvas>(out Canvas canvas))
    //    {
    //        Canvas _canvas = canvas;

    //        if (_canvas.sortingOrder < maxOrder)
    //        {
    //            _canvas.sortingOrder += orderIncrement;
    //        }
    //    }
    //    else if (obj.TryGetComponent<ParticleSystemRenderer>(out ParticleSystemRenderer ps))
    //    {
    //        ParticleSystemRenderer _ps = ps.GetComponent<ParticleSystemRenderer>();

    //        if (_ps.sortingOrder < maxOrder)
    //        {
    //            _ps.sortingOrder += orderIncrement;
    //        }
    //    }
    //}
}

