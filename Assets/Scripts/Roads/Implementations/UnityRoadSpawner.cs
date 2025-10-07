using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roads
{
    public class UnityRoadSpawner : IRoadSpawner
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        public void SpawnRoads(IGrid<RoadTileData> grid, RoadGenerationConfig config, Transform parent)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (config == null) throw new ArgumentNullException(nameof(config));

            ClearRoads();

            foreach (var pos in grid.GetAllPositions())
            {
                var tile = grid.Get(pos.x, pos.y);

                // Early exit: empty tile
                if (tile.Type == RoadTileType.None) continue;

                var prefab = GetPrefabForTileType(tile.Type, config);

                // Early exit: no prefab assigned
                if (prefab == null)
                {
                    Debug.LogWarning($"No prefab assigned for {tile.Type} at ({pos.x}, {pos.y})");
                    continue;
                }

                var worldPos = GridUtility.GridToWorldPosition(pos.x, pos.y, config);
                var rotation = Quaternion.Euler(0, tile.Rotation, 0);
                var instance = Object.Instantiate(prefab, worldPos, rotation, parent);
                instance.name = $"{tile.Type}_{pos.x}_{pos.y}";

                _spawnedObjects.Add(instance);

                // Update tile with instance reference
                tile.Instance = instance;
                grid.Set(pos.x, pos.y, tile);
            }
        }

        public void ClearRoads()
        {
            foreach (var obj in _spawnedObjects)
            {
                if (obj != null)
                {
                    Object.Destroy(obj);
                }
            }
            _spawnedObjects.Clear();
        }

        private GameObject GetPrefabForTileType(RoadTileType type, RoadGenerationConfig config)
        {
            return type switch
            {
                RoadTileType.Straight => config.straightPrefab,
                RoadTileType.Corner => config.cornerPrefab,
                RoadTileType.TIntersection => config.tIntersectionPrefab,
                RoadTileType.FourWay => config.fourWayPrefab,
                RoadTileType.Roundabout => config.roundaboutPrefab,
                RoadTileType.DeadEnd => config.deadEndPrefab,
                RoadTileType.Start => config.startPrefab,
                RoadTileType.Exit => config.exitPrefab,
                _ => null
            };
        }
    }
}
