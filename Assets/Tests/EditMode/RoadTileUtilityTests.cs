using NUnit.Framework;
using Roads;
using UnityEngine;

namespace RoadsTests
{
    public class RoadTileUtilityTests
    {
        [Test]
        public void DetermineTileType_WithTwoOppositeConnections_ReturnsStraight()
        {
            var grid = CreateGrid(3, 3);

            SetTile(grid, 1, 1, RoadTileType.Straight);
            SetTile(grid, 1, 2, RoadTileType.Straight);
            SetTile(grid, 1, 0, RoadTileType.Straight);

            var result = RoadTileUtility.DetermineTileType(
                grid,
                new Vector2Int(1, 1),
                roundaboutProbability: 0f,
                random: new System.Random(0));

            Assert.AreEqual(RoadTileType.Straight, result.Type);
            Assert.AreEqual(0, result.Rotation);
        }

        [Test]
        public void DetermineTileType_WithThreeConnections_ReturnsTIntersection()
        {
            var grid = CreateGrid(3, 3);

            SetTile(grid, 1, 1, RoadTileType.Straight);
            SetTile(grid, 1, 2, RoadTileType.Straight);
            SetTile(grid, 1, 0, RoadTileType.Straight);
            SetTile(grid, 2, 1, RoadTileType.Straight);

            var result = RoadTileUtility.DetermineTileType(
                grid,
                new Vector2Int(1, 1),
                roundaboutProbability: 0f,
                random: new System.Random(0));

            Assert.AreEqual(RoadTileType.TIntersection, result.Type);
            Assert.AreEqual(270, result.Rotation);
        }

        private static Grid2D<RoadTileData> CreateGrid(int width, int height)
        {
            var grid = new Grid2D<RoadTileData>(width, height);
            foreach (var pos in grid.GetAllPositions())
            {
                grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
            }
            return grid;
        }

        private static void SetTile(IGrid<RoadTileData> grid, int x, int y, RoadTileType type)
        {
            var tile = grid.Get(x, y);
            tile.Type = type;
            tile.GridPosition = new Vector2Int(x, y);
            grid.Set(x, y, tile);
        }
    }
}
