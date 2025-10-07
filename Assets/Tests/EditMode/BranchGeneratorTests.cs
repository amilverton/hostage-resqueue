using System.Collections.Generic;
using NUnit.Framework;
using Roads;
using UnityEngine;

namespace RoadsTests
{
    public class BranchGeneratorTests
    {
        [Test]
        public void GenerateBranches_StaysWithinGrid()
        {
            var grid = new Grid2D<RoadTileData>(5, 5);
            foreach (var pos in grid.GetAllPositions())
            {
                grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
            }

            var mainPath = new List<Vector2Int>();
            for (int x = 0; x < 5; x++)
            {
                var pos = new Vector2Int(x, 2);
                mainPath.Add(pos);

                var tile = grid.Get(pos.x, pos.y);
                tile.Type = RoadTileType.Straight;
                tile.IsMainPath = true;
                tile.GridPosition = pos;
                grid.Set(pos.x, pos.y, tile);
            }

            var config = new RoadGenerationConfig
            {
                gridWidth = 5,
                gridHeight = 5,
                branchProbability = 1f,
                minBranchLength = 1,
                maxBranchLength = 2,
                maxBranches = 3,
                roadDensity = 0.5f
            };

            var generator = new RandomBranchGenerator();
            var random = new System.Random(42);

            var branches = generator.GenerateBranches(grid, mainPath, config, random);

            foreach (var pos in branches)
            {
                Assert.IsTrue(grid.IsValidPosition(pos.x, pos.y));
                Assert.IsFalse(mainPath.Contains(pos));
            }
        }
    }
}
