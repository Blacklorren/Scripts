using System.Collections.Generic;
using UnityEngine;
using HandballManager.Simulation.Engines; // Added for SimPlayer

namespace HandballManager.Simulation.Physics
{
    public class SpatialGrid
{
    private readonly Dictionary<int, List<SimPlayer>> cells;
    private readonly float cellSize;
    private readonly int gridWidth;
    private readonly int gridHeight;

    public SpatialGrid(float worldWidth, float worldHeight, float cellSize)
    {
        this.cellSize = cellSize;
        this.gridWidth = Mathf.CeilToInt(worldWidth / cellSize);
        this.gridHeight = Mathf.CeilToInt(worldHeight / cellSize);
        this.cells = new Dictionary<int, List<SimPlayer>>();
    }

    public void Clear()
    {
        cells.Clear();
    }

    public void Insert(SimPlayer player)
    {
        int cellIndex = GetCellIndex(player.Position);
        if (!cells.ContainsKey(cellIndex))
        {
            cells[cellIndex] = new List<SimPlayer>();
        }
        cells[cellIndex].Add(player);
    }

    public List<SimPlayer> GetNearbySimPlayers(Vector2 position, float radius)
    {
        var nearbySimPlayers = new List<SimPlayer>();
        int minCellX = Mathf.FloorToInt((position.x - radius) / cellSize);
        int maxCellX = Mathf.CeilToInt((position.x + radius) / cellSize);
        int minCellY = Mathf.FloorToInt((position.y - radius) / cellSize);
        int maxCellY = Mathf.CeilToInt((position.y + radius) / cellSize);
        minCellX = Mathf.Clamp(minCellX, 0, gridWidth - 1);
        maxCellX = Mathf.Clamp(maxCellX, 0, gridWidth - 1);
        minCellY = Mathf.Clamp(minCellY, 0, gridHeight - 1);
        maxCellY = Mathf.Clamp(maxCellY, 0, gridHeight - 1);
        for (int x = minCellX; x <= maxCellX; x++)
        {
            for (int y = minCellY; y <= maxCellY; y++)
            {
                int cellIndex = x + y * gridWidth;
                if (cells.ContainsKey(cellIndex))
                {
                    nearbySimPlayers.AddRange(cells[cellIndex]);
                }
            }
        }
        return nearbySimPlayers;
    }

    private int GetCellIndex(Vector2 position)
    {
        int cellX = Mathf.Clamp(Mathf.FloorToInt(position.x / cellSize), 0, gridWidth - 1);
        int cellY = Mathf.Clamp(Mathf.FloorToInt(position.y / cellSize), 0, gridHeight - 1);
        return cellX + cellY * gridWidth;
    }
}
}