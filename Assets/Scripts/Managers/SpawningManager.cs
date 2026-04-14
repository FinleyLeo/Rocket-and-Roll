using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpawningManager : NetworkBehaviour
{
    public static SpawningManager Instance;

    private Queue<ulong> pendingClients = new Queue<ulong>();
    public NetworkList<SpawnPoint> spawnPoints = new NetworkList<SpawnPoint>();

    public NetworkVariable<bool> allPointsUsed = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> pointsReady = new NetworkVariable<bool>();

    #region Events

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            StartCoroutine(FindSpawnPoints());

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnection;
        }
    }

    void HandleClientConnection(ulong clientId)
    {
        // Only send tilemap gen if in generation scene
        if (SceneManager.GetActiveScene().name == "RanGen")
        {
            TilemapGen.Instance.SendMapToClient(clientId);
        }

        if (pointsReady.Value)
        {
            SpawnSingleClient(clientId); // spawn just the newly joined player
        }

        else
        {
            Debug.Log($"Points not finished generating, client: ({clientId}) added to queue");
            pendingClients.Enqueue(clientId);
        }
    }

    public override void OnDestroy()
    {
        if (NetworkManager.Singleton == null || !IsHost) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnection;
    }

    #endregion

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

    public IEnumerator FindSpawnPoints()
    {
        // Reset all spawn points
        spawnPoints.Clear();

        pointsReady.Value = false;
        allPointsUsed.Value = false;

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
                    spawnPoints.Add(new SpawnPoint { 
                        position = go.transform.position,
                        used = false
                    });
                }
            }

            yield return null;
        }

        pointsReady.Value = true;

        Debug.Log("found spawn points");

        SpawnPlayersRPC();

        while (pendingClients.Count > 0)
        {
            SpawnSingleClient(pendingClients.Dequeue());
        }
    }

    [Rpc(SendTo.Server)]
    void SpawnPlayersRPC()
    {
        // iterates through every client connected to the hosts lobby
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            ulong clientId = client.ClientId;

            StartCoroutine(FindSuitableSpawn(clientId));
        }
    }

    IEnumerator FindSuitableSpawn(ulong clientId)
    {
        bool spawnFound = false;

        Vector2 spawnPos = Vector2.zero;

        while (!spawnFound)
        {
            int ranIndex = Random.Range(0, spawnPoints.Count);

            if (spawnPoints[ranIndex].used)
            {
                if (allPointsUsed.Value)
                {
                    spawnFound = true;
                    spawnPos = spawnPoints[ranIndex].position;

                    SpawnPoint point = spawnPoints[ranIndex];
                    point.used = true;
                    spawnPoints[ranIndex] = point;
                }
            }
            else
            {
                spawnFound = true;
                spawnPos = spawnPoints[ranIndex].position;

                SpawnPoint point = spawnPoints[ranIndex];
                point.used = true;
                spawnPoints[ranIndex] = point;
            }

            yield return null;
        }

        Debug.Log("Assigned point at " + spawnPos + " to client " + clientId);

        // tell client where to spawn
        TeleportClientRPC(spawnPos, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void SpawnSingleClient(ulong clientId)
    {
        StartCoroutine(FindSuitableSpawn(clientId));
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
}
