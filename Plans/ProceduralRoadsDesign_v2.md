# Procedural Road System - Architecture Design v2

## Overview
This document outlines a clean, testable architecture for procedural road generation following SOLID principles, with clear separation between functional code and Unity integration.

## Architecture Principles

1. **Early Exit Pattern** - Use guard clauses to reduce nesting
2. **Interface-Based Design** - Abstract core functionality for testability
3. **Pure Functions** - Separate data transformation from side effects
4. **Separation of Concerns** - Functional logic vs Unity glue code
5. **Extension Methods** - Add behavior to data structures without inheritance
6. **Dependency Injection** - Use Microsoft.Extensions.DependencyInjection for service registration and resolution

## System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                         DI                               │
│                  (Service Container)                     │
│           Microsoft.Extensions.DependencyInjection       │
└───────────────┬─────────────────────────────────────────┘
                │
                ├──► IPathGenerator → BiasedRandomWalkGenerator
                ├──► IBranchGenerator → RandomBranchGenerator
                ├──► IRoadSpawner → UnityRoadSpawner
                │
                ▼
┌─────────────────────────────────────────────────────────┐
│                    ProceduralRoads                      │
│                   (MonoBehaviour)                       │
│                    [Glue Code]                          │
└───────────────┬─────────────────────────────────────────┘
                │
                ├──► RoadGenerationConfig (Data)
                │
                ├──► IGrid<RoadTileData> (Interface)
                │    └──► Grid2D<RoadTileData> (Implementation)
                │
                └──► RoadTileUtility (Static Pure Functions)
```

## Dependency Injection Setup

### DI.cs - Service Container

Static class for registering and resolving services using Microsoft.Extensions.DependencyInjection.

**NuGet Package Required**: `Microsoft.Extensions.DependencyInjection` version 9.0.0

```csharp
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Roads
{
    public static class DI
    {
        private static ServiceProvider _serviceProvider;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Init()
        {
            var serviceCollection = new ServiceCollection();

            // Register road generation services as Transient (new instance each time)
            serviceCollection.AddTransient<IPathGenerator, BiasedRandomWalkGenerator>();
            serviceCollection.AddTransient<IBranchGenerator, RandomBranchGenerator>();

            // Register road spawner as Singleton (one instance for lifetime)
            serviceCollection.AddSingleton<IRoadSpawner, UnityRoadSpawner>();

            // Build service provider
            _serviceProvider = serviceCollection.BuildServiceProvider();

            Debug.Log("DI Container initialized");
        }

        public static T GetService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
```

### Service Lifetimes

**Transient**: New instance created every time the service is requested
- Use for: Generators, algorithms, stateless services
- Examples: `IPathGenerator`, `IBranchGenerator`

**Singleton**: Single instance shared throughout application lifetime
- Use for: Managers, spawners, services that maintain state
- Examples: `IRoadSpawner`

**Scoped**: Not used in this architecture (Unity doesn't have request scopes like web apps)

### Key Features

- **Auto-initialization**: `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]` runs before anything else
- **Simple API**: Just `DI.GetService<T>()` - throws exception if service not registered
- **No manual setup**: Container initializes automatically, no need for bootstrap scripts

## Core Interfaces

### IGrid<T>
Abstract grid storage and access patterns.

```csharp
public interface IGrid<T>
{
    int Width { get; }
    int Height { get; }

    T Get(int x, int y);
    void Set(int x, int y, T value);
    bool IsValidPosition(int x, int y);
    IEnumerable<Vector2Int> GetAllPositions();
    void Clear();
}
```

**Implementation: Grid2D<T>**
```csharp
public class Grid2D<T> : IGrid<T>
{
    private readonly T[,] _data;

    public int Width { get; }
    public int Height { get; }

    public Grid2D(int width, int height)
    {
        if (width <= 0) throw new ArgumentException("Width must be positive", nameof(width));
        if (height <= 0) throw new ArgumentException("Height must be positive", nameof(height));

        Width = width;
        Height = height;
        _data = new T[width, height];
    }

    public T Get(int x, int y)
    {
        if (!IsValidPosition(x, y))
            throw new IndexOutOfRangeException($"Position ({x}, {y}) is out of bounds");

        return _data[x, y];
    }

    public void Set(int x, int y, T value)
    {
        if (!IsValidPosition(x, y))
            throw new IndexOutOfRangeException($"Position ({x}, {y}) is out of bounds");

        _data[x, y] = value;
    }

    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public IEnumerable<Vector2Int> GetAllPositions()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                yield return new Vector2Int(x, y);
            }
        }
    }

    public void Clear()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _data[x, y] = default;
            }
        }
    }
}
```

### IPathGenerator
Abstract path generation algorithms.

```csharp
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
```

**Implementation: BiasedRandomWalkGenerator**
```csharp
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
```

### IBranchGenerator
Abstract branch generation logic.

```csharp
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
```

**Implementation: RandomBranchGenerator**
```csharp
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
        var current = GridUtility.GetNeighborInDirection(start, initialDirection);

        // Early exit: invalid starting position
        if (!grid.IsValidPosition(current.x, current.y)) return branch;
        if (grid.Get(current.x, current.y).Type != RoadTileType.None) return branch;

        for (int i = 0; i < maxLength; i++)
        {
            branch.Add(current);

            var availableDirections = GridUtility.GetAvailableDirections(grid, current);

            // Early exit: dead end reached
            if (availableDirections.Count == 0) break;

            var nextDirection = availableDirections[random.Next(availableDirections.Count)];
            current = GridUtility.GetNeighborInDirection(current, nextDirection);
        }

        return branch;
    }
}
```

### IRoadSpawner
Abstract prefab instantiation.

```csharp
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
```

**Implementation: UnityRoadSpawner**
```csharp
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
            RoadTileType.Start => config.startPrefab,
            RoadTileType.Exit => config.exitPrefab,
            _ => null
        };
    }
}
```

## Data Structures

### Enums

```csharp
public enum RoadTileType
{
    None,
    Straight,
    Corner,
    TIntersection,
    FourWay,
    Roundabout,
    Start,
    Exit
}

public enum GridEdge
{
    North,
    South,
    East,
    West
}

public enum Direction
{
    North,
    South,
    East,
    West
}
```

### RoadTileData (Struct)

Pure data structure with no behavior.

```csharp
[System.Serializable]
public struct RoadTileData
{
    public RoadTileType Type;
    public int Rotation;
    public Vector2Int GridPosition;
    public bool IsMainPath;

    public bool ConnectsNorth;
    public bool ConnectsSouth;
    public bool ConnectsEast;
    public bool ConnectsWest;

    public GameObject Instance;

    public static RoadTileData Empty(int x, int y)
    {
        return new RoadTileData
        {
            Type = RoadTileType.None,
            GridPosition = new Vector2Int(x, y),
            Rotation = 0,
            IsMainPath = false
        };
    }
}
```

### RoadGenerationConfig (Class)

```csharp
[System.Serializable]
public class RoadGenerationConfig
{
    [Header("Grid Settings")]
    public int gridWidth = 20;
    public int gridHeight = 20;
    public float tileSize = 10f;

    [Header("Path Settings")]
    public GridEdge startEdge = GridEdge.North;
    public GridEdge exitEdge = GridEdge.South;
    [Range(0f, 1f)]
    public float pathWindingness = 0.3f;
    public int minPathLength = 5;

    [Header("Branch Settings")]
    [Range(0f, 1f)]
    public float branchProbability = 0.4f;
    public int maxBranchLength = 5;
    public int minBranchLength = 1;
    public int maxBranches = 10;

    [Header("Density Settings")]
    [Range(0f, 1f)]
    public float roadDensity = 0.5f;

    [Header("Prefab References")]
    public GameObject straightPrefab;
    public GameObject cornerPrefab;
    public GameObject tIntersectionPrefab;
    public GameObject fourWayPrefab;
    public GameObject roundaboutPrefab;
    public GameObject startPrefab;
    public GameObject exitPrefab;

    [Header("Generation Settings")]
    public int randomSeed = 0;
    [Range(0f, 1f)]
    public float roundaboutProbability = 0.3f;

    public void Validate()
    {
        if (gridWidth <= 0) throw new InvalidOperationException("Grid width must be positive");
        if (gridHeight <= 0) throw new InvalidOperationException("Grid height must be positive");
        if (tileSize <= 0) throw new InvalidOperationException("Tile size must be positive");
        if (minPathLength < 2) throw new InvalidOperationException("Min path length must be at least 2");
        if (maxBranchLength < minBranchLength) throw new InvalidOperationException("Max branch length must be >= min branch length");
    }
}
```

## Pure Utility Functions

### GridUtility (Static Extension Methods)

```csharp
public static class GridUtility
{
    private static readonly Vector2Int[] Directions = {
        new Vector2Int(0, 1),   // North
        new Vector2Int(0, -1),  // South
        new Vector2Int(1, 0),   // East
        new Vector2Int(-1, 0)   // West
    };

    public static Vector3 GridToWorldPosition(int x, int y, RoadGenerationConfig config)
    {
        float worldX = (x - config.gridWidth / 2f) * config.tileSize;
        float worldZ = (y - config.gridHeight / 2f) * config.tileSize;
        return new Vector3(worldX, 0, worldZ);
    }

    public static Vector2Int WorldToGridPosition(Vector3 worldPos, RoadGenerationConfig config)
    {
        int x = Mathf.RoundToInt(worldPos.x / config.tileSize + config.gridWidth / 2f);
        int y = Mathf.RoundToInt(worldPos.z / config.tileSize + config.gridHeight / 2f);
        return new Vector2Int(x, y);
    }

    public static Vector2Int GetRandomEdgePosition(GridEdge edge, int gridWidth, int gridHeight, System.Random random)
    {
        return edge switch
        {
            GridEdge.North => new Vector2Int(random.Next(0, gridWidth), gridHeight - 1),
            GridEdge.South => new Vector2Int(random.Next(0, gridWidth), 0),
            GridEdge.East => new Vector2Int(gridWidth - 1, random.Next(0, gridHeight)),
            GridEdge.West => new Vector2Int(0, random.Next(0, gridHeight)),
            _ => Vector2Int.zero
        };
    }

    public static List<Direction> GetAvailableDirections(IGrid<RoadTileData> grid, Vector2Int position)
    {
        var available = new List<Direction>();

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            var neighbor = GetNeighborInDirection(position, dir);

            // Early exits
            if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;
            if (grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None) continue;

            available.Add(dir);
        }

        return available;
    }

    public static Vector2Int GetNeighborInDirection(Vector2Int position, Direction direction)
    {
        return direction switch
        {
            Direction.North => position + new Vector2Int(0, 1),
            Direction.South => position + new Vector2Int(0, -1),
            Direction.East => position + new Vector2Int(1, 0),
            Direction.West => position + new Vector2Int(-1, 0),
            _ => position
        };
    }

    public static bool HasRoadNeighbor(IGrid<RoadTileData> grid, Vector2Int position, Direction direction)
    {
        var neighbor = GetNeighborInDirection(position, direction);

        // Early exit: out of bounds
        if (!grid.IsValidPosition(neighbor.x, neighbor.y)) return false;

        return grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None;
    }

    public static List<Vector2Int> GetNeighbors(IGrid<RoadTileData> grid, Vector2Int position)
    {
        var neighbors = new List<Vector2Int>();

        foreach (var dir in Directions)
        {
            var neighbor = position + dir;

            // Early exit: invalid position
            if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;

            neighbors.Add(neighbor);
        }

        return neighbors;
    }
}
```

### RoadTileUtility (Static Extension Methods)

Pure functions for tile analysis and determination.

```csharp
public static class RoadTileUtility
{
    public static RoadTileData DetermineTileType(
        IGrid<RoadTileData> grid,
        Vector2Int position,
        float roundaboutProbability,
        System.Random random)
    {
        var tile = grid.Get(position.x, position.y);

        // Early exit: already set (Start/Exit)
        if (tile.Type == RoadTileType.Start || tile.Type == RoadTileType.Exit)
        {
            return tile;
        }

        // Early exit: empty tile
        if (tile.Type == RoadTileType.None)
        {
            return tile;
        }

        // Check connections
        bool north = GridUtility.HasRoadNeighbor(grid, position, Direction.North);
        bool south = GridUtility.HasRoadNeighbor(grid, position, Direction.South);
        bool east = GridUtility.HasRoadNeighbor(grid, position, Direction.East);
        bool west = GridUtility.HasRoadNeighbor(grid, position, Direction.West);

        int connectionCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);

        tile.ConnectsNorth = north;
        tile.ConnectsSouth = south;
        tile.ConnectsEast = east;
        tile.ConnectsWest = west;

        // Determine type and rotation based on connections
        switch (connectionCount)
        {
            case 1:
                tile.Type = RoadTileType.Straight;
                tile.Rotation = CalculateDeadEndRotation(north, south, east, west);
                break;

            case 2:
                if ((north && south) || (east && west))
                {
                    tile.Type = RoadTileType.Straight;
                    tile.Rotation = (north && south) ? 0 : 90;
                }
                else
                {
                    tile.Type = RoadTileType.Corner;
                    tile.Rotation = CalculateCornerRotation(north, south, east, west);
                }
                break;

            case 3:
                tile.Type = RoadTileType.TIntersection;
                tile.Rotation = CalculateTIntersectionRotation(north, south, east, west);
                break;

            case 4:
                tile.Type = (random.NextDouble() < roundaboutProbability)
                    ? RoadTileType.Roundabout
                    : RoadTileType.FourWay;
                tile.Rotation = 0;
                break;

            default:
                // Should not happen
                tile.Type = RoadTileType.None;
                break;
        }

        return tile;
    }

    private static int CalculateDeadEndRotation(bool north, bool south, bool east, bool west)
    {
        if (north) return 0;
        if (east) return 90;
        if (south) return 180;
        if (west) return 270;
        return 0;
    }

    private static int CalculateCornerRotation(bool north, bool south, bool east, bool west)
    {
        // Base prefab connects North and East
        if (north && east) return 0;
        if (east && south) return 90;
        if (south && west) return 180;
        if (west && north) return 270;
        return 0;
    }

    private static int CalculateTIntersectionRotation(bool north, bool south, bool east, bool west)
    {
        // Base prefab has opening to south (connects N, E, W)
        if (!north) return 0;   // Opening south
        if (!east) return 90;   // Opening west
        if (!south) return 180; // Opening north
        if (!west) return 270;  // Opening east
        return 0;
    }
}
```

### RoadTileDataExtensions

Extension methods for RoadTileData struct.

```csharp
public static class RoadTileDataExtensions
{
    public static bool IsEmpty(this RoadTileData tile)
    {
        return tile.Type == RoadTileType.None;
    }

    public static bool IsOccupied(this RoadTileData tile)
    {
        return tile.Type != RoadTileType.None;
    }

    public static bool IsIntersection(this RoadTileData tile)
    {
        return tile.Type == RoadTileType.TIntersection ||
               tile.Type == RoadTileType.FourWay ||
               tile.Type == RoadTileType.Roundabout;
    }

    public static bool IsSpecial(this RoadTileData tile)
    {
        return tile.Type == RoadTileType.Start || tile.Type == RoadTileType.Exit;
    }

    public static int GetConnectionCount(this RoadTileData tile)
    {
        int count = 0;
        if (tile.ConnectsNorth) count++;
        if (tile.ConnectsSouth) count++;
        if (tile.ConnectsEast) count++;
        if (tile.ConnectsWest) count++;
        return count;
    }

    public static Vector3 GetConnectionPoint(this RoadTileData tile, Direction direction, RoadGenerationConfig config)
    {
        var center = GridUtility.GridToWorldPosition(tile.GridPosition.x, tile.GridPosition.y, config);
        float halfTile = config.tileSize / 2f;

        return direction switch
        {
            Direction.North => center + new Vector3(0, 0, halfTile),
            Direction.South => center + new Vector3(0, 0, -halfTile),
            Direction.East => center + new Vector3(halfTile, 0, 0),
            Direction.West => center + new Vector3(-halfTile, 0, 0),
            _ => center
        };
    }
}
```

## Main System: ProceduralRoads (MonoBehaviour)

This is the **glue code** that connects all the functional pieces with Unity.

```csharp
using UnityEngine;
using System.Collections.Generic;

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

        // Public API

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

        // Private Implementation

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

                // Set start and exit in grid
                var startTile = _grid.Get(startPos.x, startPos.y);
                startTile.Type = RoadTileType.Start;
                startTile.IsMainPath = true;
                _grid.Set(startPos.x, startPos.y, startTile);

                var exitTile = _grid.Get(exitPos.x, exitPos.y);
                exitTile.Type = RoadTileType.Exit;
                exitTile.IsMainPath = true;
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

        // Unity callbacks

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
```

## Usage Examples

### Basic Usage

No bootstrap needed - DI initializes automatically via `[RuntimeInitializeOnLoadMethod]`.

```csharp
// In Unity Editor or at runtime
// DI container is automatically initialized via RuntimeInitializeOnLoadMethod
var roadSystem = GetComponent<ProceduralRoads>();
roadSystem.GenerateRoads();
```

### Using DI Directly

```csharp
// Get services from DI container
var pathGenerator = DI.GetService<IPathGenerator>();
var branchGenerator = DI.GetService<IBranchGenerator>();

// Use services
var grid = new Grid2D<RoadTileData>(20, 20);
var config = new RoadGenerationConfig();
var random = new System.Random();
var path = pathGenerator.GeneratePath(grid, start, end, config, random);
```

### Custom Path Generator with DI

```csharp
// Step 1: Create custom implementation
public class AStarPathGenerator : IPathGenerator
{
    public List<Vector2Int> GeneratePath(
        IGrid<RoadTileData> grid,
        Vector2Int start,
        Vector2Int end,
        RoadGenerationConfig config,
        System.Random random)
    {
        // Implement A* algorithm
        // ...
    }
}

// Step 2: Register in DI.Init() method
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
public static void Init()
{
    var serviceCollection = new ServiceCollection();

    // Replace default with A* implementation
    serviceCollection.AddTransient<IPathGenerator, AStarPathGenerator>();
    serviceCollection.AddTransient<IBranchGenerator, RandomBranchGenerator>();
    serviceCollection.AddSingleton<IRoadSpawner, UnityRoadSpawner>();

    _serviceProvider = serviceCollection.BuildServiceProvider();
}

// Step 3: Use normally (automatic injection)
var roadSystem = GetComponent<ProceduralRoads>();
roadSystem.GenerateRoads(); // Uses AStarPathGenerator
```

### Custom Generator at Runtime (Without DI)

```csharp
// Bypass DI for specific use case
var roadSystem = GetComponent<ProceduralRoads>();
var customGenerator = new AStarPathGenerator();
roadSystem.Initialize(pathGenerator: customGenerator);
roadSystem.GenerateRoads();
```

### Testing Without Unity or DI

```csharp
[Test]
public void TestPathGeneration()
{
    // Create services directly without DI
    var grid = new Grid2D<RoadTileData>(10, 10);
    var config = new RoadGenerationConfig { /* ... */ };
    var generator = new BiasedRandomWalkGenerator();
    var random = new System.Random(42);

    var start = new Vector2Int(0, 0);
    var end = new Vector2Int(9, 9);

    var path = generator.GeneratePath(grid, start, end, config, random);

    Assert.IsNotEmpty(path);
    Assert.AreEqual(start, path[0]);
    Assert.AreEqual(end, path[path.Count - 1]);
}
```

### Testing With DI

```csharp
[TestFixture]
public class RoadGenerationTests
{
    [SetUp]
    public void Setup()
    {
        // DI auto-initializes, or call DI.Init() manually in tests if needed
        // Note: In Unity Test Framework, RuntimeInitializeOnLoadMethod may not run
        // You may need to call DI.Init() manually
    }

    [Test]
    public void TestGenerateRoads()
    {
        var pathGenerator = DI.GetService<IPathGenerator>();
        Assert.IsNotNull(pathGenerator);
        Assert.IsInstanceOf<BiasedRandomWalkGenerator>(pathGenerator);
    }
}
```

**Note on Testing**: For unit tests that don't run in Unity's runtime, you may need to manually call `DI.Init()` since `[RuntimeInitializeOnLoadMethod]` won't execute. Alternatively, instantiate services directly without DI for pure unit tests.

## File Structure

```
Assets/
└── Scripts/
    ├── Core/
    │   └── DI.cs                        (Dependency Injection container)
    │
    └── Roads/
        ├── Data/
        │   ├── RoadTileType.cs          (enum)
        │   ├── GridEdge.cs              (enum)
        │   ├── Direction.cs             (enum)
        │   ├── RoadTileData.cs          (struct)
        │   └── RoadGenerationConfig.cs  (class)
        │
        ├── Interfaces/
        │   ├── IGrid.cs
        │   ├── IPathGenerator.cs
        │   ├── IBranchGenerator.cs
        │   └── IRoadSpawner.cs
        │
        ├── Implementations/
        │   ├── Grid2D.cs
        │   ├── BiasedRandomWalkGenerator.cs
        │   ├── RandomBranchGenerator.cs
        │   └── UnityRoadSpawner.cs
        │
        ├── Utilities/
        │   ├── GridUtility.cs
        │   ├── RoadTileUtility.cs
        │   └── RoadTileDataExtensions.cs
        │
        └── ProceduralRoads.cs           (MonoBehaviour - glue code)
```

### NuGet Dependencies

Add to your Unity project (via NuGetForUnity or manual .dll import):

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
```

Or add via Unity Package Manager with `com.unity.nuget.newtonsoft-json` as dependency.

## Testing Strategy

### Unit Tests (Pure Functions)
- `GridUtility` methods
- `RoadTileUtility` methods
- Extension methods
- Path generation algorithms

### Integration Tests
- `IPathGenerator` implementations with mock grids
- `IBranchGenerator` implementations
- Tile type determination with various connection patterns

### Unity Tests
- Full generation pipeline
- Prefab spawning
- Visual validation in play mode

## Key Benefits of This Architecture

1. **Testability**: Pure functions and interfaces can be unit tested without Unity
2. **Flexibility**: Easy to swap algorithms via DI registration (different path generators)
3. **Maintainability**: Clear separation between logic and Unity integration
4. **Readability**: Early exits reduce nesting and complexity
5. **Extensibility**: New tile types or algorithms can be added without modifying existing code
6. **Performance**: Pure functions can be optimized or parallelized independently
7. **Dependency Management**: DI container manages service lifetimes and dependencies
8. **Decoupling**: Systems don't need to know about concrete implementations
9. **Testing Flexibility**: Easy to inject mocks/stubs for testing via DI or Initialize parameters

## Implementation Checklist

### Phase 0: Setup
- [ ] Install Microsoft.Extensions.DependencyInjection (version 9.0.0)
- [ ] Create Core folder structure
- [ ] Create Roads folder structure

### Phase 1: Core Infrastructure
- [ ] Implement DI.cs (Dependency Injection container with auto-init)
- [ ] Create all enum types (RoadTileType, GridEdge, Direction)
- [ ] Create data structures (RoadTileData, RoadGenerationConfig)

### Phase 2: Interfaces & Pure Functions
- [ ] Implement IGrid interface and Grid2D
- [ ] Implement GridUtility (pure functions)
- [ ] Implement RoadTileUtility (pure functions)
- [ ] Implement RoadTileDataExtensions

### Phase 3: Service Implementations
- [ ] Implement IPathGenerator interface
- [ ] Implement BiasedRandomWalkGenerator
- [ ] Implement IBranchGenerator interface
- [ ] Implement RandomBranchGenerator
- [ ] Implement IRoadSpawner interface
- [ ] Implement UnityRoadSpawner

### Phase 4: Integration
- [ ] Register all services in DI.RegisterServices()
- [ ] Implement ProceduralRoads (glue code with DI)
- [ ] Test DI container initialization

### Phase 5: Testing
- [ ] Write unit tests for utilities (no Unity dependencies)
- [ ] Write integration tests for generators
- [ ] Write DI container tests
- [ ] Test service resolution

### Phase 6: Unity Integration
- [ ] Create road prefabs
- [ ] Test full pipeline in Unity
- [ ] Create custom inspector (optional)
- [ ] Test with different algorithms via DI

## Next Steps

1. Review and approve architecture
2. Begin implementation in order of dependencies (data → utilities → generators → spawner → glue)
3. Write tests alongside implementation
4. Create prefabs
5. Integrate with game systems
