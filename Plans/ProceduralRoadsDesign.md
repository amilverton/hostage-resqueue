# Procedural Road System - Implementation Design

## Overview
Generate a procedural road network on a 2D grid with guaranteed start and exit points on opposite edges, supporting dead-ends and branches. The system will use Unity types and be fully configurable for different world sizes across game progression.

## Core Requirements
- Single entry point (Start) and single exit point (Exit) on opposite edges
- Guaranteed valid path between start and exit
- Support for dead-ends and branches off the main path
- Configurable grid size for different levels (small early game, large late game)
- All road tiles same size (configurable for future changes)
- 2D grid-based storage for easy position lookup
- Connections at midpoint of cell edges

## Data Structures

### RoadTileType (enum)
Defines all possible road piece types that can be placed on the grid.

```csharp
public enum RoadTileType
{
    None,           // Empty grid cell (no road)
    Straight,       // 2 opposite connections (North-South or East-West)
    Corner,         // 2 adjacent connections (90° turn)
    TIntersection,  // 3 connections
    FourWay,        // 4 connections (cross intersection)
    Roundabout,     // 4 connections (visual variant of FourWay)
    Start,          // 1 connection (entry point)
    Exit            // 1 connection (exit point)
}
```

**Notes:**
- `None` represents empty space that may later contain houses/scenery
- `Roundabout` is functionally identical to `FourWay` but different visually
- `Start` and `Exit` are special tiles with only one connection point

### RoadTileData (struct)
Contains all data needed for a single grid cell's road tile.

```csharp
[System.Serializable]
public struct RoadTileData
{
    public RoadTileType Type;
    public int Rotation;  // 0, 90, 180, 270 degrees (clockwise)
    public Vector2Int GridPosition;  // Position in grid array [x, y]
    public bool IsMainPath;  // True if part of guaranteed start-to-exit path

    // Connection flags (after rotation applied)
    // These indicate which directions this tile connects to neighbors
    public bool ConnectsNorth;
    public bool ConnectsSouth;
    public bool ConnectsEast;
    public bool ConnectsWest;

    // Reference to instantiated GameObject (set during prefab placement)
    public GameObject Instance;
}
```

**Connection Details:**
- Connections represent roads extending from the center of this tile to adjacent tiles
- Connection points are at the midpoint of each cell edge
- For a tile at grid position (x, y) with tileSize:
  - North connection point: tile center + (0, tileSize/2, 0)
  - South connection point: tile center + (0, -tileSize/2, 0)
  - East connection point: tile center + (tileSize/2, 0, 0)
  - West connection point: tile center + (-tileSize/2, 0, 0)

### GridEdge (enum)
Defines the four edges of the grid where start/exit can be placed.

```csharp
public enum GridEdge
{
    North,  // Top edge (y = gridHeight - 1)
    South,  // Bottom edge (y = 0)
    East,   // Right edge (x = gridWidth - 1)
    West    // Left edge (x = 0)
}
```

### Direction (enum)
Helper enum for navigating the grid.

```csharp
public enum Direction
{
    North,
    South,
    East,
    West
}
```

## Configuration System

### RoadGenerationConfig (class)
Serializable configuration for all generation parameters.

```csharp
[System.Serializable]
public class RoadGenerationConfig
{
    [Header("Grid Settings")]
    [Tooltip("Width of the grid in tiles")]
    public int gridWidth = 20;

    [Tooltip("Height of the grid in tiles")]
    public int gridHeight = 20;

    [Tooltip("Size of each tile in Unity world units")]
    public float tileSize = 10f;

    [Header("Path Settings")]
    [Tooltip("Which edge the start tile spawns on")]
    public GridEdge startEdge = GridEdge.North;

    [Tooltip("Which edge the exit tile spawns on (should be opposite of start)")]
    public GridEdge exitEdge = GridEdge.South;

    [Range(0f, 1f)]
    [Tooltip("How winding the main path is. 0=direct path, 1=very winding")]
    public float pathWindingness = 0.3f;

    [Tooltip("Minimum length of main path in tiles (excluding start/exit)")]
    public int minPathLength = 5;

    [Header("Branch Settings")]
    [Range(0f, 1f)]
    [Tooltip("Probability of spawning a branch at each main path tile")]
    public float branchProbability = 0.4f;

    [Tooltip("Maximum length of a branch in tiles")]
    public int maxBranchLength = 5;

    [Tooltip("Minimum length of a branch in tiles")]
    public int minBranchLength = 1;

    [Tooltip("Maximum number of branches to generate")]
    public int maxBranches = 10;

    [Header("Density Settings")]
    [Range(0f, 1f)]
    [Tooltip("Target percentage of grid filled with roads (0-1)")]
    public float roadDensity = 0.5f;

    [Header("Prefab References")]
    [Tooltip("Prefab for straight road segment")]
    public GameObject straightPrefab;

    [Tooltip("Prefab for corner road segment")]
    public GameObject cornerPrefab;

    [Tooltip("Prefab for T-intersection")]
    public GameObject tIntersectionPrefab;

    [Tooltip("Prefab for 4-way intersection")]
    public GameObject fourWayPrefab;

    [Tooltip("Prefab for roundabout")]
    public GameObject roundaboutPrefab;

    [Tooltip("Prefab for start tile")]
    public GameObject startPrefab;

    [Tooltip("Prefab for exit tile")]
    public GameObject exitPrefab;

    [Header("Generation Settings")]
    [Tooltip("Random seed for generation (0 = random seed)")]
    public int randomSeed = 0;

    [Tooltip("Use roundabouts instead of 4-way intersections (random chance)")]
    [Range(0f, 1f)]
    public float roundaboutProbability = 0.3f;
}
```

**Recommended Settings by Level:**
- **Early Game (Tutorial/Level 1-3)**
  - Grid: 10x10
  - Density: 0.3-0.4
  - Branch probability: 0.2
  - Max branches: 3-5
  - Windingness: 0.2

- **Mid Game (Level 4-10)**
  - Grid: 25x25
  - Density: 0.4-0.5
  - Branch probability: 0.4
  - Max branches: 8-12
  - Windingness: 0.3

- **Late Game (Level 11+)**
  - Grid: 50x50
  - Density: 0.5-0.6
  - Branch probability: 0.5
  - Max branches: 15-20
  - Windingness: 0.4

## Main Class: ProceduralRoads

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace Roads
{
    public class ProceduralRoads : MonoBehaviour
    {
        [SerializeField] private RoadGenerationConfig config;

        // Grid storage
        private RoadTileData[,] grid;  // 2D array [x, y]
        private List<Vector2Int> mainPath;  // List of positions forming main path
        private List<Vector2Int> branches;  // List of all branch positions
        private System.Random rng;  // Random number generator
        private GameObject roadContainer;  // Parent object for all road instances

        // Public API
        /// <summary>
        /// Generate the entire road network based on current config
        /// </summary>
        public void GenerateRoads()
        {
            InitializeGrid();
            GenerateMainPath();
            GenerateBranches();
            FillRoadTiles();
            PlaceRoadPrefabs();
        }

        /// <summary>
        /// Get the road tile data at a specific grid position
        /// </summary>
        public RoadTileData GetTileAt(int x, int y)
        {
            if (!IsValidPosition(x, y))
                return default;
            return grid[x, y];
        }

        /// <summary>
        /// Convert grid coordinates to world position (center of tile)
        /// </summary>
        public Vector3 GridToWorldPosition(int x, int y)
        {
            float worldX = (x - config.gridWidth / 2f) * config.tileSize;
            float worldZ = (y - config.gridHeight / 2f) * config.tileSize;
            return new Vector3(worldX, 0, worldZ);
        }

        /// <summary>
        /// Convert world position to grid coordinates
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt(worldPos.x / config.tileSize + config.gridWidth / 2f);
            int y = Mathf.RoundToInt(worldPos.z / config.tileSize + config.gridHeight / 2f);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Clear all generated roads and reset grid
        /// </summary>
        public void ClearRoads()
        {
            if (roadContainer != null)
            {
                DestroyImmediate(roadContainer);
            }
            grid = null;
            mainPath = null;
            branches = null;
        }

        /// <summary>
        /// Get all tiles that are part of the main path
        /// </summary>
        public List<Vector2Int> GetMainPath()
        {
            return new List<Vector2Int>(mainPath);
        }

        // Generation Steps (Private Implementation)

        /// <summary>
        /// Phase 1: Initialize the grid and random number generator
        /// </summary>
        private void InitializeGrid()
        {
            // Create grid array
            grid = new RoadTileData[config.gridWidth, config.gridHeight];

            // Initialize all cells as empty
            for (int x = 0; x < config.gridWidth; x++)
            {
                for (int y = 0; y < config.gridHeight; y++)
                {
                    grid[x, y] = new RoadTileData
                    {
                        Type = RoadTileType.None,
                        GridPosition = new Vector2Int(x, y),
                        Rotation = 0,
                        IsMainPath = false
                    };
                }
            }

            // Initialize random number generator
            if (config.randomSeed == 0)
            {
                rng = new System.Random();
            }
            else
            {
                rng = new System.Random(config.randomSeed);
            }

            mainPath = new List<Vector2Int>();
            branches = new List<Vector2Int>();
        }

        /// <summary>
        /// Phase 2: Generate the main path from start to exit
        /// Uses a biased random walk that tends toward the exit
        /// </summary>
        private void GenerateMainPath()
        {
            // 1. Choose random start position on start edge
            Vector2Int startPos = GetRandomEdgePosition(config.startEdge);

            // 2. Choose random exit position on exit edge
            Vector2Int exitPos = GetRandomEdgePosition(config.exitEdge);

            // 3. Generate path from start to exit
            Vector2Int currentPos = startPos;
            mainPath.Add(currentPos);

            // Mark start position in grid
            grid[currentPos.x, currentPos.y].Type = RoadTileType.Start;
            grid[currentPos.x, currentPos.y].IsMainPath = true;

            // Walk toward exit until we reach it or get close enough
            int maxAttempts = config.gridWidth * config.gridHeight * 2; // Safety limit
            int attempts = 0;

            while (currentPos != exitPos && attempts < maxAttempts)
            {
                attempts++;

                // Get valid neighbors (not visited, within bounds)
                List<Vector2Int> validNeighbors = GetValidNeighborsForPath(currentPos, exitPos);

                if (validNeighbors.Count == 0)
                {
                    // Dead end - backtrack
                    if (mainPath.Count > 1)
                    {
                        mainPath.RemoveAt(mainPath.Count - 1);
                        currentPos = mainPath[mainPath.Count - 1];
                    }
                    else
                    {
                        // Failed to generate path - restart
                        InitializeGrid();
                        GenerateMainPath();
                        return;
                    }
                    continue;
                }

                // Choose next position (biased toward exit based on windingness)
                Vector2Int nextPos;
                if (rng.NextDouble() > config.pathWindingness)
                {
                    // Move toward exit (greedy)
                    nextPos = GetClosestToExit(validNeighbors, exitPos);
                }
                else
                {
                    // Move randomly
                    nextPos = validNeighbors[rng.Next(validNeighbors.Count)];
                }

                // Add to path
                currentPos = nextPos;
                mainPath.Add(currentPos);

                // Mark in grid (don't set type yet, will determine after connections known)
                grid[currentPos.x, currentPos.y].IsMainPath = true;
            }

            // Mark exit position
            grid[exitPos.x, exitPos.y].Type = RoadTileType.Exit;
            grid[exitPos.x, exitPos.y].IsMainPath = true;

            // Ensure path meets minimum length
            if (mainPath.Count < config.minPathLength)
            {
                // Regenerate with different seed or adjust parameters
                Debug.LogWarning($"Main path too short ({mainPath.Count} < {config.minPathLength}). Regenerating...");
                InitializeGrid();
                GenerateMainPath();
            }
        }

        /// <summary>
        /// Phase 3: Generate branches off the main path
        /// </summary>
        private void GenerateBranches()
        {
            int branchCount = 0;
            int currentRoadCount = mainPath.Count;
            int targetRoadCount = Mathf.RoundToInt(config.gridWidth * config.gridHeight * config.roadDensity);

            // Try to add branches at each main path tile (excluding start/exit)
            for (int i = 1; i < mainPath.Count - 1 && branchCount < config.maxBranches; i++)
            {
                // Check if we've hit density target
                if (currentRoadCount >= targetRoadCount)
                    break;

                // Roll for branch probability
                if (rng.NextDouble() > config.branchProbability)
                    continue;

                // Generate a branch from this position
                Vector2Int branchStart = mainPath[i];
                List<Direction> availableDirections = GetAvailableDirections(branchStart);

                if (availableDirections.Count == 0)
                    continue;

                // Choose random direction for branch
                Direction branchDir = availableDirections[rng.Next(availableDirections.Count)];

                // Generate branch
                int branchLength = rng.Next(config.minBranchLength, config.maxBranchLength + 1);
                List<Vector2Int> branch = GenerateBranch(branchStart, branchDir, branchLength);

                if (branch.Count > 0)
                {
                    branches.AddRange(branch);
                    currentRoadCount += branch.Count;
                    branchCount++;
                }
            }

            // Mark all branch positions in grid
            foreach (Vector2Int pos in branches)
            {
                grid[pos.x, pos.y].IsMainPath = false; // Branches are not main path
            }
        }

        /// <summary>
        /// Generate a single branch from a starting position
        /// </summary>
        private List<Vector2Int> GenerateBranch(Vector2Int start, Direction initialDir, int maxLength)
        {
            List<Vector2Int> branch = new List<Vector2Int>();
            Vector2Int currentPos = GetNeighborInDirection(start, initialDir);

            // Check if starting position is valid
            if (!IsValidPosition(currentPos.x, currentPos.y) ||
                grid[currentPos.x, currentPos.y].Type != RoadTileType.None)
            {
                return branch;
            }

            // Walk in random directions until max length or dead end
            for (int i = 0; i < maxLength; i++)
            {
                // Add current position to branch
                branch.Add(currentPos);

                // Get valid neighbors for continuing branch
                List<Direction> validDirs = GetAvailableDirections(currentPos);

                if (validDirs.Count == 0)
                {
                    // Dead end - branch complete
                    break;
                }

                // Choose random direction
                Direction nextDir = validDirs[rng.Next(validDirs.Count)];
                currentPos = GetNeighborInDirection(currentPos, nextDir);
            }

            return branch;
        }

        /// <summary>
        /// Phase 4: Determine the road tile type for each occupied cell
        /// Based on neighboring connections
        /// </summary>
        private void FillRoadTiles()
        {
            for (int x = 0; x < config.gridWidth; x++)
            {
                for (int y = 0; y < config.gridHeight; y++)
                {
                    // Skip empty tiles and already-set start/exit tiles
                    if (grid[x, y].Type == RoadTileType.None ||
                        grid[x, y].Type == RoadTileType.Start ||
                        grid[x, y].Type == RoadTileType.Exit)
                    {
                        continue;
                    }

                    Vector2Int pos = new Vector2Int(x, y);

                    // Check which directions have road neighbors
                    bool north = HasRoadNeighbor(pos, Direction.North);
                    bool south = HasRoadNeighbor(pos, Direction.South);
                    bool east = HasRoadNeighbor(pos, Direction.East);
                    bool west = HasRoadNeighbor(pos, Direction.West);

                    int connectionCount = (north ? 1 : 0) + (south ? 1 : 0) +
                                         (east ? 1 : 0) + (west ? 1 : 0);

                    // Determine tile type and rotation based on connections
                    RoadTileType tileType;
                    int rotation = 0;

                    switch (connectionCount)
                    {
                        case 1:
                            // Dead end (shouldn't happen on main path except start/exit)
                            tileType = RoadTileType.Straight;
                            rotation = GetDeadEndRotation(north, south, east, west);
                            break;

                        case 2:
                            if ((north && south) || (east && west))
                            {
                                // Straight road
                                tileType = RoadTileType.Straight;
                                rotation = (north && south) ? 0 : 90;
                            }
                            else
                            {
                                // Corner
                                tileType = RoadTileType.Corner;
                                rotation = GetCornerRotation(north, south, east, west);
                            }
                            break;

                        case 3:
                            // T-intersection
                            tileType = RoadTileType.TIntersection;
                            rotation = GetTIntersectionRotation(north, south, east, west);
                            break;

                        case 4:
                            // 4-way intersection or roundabout
                            if (rng.NextDouble() < config.roundaboutProbability)
                            {
                                tileType = RoadTileType.Roundabout;
                            }
                            else
                            {
                                tileType = RoadTileType.FourWay;
                            }
                            rotation = 0; // 4-way is symmetrical
                            break;

                        default:
                            // Shouldn't happen
                            tileType = RoadTileType.None;
                            break;
                    }

                    // Update grid
                    grid[x, y].Type = tileType;
                    grid[x, y].Rotation = rotation;
                    grid[x, y].ConnectsNorth = north;
                    grid[x, y].ConnectsSouth = south;
                    grid[x, y].ConnectsEast = east;
                    grid[x, y].ConnectsWest = west;
                }
            }
        }

        /// <summary>
        /// Phase 5: Instantiate prefabs for all road tiles
        /// </summary>
        private void PlaceRoadPrefabs()
        {
            // Create container for all road objects
            if (roadContainer != null)
            {
                DestroyImmediate(roadContainer);
            }
            roadContainer = new GameObject("Road Network");
            roadContainer.transform.SetParent(transform);

            // Instantiate each tile
            for (int x = 0; x < config.gridWidth; x++)
            {
                for (int y = 0; y < config.gridHeight; y++)
                {
                    RoadTileData tile = grid[x, y];

                    if (tile.Type == RoadTileType.None)
                        continue;

                    // Get appropriate prefab
                    GameObject prefab = GetPrefabForTileType(tile.Type);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"No prefab assigned for {tile.Type}");
                        continue;
                    }

                    // Calculate world position (center of tile)
                    Vector3 worldPos = GridToWorldPosition(x, y);

                    // Instantiate and configure
                    GameObject instance = Instantiate(prefab, worldPos, Quaternion.Euler(0, tile.Rotation, 0));
                    instance.transform.SetParent(roadContainer.transform);
                    instance.name = $"{tile.Type}_{x}_{y}";

                    // Store reference
                    grid[x, y].Instance = instance;
                }
            }
        }

        // Helper Methods

        private Vector2Int GetRandomEdgePosition(GridEdge edge)
        {
            switch (edge)
            {
                case GridEdge.North:
                    return new Vector2Int(rng.Next(0, config.gridWidth), config.gridHeight - 1);
                case GridEdge.South:
                    return new Vector2Int(rng.Next(0, config.gridWidth), 0);
                case GridEdge.East:
                    return new Vector2Int(config.gridWidth - 1, rng.Next(0, config.gridHeight));
                case GridEdge.West:
                    return new Vector2Int(0, rng.Next(0, config.gridHeight));
                default:
                    return Vector2Int.zero;
            }
        }

        private bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < config.gridWidth && y >= 0 && y < config.gridHeight;
        }

        private List<Vector2Int> GetValidNeighborsForPath(Vector2Int pos, Vector2Int target)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();
            Vector2Int[] directions = {
                new Vector2Int(0, 1),   // North
                new Vector2Int(0, -1),  // South
                new Vector2Int(1, 0),   // East
                new Vector2Int(-1, 0)   // West
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = pos + dir;

                // Check if within bounds
                if (!IsValidPosition(neighbor.x, neighbor.y))
                    continue;

                // Check if already occupied (except if it's the target)
                if (neighbor != target && grid[neighbor.x, neighbor.y].Type != RoadTileType.None)
                    continue;

                neighbors.Add(neighbor);
            }

            return neighbors;
        }

        private Vector2Int GetClosestToExit(List<Vector2Int> positions, Vector2Int exit)
        {
            Vector2Int closest = positions[0];
            float minDist = Vector2Int.Distance(closest, exit);

            foreach (Vector2Int pos in positions)
            {
                float dist = Vector2Int.Distance(pos, exit);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = pos;
                }
            }

            return closest;
        }

        private List<Direction> GetAvailableDirections(Vector2Int pos)
        {
            List<Direction> available = new List<Direction>();

            if (IsValidPosition(pos.x, pos.y + 1) && grid[pos.x, pos.y + 1].Type == RoadTileType.None)
                available.Add(Direction.North);

            if (IsValidPosition(pos.x, pos.y - 1) && grid[pos.x, pos.y - 1].Type == RoadTileType.None)
                available.Add(Direction.South);

            if (IsValidPosition(pos.x + 1, pos.y) && grid[pos.x + 1, pos.y].Type == RoadTileType.None)
                available.Add(Direction.East);

            if (IsValidPosition(pos.x - 1, pos.y) && grid[pos.x - 1, pos.y].Type == RoadTileType.None)
                available.Add(Direction.West);

            return available;
        }

        private Vector2Int GetNeighborInDirection(Vector2Int pos, Direction dir)
        {
            switch (dir)
            {
                case Direction.North: return pos + new Vector2Int(0, 1);
                case Direction.South: return pos + new Vector2Int(0, -1);
                case Direction.East: return pos + new Vector2Int(1, 0);
                case Direction.West: return pos + new Vector2Int(-1, 0);
                default: return pos;
            }
        }

        private bool HasRoadNeighbor(Vector2Int pos, Direction dir)
        {
            Vector2Int neighbor = GetNeighborInDirection(pos, dir);

            if (!IsValidPosition(neighbor.x, neighbor.y))
                return false;

            return grid[neighbor.x, neighbor.y].Type != RoadTileType.None;
        }

        private int GetDeadEndRotation(bool north, bool south, bool east, bool west)
        {
            if (north) return 0;
            if (east) return 90;
            if (south) return 180;
            if (west) return 270;
            return 0;
        }

        private int GetCornerRotation(bool north, bool south, bool east, bool west)
        {
            // Corner rotations (assuming base prefab connects North and East)
            if (north && east) return 0;
            if (east && south) return 90;
            if (south && west) return 180;
            if (west && north) return 270;
            return 0;
        }

        private int GetTIntersectionRotation(bool north, bool south, bool east, bool west)
        {
            // T-intersection rotations (assuming base prefab has opening to south)
            if (!north) return 0;   // Opening south
            if (!east) return 90;   // Opening west
            if (!south) return 180; // Opening north
            if (!west) return 270;  // Opening east
            return 0;
        }

        private GameObject GetPrefabForTileType(RoadTileType type)
        {
            switch (type)
            {
                case RoadTileType.Straight: return config.straightPrefab;
                case RoadTileType.Corner: return config.cornerPrefab;
                case RoadTileType.TIntersection: return config.tIntersectionPrefab;
                case RoadTileType.FourWay: return config.fourWayPrefab;
                case RoadTileType.Roundabout: return config.roundaboutPrefab;
                case RoadTileType.Start: return config.startPrefab;
                case RoadTileType.Exit: return config.exitPrefab;
                default: return null;
            }
        }
    }
}
```

## Implementation Steps

### Step 1: Create Enums
1. Create `RoadTileType` enum
2. Create `GridEdge` enum
3. Create `Direction` enum

### Step 2: Create Data Structures
1. Create `RoadTileData` struct with all required fields
2. Ensure `[System.Serializable]` attribute for inspector visibility

### Step 3: Create Configuration Class
1. Create `RoadGenerationConfig` class
2. Add all configuration fields with appropriate attributes
3. Add tooltip descriptions for designer clarity
4. Add prefab reference fields

### Step 4: Implement ProceduralRoads Core
1. Add serialized config field
2. Add private grid and path storage fields
3. Implement `GenerateRoads()` main entry point

### Step 5: Implement Generation Phases
1. **InitializeGrid()**: Create grid array, initialize RNG
2. **GenerateMainPath()**: Implement biased random walk algorithm
3. **GenerateBranches()**: Add branches with probability checks
4. **FillRoadTiles()**: Analyze connections and assign tile types
5. **PlaceRoadPrefabs()**: Instantiate Unity GameObjects

### Step 6: Implement Helper Methods
1. Position conversion methods (grid ↔ world)
2. Validation methods (IsValidPosition, HasRoadNeighbor)
3. Neighbor finding methods
4. Rotation calculation methods
5. Prefab lookup method

### Step 7: Testing
1. Test with small grid (5x5) first
2. Verify start/exit placement on opposite edges
3. Verify main path always connects start to exit
4. Test branches generate correctly
5. Test various configuration parameters
6. Test with different random seeds
7. Test prefab instantiation and rotation

### Step 8: Create Unity Prefabs
1. Model straight road segment
2. Model corner road segment (90° turn)
3. Model T-intersection
4. Model 4-way intersection
5. Model roundabout
6. Model start tile (special marker/decoration)
7. Model exit tile (special marker/decoration)
8. Ensure all prefabs are same size (tileSize × tileSize)
9. Ensure connections align at midpoint of edges

### Step 9: Create Editor Tools (Optional)
1. Custom inspector for ProceduralRoads
2. "Generate" button in inspector
3. "Clear" button in inspector
4. Gizmos to visualize grid and connections in scene view

## Algorithm Details

### Main Path Generation (Biased Random Walk)

```
1. Initialize:
   - Choose random position on startEdge
   - Choose random position on exitEdge
   - Set current position = start position
   - Add start to mainPath list

2. Loop until current == exit:
   a. Get valid neighbors (not visited, within bounds)

   b. If no valid neighbors:
      - Backtrack by removing last path position
      - If can't backtrack, restart entire generation

   c. Choose next position:
      - Roll random number [0, 1]
      - If roll > pathWindingness:
         * Choose neighbor closest to exit (greedy)
      - Else:
         * Choose random neighbor

   d. Move to next position:
      - Set current = next
      - Add to mainPath
      - Mark as IsMainPath = true in grid

3. Verify path length >= minPathLength
   - If too short, regenerate
```

### Branch Generation

```
For each tile in mainPath (excluding start/exit):
   1. Roll random [0, 1]
   2. If roll <= branchProbability:
      a. Get available directions (no existing roads)
      b. If no available directions, skip
      c. Choose random direction
      d. Generate branch:
         - Pick random length [minBranchLength, maxBranchLength]
         - Walk in available directions until length reached or blocked
         - Add all positions to branches list
      e. Check if road density target reached
      f. Check if max branches reached
```

### Tile Type Determination

```
For each grid position with a road:
   1. Check neighbors in all 4 directions (N, S, E, W)
   2. Count connections (neighbor has road)
   3. Assign type based on connection pattern:

      1 connection:
         - Dead end (or Start/Exit which are pre-set)

      2 connections:
         - If opposite (N-S or E-W): Straight
         - If adjacent: Corner

      3 connections:
         - T-intersection

      4 connections:
         - Roll for roundabout probability
         - If success: Roundabout
         - Else: FourWay

   4. Calculate rotation based on connection directions
```

## Connection Point Coordinate System

Each tile occupies a grid cell and has its center at `GridToWorldPosition(x, y)`.

**World Space Calculations:**
```
tileCenter = GridToWorldPosition(x, y)

North connection = tileCenter + Vector3(0, 0, tileSize/2)
South connection = tileCenter + Vector3(0, 0, -tileSize/2)
East connection = tileCenter + Vector3(tileSize/2, 0, 0)
West connection = tileCenter + Vector3(-tileSize/2, 0, 0)
```

**Grid Space:**
- North neighbor: (x, y+1)
- South neighbor: (x, y-1)
- East neighbor: (x+1, y)
- West neighbor: (x-1, y)

When creating road prefabs, ensure connection points (where road meets edge) are at distance `tileSize/2` from center.

## Rotation System

All rotations are in degrees, clockwise when viewed from above (Y-up coordinate system).

**Base Orientations (0° rotation):**
- **Straight**: Connects North-South
- **Corner**: Connects North-East
- **T-Intersection**: Opening to South (connects North, East, West)
- **Four-Way/Roundabout**: Connects all directions (rotation doesn't matter)
- **Start/Exit**: Opens to direction of first path segment

**Rotation Values:**
- 0° = no rotation
- 90° = rotated 90° clockwise
- 180° = rotated 180°
- 270° = rotated 270° clockwise (or 90° counter-clockwise)

## Edge Cases and Error Handling

### Failed Path Generation
- **Problem**: Random walk gets stuck before reaching exit
- **Solution**: Backtracking algorithm; if fully stuck, regenerate with new seed

### Path Too Short
- **Problem**: Direct path doesn't meet minPathLength
- **Solution**: Increase pathWindingness or regenerate

### Insufficient Branches
- **Problem**: Can't reach target roadDensity
- **Solution**: Not an error; just log warning and accept lower density

### Invalid Configuration
- **Problem**: startEdge and exitEdge are the same or adjacent
- **Solution**: Validate in config; warn user; automatically set to opposite

### Prefab Missing
- **Problem**: No prefab assigned for a tile type
- **Solution**: Log warning, skip instantiation, leave grid data intact

## Future Extensions

### Phase 1 (Current Implementation)
- Basic road generation
- Single tile size
- Simple prefab placement
- Fixed tile types

### Phase 2 (Future)
- Multi-lane roads (varying widths)
- Elevation changes (hills, bridges)
- Buildings/props on empty tiles
- Decorative elements (street lights, signs)

### Phase 3 (Advanced)
- Traffic simulation
- Pedestrian paths
- Vehicle spawning system
- Dynamic obstacles

### Phase 4 (Polish)
- Biome-specific road styles
- Weather effects on roads
- Road damage/wear system
- Animated elements (traffic lights)

## Performance Considerations

### Grid Size Impact
- **10x10**: Negligible (100 cells)
- **25x25**: Fast (625 cells)
- **50x50**: Acceptable (2500 cells)
- **100x100**: May need optimization (10000 cells)

### Optimization Strategies
1. **Object Pooling**: Reuse prefab instances instead of Destroy/Instantiate
2. **LOD System**: Use lower-detail models for distant roads
3. **Chunking**: Split large grids into chunks for culling
4. **Async Generation**: Generate in background thread (grid logic only)
5. **Static Batching**: Mark road objects as static for draw call batching

### Memory Footprint
- Grid array: `sizeof(RoadTileData) × width × height`
- For 50x50 with ~100 bytes per tile: ~250KB (negligible)
- Main cost is instantiated GameObjects (prefabs)

## Testing Checklist

- [ ] Grid initializes with correct dimensions
- [ ] Start and exit placed on opposite edges
- [ ] Main path always connects start to exit
- [ ] Path respects minPathLength
- [ ] Branches generate at correct probability
- [ ] Dead-ends only appear at branch endings
- [ ] No isolated road segments (all connected)
- [ ] Tile types correctly determined from connections
- [ ] Rotations correctly applied (visual verification)
- [ ] Prefabs instantiate at correct positions
- [ ] World/grid coordinate conversion accurate
- [ ] Random seed produces consistent results
- [ ] Edge cases handled (stuck paths, missing prefabs)
- [ ] Performance acceptable on target grid sizes
- [ ] Memory usage acceptable

## Conclusion

This design provides a complete, configurable procedural road generation system suitable for a Unity game with progressive difficulty levels. The graph-based algorithm with biased random walk ensures a guaranteed path while allowing organic branching and dead-ends. The modular structure allows easy extension for future features like buildings, traffic, and advanced road types.

Key strengths:
- ✅ Configurable for different level sizes
- ✅ Guaranteed start-to-exit connectivity
- ✅ Controllable road density and branching
- ✅ Unity-friendly with prefab support
- ✅ Grid-based for easy spatial queries
- ✅ Extensible architecture

Next steps after implementation:
1. Create road prefabs with consistent sizing
2. Test generation with various config parameters
3. Profile performance on large grids
4. Add visual debugging tools (gizmos)
5. Integrate with game systems (vehicle spawning, objectives)
