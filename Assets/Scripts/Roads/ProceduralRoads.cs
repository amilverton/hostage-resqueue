using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
    public class ProceduralRoads : MonoBehaviour
    {
        [SerializeField] private RoadGenerationConfig config;

        // Services injected from DI container
        private IPathGenerator _pathGenerator;
        private IBranchGenerator _branchGenerator;
        private IRoadSpawner _roadSpawner;

        // State
        private IGrid<RoadTileData> _grid;
        private List<Vector2Int> _mainPath;
        private System.Random _random;
        private GameObject _roadContainer;

        /// <summary>
        /// Initialize services from DI container
        /// Can optionally provide custom implementations for testing
        /// </summary>
        public void Initialize(
            IPathGenerator pathGenerator = null,
            IBranchGenerator branchGenerator = null,
            IRoadSpawner roadSpawner = null)
        {
            // Use provided implementations or get from DI container
            _pathGenerator = pathGenerator ?? DI.GetService<IPathGenerator>();
            _branchGenerator = branchGenerator ?? DI.GetService<IBranchGenerator>();
            _roadSpawner = roadSpawner ?? DI.GetService<IRoadSpawner>();
        }

        public void GenerateRoads()
        {
            // Early exit: not initialized
            if (_pathGenerator == null)
            {
                Initialize();
            }

            // Validate configuration
            try
            {
                config.Validate();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Invalid configuration: {e.Message}");
                return;
            }

            // Clear previous generation
            ClearRoads();

            // Initialize state
            InitializeState();

            // Generate main path
            if (!TryGenerateMainPath())
            {
                Debug.LogError("Failed to generate main path");
                return;
            }

            // Mark main path in grid
            MarkPathInGrid(_mainPath, isMainPath: true);

            // Generate branches
            var branches = _branchGenerator.GenerateBranches(_grid, _mainPath, config, _random);
            MarkPathInGrid(branches, isMainPath: false);

            // Determine tile types
            FillTileTypes();

            // Spawn prefabs
            CreateRoadContainer();
            _roadSpawner.SpawnRoads(_grid, config, _roadContainer.transform);

            Debug.Log($"Generated road network: {_mainPath.Count} main path tiles, {branches.Count} branch tiles");
        }

        public void ClearRoads()
        {
            _roadSpawner?.ClearRoads();

            if (_roadContainer != null)
            {
                DestroyImmediate(_roadContainer);
                _roadContainer = null;
            }

            _grid = null;
            _mainPath = null;
        }

        public RoadTileData GetTileAt(int x, int y)
        {
            // Early exit: no grid
            if (_grid == null) return default;

            // Early exit: invalid position
            if (!_grid.IsValidPosition(x, y)) return default;

            return _grid.Get(x, y);
        }

        public List<Vector2Int> GetMainPath()
        {
            return _mainPath != null ? new List<Vector2Int>(_mainPath) : new List<Vector2Int>();
        }

        private void InitializeState()
        {
            _grid = new Grid2D<RoadTileData>(config.gridWidth, config.gridHeight);

            // Initialize all cells as empty
            foreach (var pos in _grid.GetAllPositions())
            {
                _grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
            }

            // Initialize random
            _random = config.randomSeed == 0
                ? new System.Random()
                : new System.Random(config.randomSeed);

            _mainPath = new List<Vector2Int>();
        }

        private bool TryGenerateMainPath()
        {
            const int MaxRetries = 5;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var startPos = GridUtility.GetRandomEdgePosition(
                    config.startEdge,
                    config.gridWidth,
                    config.gridHeight,
                    _random);

                var exitPos = GridUtility.GetRandomEdgePosition(
                    config.exitEdge,
                    config.gridWidth,
                    config.gridHeight,
                    _random);

                // Early exit: start and exit are the same position (or too close)
                if (startPos == exitPos || Vector2Int.Distance(startPos, exitPos) < 2)
                {
                    Debug.LogWarning($"Start and exit too close/identical: {startPos} -> {exitPos}");
                    continue;
                }

                // Clear any previous attempt's markers before setting new ones
                if (attempt > 0)
                {
                    // Reset all cells to empty (full grid reset for safety)
                    foreach (var pos in _grid.GetAllPositions())
                    {
                        _grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
                    }
                }

                // Set start and exit in grid
                var startTile = _grid.Get(startPos.x, startPos.y);
                startTile.Type = RoadTileType.Start;
                startTile.IsMainPath = true;
                startTile.GridPosition = startPos;
                _grid.Set(startPos.x, startPos.y, startTile);

                var exitTile = _grid.Get(exitPos.x, exitPos.y);
                exitTile.Type = RoadTileType.Exit;
                exitTile.IsMainPath = true;
                exitTile.GridPosition = exitPos;
                _grid.Set(exitPos.x, exitPos.y, exitTile);

                // Generate path
                _mainPath = _pathGenerator.GeneratePath(_grid, startPos, exitPos, config, _random);

                // Early exit: path generation failed
                if (_mainPath.Count == 0)
                {
                    Debug.LogWarning($"Path generation attempt {attempt + 1} failed");
                    continue;
                }

                // Early exit: path too short
                if (_mainPath.Count < config.minPathLength)
                {
                    Debug.LogWarning($"Path too short: {_mainPath.Count} < {config.minPathLength}");
                    continue;
                }

                return true;
            }

            return false;
        }

        private void MarkPathInGrid(List<Vector2Int> path, bool isMainPath)
        {
            foreach (var pos in path)
            {
                var tile = _grid.Get(pos.x, pos.y);

                // Early exit: already special tile
                if (tile.IsSpecial()) continue;

                // Mark as occupied (type will be determined later)
                tile.Type = RoadTileType.Straight; // Placeholder
                tile.IsMainPath = isMainPath;
                tile.GridPosition = pos;

                _grid.Set(pos.x, pos.y, tile);
            }
        }

        private void FillTileTypes()
        {
            foreach (var pos in _grid.GetAllPositions())
            {
                var tile = _grid.Get(pos.x, pos.y);

                // Early exit: empty tile
                if (tile.IsEmpty()) continue;

                // Determine type based on connections
                var updatedTile = RoadTileUtility.DetermineTileType(
                    _grid,
                    pos,
                    config.roundaboutProbability,
                    _random);

                _grid.Set(pos.x, pos.y, updatedTile);
            }
        }

        private void CreateRoadContainer()
        {
            _roadContainer = new GameObject("Road Network");
            _roadContainer.transform.SetParent(transform);
            _roadContainer.transform.localPosition = Vector3.zero;
        }

        private void Awake()
        {
            Initialize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (config != null)
            {
                try
                {
                    config.Validate();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Configuration validation failed: {e.Message}");
                }
            }
        }
#endif
    }
}
