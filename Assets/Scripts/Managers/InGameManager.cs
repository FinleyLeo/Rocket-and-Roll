using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameManager : NetworkBehaviour
{
    List<Player> players;
    List<NetworkClient> netPlayers;

    NetworkList<SpawnPoint> spawnPoints = new NetworkList<SpawnPoint>();
    NetworkVariable<bool> allPointsUsed = new NetworkVariable<bool>(false);
    NetworkVariable<bool> pointsReady = new NetworkVariable<bool>();

    [SerializeField] TextMeshProUGUI roomNameText;
    [SerializeField] TextMeshProUGUI roomCodeText;

    [SerializeField] Button startGameButton;

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

            if (IsHost)
            {
                startGameButton.interactable = true;
                startGameButton.onClick.AddListener(StartGame);
            }
        }

        if (IsHost)
        {
            allPointsUsed.Value = true;
            StartCoroutine(FindSpawnPoints());

            NetworkManager.Singleton.OnClientConnectedCallback += (ulong clientId) =>
            {
                UpdatePlayerInfo();

                if (pointsReady.Value)
                {
                    SpawnSingleClientRPC(clientId); // spawn just the newly joined player
                }
            };
        }

        UpdatePlayerInfo();

        NetworkManager.Singleton.OnClientConnectedCallback += (ulong clientId) => UpdatePlayerInfo();
    }

    private void Update()
    {
        if (!IsHost) return;

        bool anyFree = false;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (!spawnPoints[i].used)
            {
                anyFree = true;
                break;
            }
        }

        allPointsUsed.Value = !anyFree;
    }

    void StartGame()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("RanGen", LoadSceneMode.Single);
    }

    IEnumerator FindSpawnPoints()
    {
        #region spawnpoint ref

        // checks for spawn points until found (atleast one spawn point)
        while (spawnPoints.Count == 0)
        {
            Debug.Log("Looking for spawn points");

            GameObject[] temp = GameObject.FindGameObjectsWithTag("Spawn");

            if (temp.Length > 0)
            {
                spawnPoints.Clear();
                foreach (GameObject go in temp)
                {
                    spawnPoints.Add(new SpawnPoint { position = go.transform.position, used = false });
                }
            }

            yield return null;
        }

        pointsReady.Value = true;
        Debug.Log("found spawn points");

        #endregion

        SpawnPlayerRPC();
    }

    [Rpc(SendTo.Server)]
    void SpawnPlayerRPC()
    {
        for (int i = 0; i < netPlayers.Count; i++)
        {
            NetworkObject clientObj = netPlayers[i].PlayerObject;
            ulong clientId = netPlayers[i].ClientId;

            Vector2 spawnPos;

            if (!allPointsUsed.Value)
            {
                for (int j = 0; j < spawnPoints.Count; j++)
                {
                    if (!spawnPoints[j].used)
                    {
                        spawnPos = spawnPoints[j].position;

                        SpawnPoint point = spawnPoints[j];
                        point.used = true;
                        spawnPoints[j] = point;

                        Debug.Log("Assigned point " + j + " to client " + clientId);

                        // tell client where to spawn
                        TeleportClientRPC(spawnPos, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                        return;
                    }
                }
            }

            int ranIndex = Random.Range(0, spawnPoints.Count);
            spawnPos = spawnPoints[ranIndex].position;

            Debug.Log("No spots left, Randomly spawned at: " + spawnPoints[ranIndex].position);
            TeleportClientRPC(spawnPos, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnSingleClientRPC(ulong clientId)
    {
        Vector2 spawnPos;

        if (!allPointsUsed.Value)
        {
            for (int j = 0; j < spawnPoints.Count; j++)
            {
                if (!spawnPoints[j].used)
                {
                    spawnPos = spawnPoints[j].position;

                    SpawnPoint point = spawnPoints[j];
                    point.used = true;
                    spawnPoints[j] = point;

                    TeleportClientRPC(spawnPos, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                    return;
                }
            }
        }

        // if all points used
        int ranIndex = Random.Range(0, spawnPoints.Count);
        spawnPos = spawnPoints[ranIndex].position;
        TeleportClientRPC(spawnPos, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server)]
    public void StartNewRoundRPC()
    {
        // Reset all spawn points
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            SpawnPoint point = spawnPoints[i];
            point.used = false;
            spawnPoints[i] = point;
        }

        pointsReady.Value = false;
        allPointsUsed.Value = false;

        StartCoroutine(FindSpawnPoints());
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void TeleportClientRPC(Vector2 position, RpcParams rpcParams = default)
    {
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            NetworkObject obj = client.PlayerObject;

            if (obj.IsOwner)
            {
                obj.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                obj.transform.position = position;
                Debug.Log("Teleported to: " + position);
                break;
            }
        }
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

            SetOrder(clientObj, clientOrder, 2);

            for (int j = 0; j < clientObj.transform.childCount; j++)
            {
                GameObject childObj = clientObj.transform.GetChild(j).gameObject;

                if (childObj.transform.childCount > 0)
                {
                    for (int k = 0; k < childObj.transform.childCount; k++)
                    {
                        GameObject _childObj = childObj.transform.GetChild(k).gameObject;

                        if (_childObj.transform.childCount > 0)
                        {
                            for (int l = 0; l < _childObj.transform.childCount; l++)
                            {
                                GameObject __childObj = _childObj.transform.GetChild(l).gameObject;

                                SetOrder(__childObj, clientOrder, 5);
                            }
                        }

                        SetOrder(_childObj, clientOrder, 5);
                    }
                }

                SetOrder(childObj, clientOrder, 5);
            }

            #endregion layerOrdering
        }
    }

    void SetOrder(GameObject obj, int orderIncrement, int maxOrder)
    {
        if (obj.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
        {
            SpriteRenderer _sr = sr;

            if (_sr.sortingOrder < maxOrder)
            {
                _sr.sortingOrder += orderIncrement;
            }
        }
        else if (obj.TryGetComponent<Canvas>(out Canvas canvas))
        {
            Canvas _canvas = canvas;

            if (_canvas.sortingOrder < maxOrder)
            {
                _canvas.sortingOrder += orderIncrement;
            }
        }
        else if (obj.TryGetComponent<ParticleSystemRenderer>(out ParticleSystemRenderer ps))
        {
            ParticleSystemRenderer _ps = ps.GetComponent<ParticleSystemRenderer>();

            if (_ps.sortingOrder < maxOrder)
            {
                _ps.sortingOrder += orderIncrement;
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.LeaveLobby();
        }
    }
}

[System.Serializable] 
public struct SpawnPoint : System.IEquatable<SpawnPoint>, INetworkSerializeByMemcpy
{
    public Vector2 position;
    public bool used;

    public bool Equals(SpawnPoint other)
    {
        return position.Equals(other.position) && used == other.used;
    }
}