using UnityEngine;

namespace Roads
{
    public static class RoadTileDataExtensions
    {
        public static bool IsEmpty(this RoadTileData tile)
        {
            return tile.Type == RoadTileType.None;
        }

        public static bool IsOccupied(this RoadTileData tile)
        {
            return tile.Type != RoadTileType.None;
        }

        public static bool IsIntersection(this RoadTileData tile)
        {
            return tile.Type == RoadTileType.TIntersection ||
                   tile.Type == RoadTileType.FourWay ||
                   tile.Type == RoadTileType.Roundabout;
        }

        public static bool IsSpecial(this RoadTileData tile)
        {
            return tile.Type == RoadTileType.Start || tile.Type == RoadTileType.Exit;
        }

        public static int GetConnectionCount(this RoadTileData tile)
        {
            int count = 0;
            if (tile.ConnectsNorth) count++;
            if (tile.ConnectsSouth) count++;
            if (tile.ConnectsEast) count++;
            if (tile.ConnectsWest) count++;
            return count;
        }

        public static Vector3 GetConnectionPoint(this RoadTileData tile, Direction direction, RoadGenerationConfig config)
        {
            var center = GridUtility.GridToWorldPosition(tile.GridPosition.x, tile.GridPosition.y, config);
            float halfTile = config.tileSize / 2f;

            return direction switch
            {
                Direction.North => center + new Vector3(0, 0, halfTile),
                Direction.South => center + new Vector3(0, 0, -halfTile),
                Direction.East => center + new Vector3(halfTile, 0, 0),
                Direction.West => center + new Vector3(-halfTile, 0, 0),
                _ => center
            };
        }
    }
}
