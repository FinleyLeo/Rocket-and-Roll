using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public class TilemapGen : MonoBehaviour
{
    int[,] cellGrid;

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
    [SerializeField] List<Vector3> spawnPoints;

    private void Start()
    {
        cam = Camera.main;

        GenerateMap();

        for (int i = 0; i < autoSmoothStepAmount; i++)
        {
            SmoothMap();
        }

        RenderTileMap();

        GetViableSpawnPoints();
    }

    private void Update()
    {
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            GenerateAutoSmooth();
        }
    }

    void GenerateAutoSmooth()
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

    void GetViableSpawnPoints()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int wallCount = GetWallNeighbourCount(x, y, cellGrid);

                if (wallCount <= 1)
                {
                    Debug.Log("Spot found with enough space, checking floor...");

                    spawnPoints.Add(new Vector3(x, y, 0));

                    // check if ground is below
                    // if ground, add to spawn points list
                    if (Physics2D.Raycast(new Vector2(x, y), Vector2.down, 10, wallLayer))
                    {
                        Debug.Log("Point found at: " + x + "X, " + y + "Y");
                        //spawnPoints.Add(new Vector3(x, y, 0));
                    }
                }
            }
    }

    private void OnDrawGizmos()
    {
        if (spawnPoints != null)
        {
            foreach (Vector3 point in spawnPoints)
            {
                Gizmos.DrawSphere(point, 2f);
            }
        }
    }
}
