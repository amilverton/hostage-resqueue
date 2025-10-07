using System.Collections.Generic;
using NUnit.Framework;
using Roads;
using UnityEngine;

namespace RoadsTests
{
    public class PathGeneratorTests
    {
        [Test]
        public void GeneratePath_ReturnsValidPath()
        {
            var grid = new Grid2D<RoadTileData>(10, 10);
            foreach (var pos in grid.GetAllPositions())
            {
                grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
            }

            var start = new Vector2Int(0, 5);
            var end = new Vector2Int(9, 5);

            var startTile = grid.Get(start.x, start.y);
            startTile.Type = RoadTileType.Start;
            startTile.GridPosition = start;
            grid.Set(start.x, start.y, startTile);

            var exitTile = grid.Get(end.x, end.y);
            exitTile.Type = RoadTileType.Exit;
            exitTile.GridPosition = end;
            grid.Set(end.x, end.y, exitTile);

            var config = new RoadGenerationConfig
            {
                gridWidth = 10,
                gridHeight = 10,
                pathWindingness = 0f
            };

            var generator = new BiasedRandomWalkGenerator();
            var random = new System.Random(1234);

            var path = generator.GeneratePath(grid, start, end, config, random);

            Assert.IsNotEmpty(path);
            Assert.AreEqual(start, path[0]);
            Assert.AreEqual(end, path[path.Count - 1]);

            // Ensure unique positions to avoid loops
            var uniquePositions = new HashSet<Vector2Int>(path);
            Assert.AreEqual(path.Count, uniquePositions.Count);
        }
    }
}
