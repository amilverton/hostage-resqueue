using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
    public static class GridUtility
    {
        private static readonly Vector2Int[] Directions = {
            new Vector2Int(0, 1),   // North
            new Vector2Int(0, -1),  // South
            new Vector2Int(1, 0),   // East
            new Vector2Int(-1, 0)   // West
        };

        public static Vector3 GridToWorldPosition(int x, int y, RoadGenerationConfig config)
        {
            float worldX = (x - config.gridWidth / 2f) * config.tileSize;
            float worldZ = (y - config.gridHeight / 2f) * config.tileSize;
            return new Vector3(worldX, 0, worldZ);
        }

        public static Vector2Int WorldToGridPosition(Vector3 worldPos, RoadGenerationConfig config)
        {
            int x = Mathf.RoundToInt(worldPos.x / config.tileSize + config.gridWidth / 2f);
            int y = Mathf.RoundToInt(worldPos.z / config.tileSize + config.gridHeight / 2f);
            return new Vector2Int(x, y);
        }

        public static Vector2Int GetRandomEdgePosition(GridEdge edge, int gridWidth, int gridHeight, System.Random random)
        {
            return edge switch
            {
                GridEdge.North => new Vector2Int(random.Next(0, gridWidth), gridHeight - 1),
                GridEdge.South => new Vector2Int(random.Next(0, gridWidth), 0),
                GridEdge.East => new Vector2Int(gridWidth - 1, random.Next(0, gridHeight)),
                GridEdge.West => new Vector2Int(0, random.Next(0, gridHeight)),
                _ => Vector2Int.zero
            };
        }

        public static List<Direction> GetAvailableDirections(IGrid<RoadTileData> grid, Vector2Int position)
        {
            var available = new List<Direction>();

            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                var neighbor = GetNeighborInDirection(position, dir);

                // Early exits
                if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;
                if (grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None) continue;

                available.Add(dir);
            }

            return available;
        }

        public static Vector2Int GetNeighborInDirection(Vector2Int position, Direction direction)
        {
            return direction switch
            {
                Direction.North => position + new Vector2Int(0, 1),
                Direction.South => position + new Vector2Int(0, -1),
                Direction.East => position + new Vector2Int(1, 0),
                Direction.West => position + new Vector2Int(-1, 0),
                _ => position
            };
        }

        public static bool HasRoadNeighbor(IGrid<RoadTileData> grid, Vector2Int position, Direction direction)
        {
            var neighbor = GetNeighborInDirection(position, direction);

            // Early exit: out of bounds
            if (!grid.IsValidPosition(neighbor.x, neighbor.y)) return false;

            return grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None;
        }

        public static List<Vector2Int> GetNeighbors(IGrid<RoadTileData> grid, Vector2Int position)
        {
            var neighbors = new List<Vector2Int>();

            foreach (var dir in Directions)
            {
                var neighbor = position + dir;

                // Early exit: invalid position
                if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;

                neighbors.Add(neighbor);
            }

            return neighbors;
        }
    }
}
