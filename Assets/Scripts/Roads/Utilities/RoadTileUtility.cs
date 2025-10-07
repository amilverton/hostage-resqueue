using System;
using UnityEngine;

namespace Roads
{
    public static class RoadTileUtility
    {
        public static RoadTileData DetermineTileType(
            IGrid<RoadTileData> grid,
            Vector2Int position,
            float roundaboutProbability,
            System.Random random)
        {
            var tile = grid.Get(position.x, position.y);

            // Early exit: already set (Start/Exit)
            if (tile.Type == RoadTileType.Start || tile.Type == RoadTileType.Exit)
            {
                return tile;
            }

            // Early exit: empty tile
            if (tile.Type == RoadTileType.None)
            {
                return tile;
            }

            // Check connections
            bool north = GridUtility.HasRoadNeighbor(grid, position, Direction.North);
            bool south = GridUtility.HasRoadNeighbor(grid, position, Direction.South);
            bool east = GridUtility.HasRoadNeighbor(grid, position, Direction.East);
            bool west = GridUtility.HasRoadNeighbor(grid, position, Direction.West);

            int connectionCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);

            tile.ConnectsNorth = north;
            tile.ConnectsSouth = south;
            tile.ConnectsEast = east;
            tile.ConnectsWest = west;

            // Determine type and rotation based on connections
            switch (connectionCount)
            {
                case 1:
                    // Single connection = dead end (not straight road)
                    tile.Type = RoadTileType.DeadEnd;
                    tile.Rotation = CalculateDeadEndRotation(north, south, east, west);
                    break;

                case 2:
                    if ((north && south) || (east && west))
                    {
                        tile.Type = RoadTileType.Straight;
                        tile.Rotation = (north && south) ? 0 : 90;
                    }
                    else
                    {
                        tile.Type = RoadTileType.Corner;
                        tile.Rotation = CalculateCornerRotation(north, south, east, west);
                    }
                    break;

                case 3:
                    tile.Type = RoadTileType.TIntersection;
                    tile.Rotation = CalculateTIntersectionRotation(north, south, east, west);
                    break;

                case 4:
                    tile.Type = (random.NextDouble() < roundaboutProbability)
                        ? RoadTileType.Roundabout
                        : RoadTileType.FourWay;
                    tile.Rotation = 0;
                    break;

                default:
                    // Should not happen
                    tile.Type = RoadTileType.None;
                    break;
            }

            return tile;
        }

        private static int CalculateDeadEndRotation(bool north, bool south, bool east, bool west)
        {
            if (north) return 0;
            if (east) return 90;
            if (south) return 180;
            if (west) return 270;
            return 0;
        }

        private static int CalculateCornerRotation(bool north, bool south, bool east, bool west)
        {
            // Base prefab connects North and East
            if (north && east) return 0;
            if (east && south) return 90;
            if (south && west) return 180;
            if (west && north) return 270;
            return 0;
        }

        private static int CalculateTIntersectionRotation(bool north, bool south, bool east, bool west)
        {
            // Base prefab has opening to south (connects N, E, W)
            if (!north) return 0;   // Opening south
            if (!east) return 90;   // Opening west
            if (!south) return 180; // Opening north
            if (!west) return 270;  // Opening east
            return 0;
        }
    }
}
