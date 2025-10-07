using UnityEngine;

namespace Roads
{
    public interface IRoadSpawner
    {
        /// <summary>
        /// Spawn road prefabs for all tiles in the grid
        /// </summary>
        void SpawnRoads(IGrid<RoadTileData> grid, RoadGenerationConfig config, Transform parent);

        /// <summary>
        /// Clear all spawned roads
        /// </summary>
        void ClearRoads();
    }
}
