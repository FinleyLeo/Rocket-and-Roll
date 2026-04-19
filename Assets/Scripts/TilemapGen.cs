using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class TilemapGen : NetworkBehaviour
{
    public static TilemapGen Instance;

    int[,] cellGrid;

    int[] lastFlattenedGrid;
    int lastWidth, lastHeight;

    [Header("Map Options")]
    [SerializeField] int width;
    [SerializeField] int height;

    [Range(0, 1)]
    [SerializeField] float fillPercentage;

    [Range(0, 8)]
    [SerializeField] int neighbourThreshold;

    [SerializeField] int autoSmoothStepAmount = 10;

    [Header("References")]
    [SerializeField] Tilemap mainTileMap;
    [SerializeField] RuleTile wallTile;
    Camera cam;

    [SerializeField] LayerMask wallLayer;
    [SerializeField] HashSet<Vector2> spawnPoints;

    private void Awake()
    {
        Instance = this;

        spawnPoints = new HashSet<Vector2>();
    }

    public override void OnNetworkSpawn()
    {
        cam = Camera.main;

        if (IsHost)
        {
            StartCoroutine(DelayRoundStart());
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            GenerateAutoSmooth();
        }
    }

    IEnumerator DelayRoundStart()
    {
        yield return new WaitForSeconds(0.1f);

        InGameManager.Instance.StartNewRound();
    }

    #region Generation

    public void GenerateAutoSmooth()
    {
        GenerateMap();

        for (int i = 0; i < autoSmoothStepAmount; i++)
        {
            SmoothMap();
        }

        RenderTileMap();
        GetViableSpawnPoints();
    }

    void GenerateMap()
    {
        cellGrid = new int[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                cellGrid[x, y] = Random.value < fillPercentage ? 1 : 0;
            }

        cam.transform.position = new Vector3(width / 2, height / 2, -10);
        cam.orthographicSize = (height / 2) + 5;
    }

    void SmoothMap()
    {
        int[,] tempGrid = (int[,])cellGrid.Clone();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int wallCount = GetWallNeighbourCount(x, y, cellGrid);

                if (wallCount > neighbourThreshold)
                    tempGrid[x, y] = 1;
                else if (wallCount < neighbourThreshold)
                    tempGrid[x, y] = 0;
            }

        // Updates main grid once all calculations are complete
        cellGrid = tempGrid;
    }

    #endregion

    #region Rendering

    void RenderTileMap()
    {
        // Clear old tiles
        mainTileMap.ClearAllTiles();

        // Set new tiles
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (cellGrid[x, y] == 1)
                    mainTileMap.SetTile(new Vector3Int(x, y), wallTile);
            }

        lastFlattenedGrid = FlattenGrid(cellGrid, width, height);
        lastWidth = width;
        lastHeight = height;

        RenderOnAllClientsRPC(lastWidth, lastHeight, lastFlattenedGrid);
    }

    [Rpc(SendTo.NotServer)]
    void RenderOnAllClientsRPC(int width, int height, int[] flatGrid)
    {
        cam = Camera.main;
        cam.transform.position = new Vector3(width / 2, height / 2, -10);

        // Clear old tiles
        mainTileMap.ClearAllTiles();

        int[,] clientGrid = UnflattenGrid(flatGrid, width, height);

        // Set new tiles
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (clientGrid[x, y] == 1)
                    mainTileMap.SetTile(new Vector3Int(x, y), wallTile);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void RenderOnClientRPC(int width, int height, int[] flatGrid, RpcParams rpcParams = default)
    {
        cam = Camera.main;
        cam.transform.position = new Vector3(width / 2, height / 2, -10);

        // Clear old tiles
        mainTileMap.ClearAllTiles();

        int[,] clientGrid = UnflattenGrid(flatGrid, width, height);

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (clientGrid[x, y] == 1)
                    mainTileMap.SetTile(new Vector3Int(x, y), wallTile);
    }

    public void SendMapToClient(ulong clientId)
    {
        if (lastFlattenedGrid != null)
        {
            RenderOnClientRPC(lastWidth, lastHeight, lastFlattenedGrid, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    #endregion

    #region Spawn points

    void GetViableSpawnPoints()
    {
        // Remove old tilemap spawns
        spawnPoints.Clear();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int wallCount = GetWallNeighbourCount(x, y, cellGrid);

                if (wallCount <= 1)
                {
                    CheckForGroundBelowPoint(new Vector2Int(x, y));
                }
            }

        SetSpawnPoints();
    }

    void CheckForGroundBelowPoint(Vector2Int pointPos)
    {
        // checks for a maximum of 50 cells below the point before stopping if nothing is found
        for (int y = pointPos.y; y > pointPos.y - 50; y--)
        {
            if (y < 0) break;

            // if the current checked cell is a wall then end the loop as that is the floor, else continue checking
            if (cellGrid[pointPos.x, y] == 1)
            {
                spawnPoints.Add(new Vector2(pointPos.x + 0.5f, y + 3f)); // adds 2 units to Y axis to be above the ground and not in it
                break;
            }
        }
    }

    void SetSpawnPoints()
    {
        if (spawnPoints != null)
        {
            GameObject[] temp = GameObject.FindGameObjectsWithTag("Spawn");

            // Destroy all existing points
            foreach (GameObject point in temp)
            {
                //Destroy(point);
                DestroyImmediate(point);
            }

            foreach (Vector2 point in spawnPoints)
            {
                GameObject spawnObj = new GameObject();
                spawnObj.transform.position = point;

                spawnObj.name = $"Spawn point - ({point.x}X, {point.y}Y)";
                spawnObj.tag = "Spawn";
                spawnObj.transform.parent = transform;
            }

            StartCoroutine(SpawningManager.Instance.FindSpawnPoints());
        }
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints != null)
        {
            foreach (Vector2 point in spawnPoints)
            {
                Gizmos.DrawSphere(point, 0.5f);
            }
        }
    }

    #endregion

    #region Tools

    int GetWallNeighbourCount(int posX, int posY, int[,] grid)
    {
        int neighbourCount = 0;

        for (int x = posX - 1; x <= posX + 1; x++)
            for (int y = posY - 1; y <= posY + 1; y++)
            {
                // Keeps within bounds
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    // Doesnt include main tile
                    if (x != posX || y != posY)
                    {
                        neighbourCount += grid[x, y];
                    }
                }

                else
                {
                    neighbourCount++;
                }
            }

        return neighbourCount;
    }

    int[] FlattenGrid(int[,] grid, int w, int h)
    {
        int[] flat = new int[w * h];

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                flat[x * h + y] = grid[x, y];

        return flat;
    }

    int[,] UnflattenGrid(int[] flat, int w, int h)
    {
        int[,] grid = new int[w, h];

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                grid[x, y] = flat[x * h + y];

        return grid;
    }

    #endregion
}
