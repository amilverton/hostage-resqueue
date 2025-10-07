using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
    public interface IBranchGenerator
    {
        /// <summary>
        /// Generate branches off an existing path
        /// </summary>
        List<Vector2Int> GenerateBranches(
            IGrid<RoadTileData> grid,
            List<Vector2Int> mainPath,
            RoadGenerationConfig config,
            System.Random random);
    }
}
