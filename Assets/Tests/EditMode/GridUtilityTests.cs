using System.Linq;
using NUnit.Framework;
using Roads;
using UnityEngine;

namespace RoadsTests
{
    public class GridUtilityTests
    {
        [Test]
        public void GridToWorldPosition_UsesGridCenter()
        {
            var config = new RoadGenerationConfig
            {
                gridWidth = 4,
                gridHeight = 4,
                tileSize = 10f
            };

            var world = GridUtility.GridToWorldPosition(2, 2, config);

            Assert.AreEqual(Vector3.zero, world);
        }

        [Test]
        public void GetAvailableDirections_IgnoresOccupiedNeighbors()
        {
            var grid = new Grid2D<RoadTileData>(3, 3);
            foreach (var pos in grid.GetAllPositions())
            {
                grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
            }

            var center = grid.Get(1, 1);
            center.Type = RoadTileType.Straight;
            center.GridPosition = new Vector2Int(1, 1);
            grid.Set(1, 1, center);

            var blocked = grid.Get(1, 2);
            blocked.Type = RoadTileType.Straight;
            blocked.GridPosition = new Vector2Int(1, 2);
            grid.Set(1, 2, blocked);

            var directions = GridUtility.GetAvailableDirections(grid, new Vector2Int(1, 1));

            Assert.IsFalse(directions.Contains(Direction.North));
            Assert.IsTrue(directions.Contains(Direction.South));
            Assert.IsTrue(directions.Contains(Direction.East));
            Assert.IsTrue(directions.Contains(Direction.West));
        }
    }
}
