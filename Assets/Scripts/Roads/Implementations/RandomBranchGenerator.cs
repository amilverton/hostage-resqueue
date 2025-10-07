using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
    public class RandomBranchGenerator : IBranchGenerator
    {
        public List<Vector2Int> GenerateBranches(
            IGrid<RoadTileData> grid,
            List<Vector2Int> mainPath,
            RoadGenerationConfig config,
            System.Random random)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (mainPath == null) throw new ArgumentNullException(nameof(mainPath));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var allBranches = new List<Vector2Int>();
            int branchCount = 0;
            int currentRoadCount = mainPath.Count;
            int targetRoadCount = Mathf.RoundToInt(grid.Width * grid.Height * config.roadDensity);

            // Skip start and end positions
            for (int i = 1; i < mainPath.Count - 1; i++)
            {
                // Early exit: reached targets
                if (branchCount >= config.maxBranches) break;
                if (currentRoadCount >= targetRoadCount) break;

                // Early exit: didn't roll branch probability
                if (random.NextDouble() > config.branchProbability) continue;

                var branchStart = mainPath[i];
                var availableDirections = GridUtility.GetAvailableDirections(grid, branchStart);

                // Early exit: no available directions
                if (availableDirections.Count == 0) continue;

                var direction = availableDirections[random.Next(availableDirections.Count)];
                var branchLength = random.Next(config.minBranchLength, config.maxBranchLength + 1);

                var branch = GenerateSingleBranch(grid, branchStart, direction, branchLength, random);

                // Early exit: failed to generate branch
                if (branch.Count == 0) continue;

                allBranches.AddRange(branch);
                currentRoadCount += branch.Count;
                branchCount++;
            }

            return allBranches;
        }

        private List<Vector2Int> GenerateSingleBranch(
            IGrid<RoadTileData> grid,
            Vector2Int start,
            Direction initialDirection,
            int maxLength,
            System.Random random)
        {
            var branch = new List<Vector2Int>();
            var visited = new HashSet<Vector2Int>(); // Track visited positions to prevent backtracking
            var current = GridUtility.GetNeighborInDirection(start, initialDirection);

            // Early exit: invalid starting position
            if (!grid.IsValidPosition(current.x, current.y)) return branch;
            if (grid.Get(current.x, current.y).Type != RoadTileType.None) return branch;

            for (int i = 0; i < maxLength; i++)
            {
                // Early exit: already visited (prevents oscillation and duplicates)
                if (visited.Contains(current)) break;

                branch.Add(current);
                visited.Add(current);

                // Mark tile temporarily to prevent other branches from using it
                var tile = grid.Get(current.x, current.y);
                tile.Type = RoadTileType.Straight; // Placeholder to mark as occupied
                tile.GridPosition = current;
                grid.Set(current.x, current.y, tile);

                var availableDirections = GridUtility.GetAvailableDirections(grid, current);

                // Early exit: dead end reached
                if (availableDirections.Count == 0) break;

                var nextDirection = availableDirections[random.Next(availableDirections.Count)];
                var next = GridUtility.GetNeighborInDirection(current, nextDirection);

                // Early exit: next position already visited
                if (visited.Contains(next)) break;

                current = next;
            }

            return branch;
        }
    }
}
