using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
    public class BiasedRandomWalkGenerator : IPathGenerator
    {
        private const int MaxAttempts = 10000;

        public List<Vector2Int> GeneratePath(
            IGrid<RoadTileData> grid,
            Vector2Int start,
            Vector2Int end,
            RoadGenerationConfig config,
            System.Random random)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var path = new List<Vector2Int> { start };
            var visited = new HashSet<Vector2Int> { start };
            var current = start;
            int attempts = 0;

            while (current != end && attempts < MaxAttempts)
            {
                attempts++;

                var validNeighbors = GetValidNeighbors(grid, current, end, visited);

                // Early exit: no valid neighbors
                if (validNeighbors.Count == 0)
                {
                    if (!TryBacktrack(path, visited, out current))
                    {
                        // Failed to generate path
                        return new List<Vector2Int>();
                    }
                    continue;
                }

                // Choose next position (biased toward exit)
                var next = ChooseNextPosition(validNeighbors, end, config.pathWindingness, random);

                current = next;
                path.Add(current);
                visited.Add(current);
            }

            // Early exit: failed to reach end
            if (current != end)
            {
                return new List<Vector2Int>();
            }

            return path;
        }

        private List<Vector2Int> GetValidNeighbors(
            IGrid<RoadTileData> grid,
            Vector2Int position,
            Vector2Int target,
            HashSet<Vector2Int> visited)
        {
            var neighbors = new List<Vector2Int>();
            var directions = new[] {
                new Vector2Int(0, 1),   // North
                new Vector2Int(0, -1),  // South
                new Vector2Int(1, 0),   // East
                new Vector2Int(-1, 0)   // West
            };

            foreach (var dir in directions)
            {
                var neighbor = position + dir;

                // Early exits for invalid neighbors
                if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;
                if (visited.Contains(neighbor) && neighbor != target) continue;
                if (grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None && neighbor != target) continue;

                neighbors.Add(neighbor);
            }

            return neighbors;
        }

        private bool TryBacktrack(List<Vector2Int> path, HashSet<Vector2Int> visited, out Vector2Int position)
        {
            // Early exit: can't backtrack from start
            if (path.Count <= 1)
            {
                position = default;
                return false;
            }

            var lastPos = path[path.Count - 1];
            path.RemoveAt(path.Count - 1);
            visited.Remove(lastPos);
            position = path[path.Count - 1];
            return true;
        }

        private Vector2Int ChooseNextPosition(
            List<Vector2Int> candidates,
            Vector2Int target,
            float windingness,
            System.Random random)
        {
            // Greedy toward target based on windingness
            if (random.NextDouble() > windingness)
            {
                return GetClosestToTarget(candidates, target);
            }

            // Random choice
            return candidates[random.Next(candidates.Count)];
        }

        private Vector2Int GetClosestToTarget(List<Vector2Int> positions, Vector2Int target)
        {
            var closest = positions[0];
            var minDist = Vector2Int.Distance(closest, target);

            foreach (var pos in positions)
            {
                var dist = Vector2Int.Distance(pos, target);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = pos;
                }
            }

            return closest;
        }
    }
}
