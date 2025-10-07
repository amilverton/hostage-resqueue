using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
    public interface IPathGenerator
    {
        /// <summary>
        /// Generate a path from start to end position
        /// </summary>
        /// <param name="grid">The grid to generate path on</param>
        /// <param name="start">Start position</param>
        /// <param name="end">End position</param>
        /// <param name="config">Generation configuration</param>
        /// <param name="random">Random number generator</param>
        /// <returns>List of positions forming the path (including start and end)</returns>
        List<Vector2Int> GeneratePath(
            IGrid<RoadTileData> grid,
            Vector2Int start,
            Vector2Int end,
            RoadGenerationConfig config,
            System.Random random);
    }
}
