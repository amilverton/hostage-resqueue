# Coding Patterns & Best Practices Guide

## Overview

This document outlines the coding patterns, conventions, and architectural decisions used in this codebase. Follow these patterns to maintain consistency, testability, and maintainability.

---

## Table of Contents

1. [Early Exit Pattern](#early-exit-pattern)
2. [Dependency Injection](#dependency-injection)
3. [Interface-Based Design](#interface-based-design)
4. [Pure Functions vs Systems](#pure-functions-vs-systems)
5. [Extension Methods](#extension-methods)
6. [Data Structures](#data-structures)
7. [Error Handling](#error-handling)
8. [Naming Conventions](#naming-conventions)
9. [Unity Integration](#unity-integration)
10. [Testing Patterns](#testing-patterns)

---

## Early Exit Pattern

### Principle
**Exit early from functions when conditions aren't met. Avoid deep nesting.**

### Why?
- Reduces cognitive load
- Makes error cases explicit
- Easier to read and debug
- Reduces indentation levels

### ✅ DO: Use Early Exits

```csharp
public List<Vector2Int> GeneratePath(
    IGrid<RoadTileData> grid,
    Vector2Int start,
    Vector2Int end,
    RoadGenerationConfig config,
    System.Random random)
{
    // Early exit: null checks
    if (grid == null) throw new ArgumentNullException(nameof(grid));
    if (config == null) throw new ArgumentNullException(nameof(config));
    if (random == null) throw new ArgumentNullException(nameof(random));

    // Early exit: invalid positions
    if (!grid.IsValidPosition(start.x, start.y)) return new List<Vector2Int>();
    if (!grid.IsValidPosition(end.x, end.y)) return new List<Vector2Int>();

    // Early exit: trivial case
    if (start == end) return new List<Vector2Int> { start };

    // Main logic here
    var path = new List<Vector2Int>();
    // ... actual generation code ...
    return path;
}
```

### ❌ DON'T: Nest Conditions

```csharp
// BAD: Nested conditions are hard to read
public List<Vector2Int> GeneratePath(...)
{
    if (grid != null)
    {
        if (config != null)
        {
            if (random != null)
            {
                if (grid.IsValidPosition(start.x, start.y))
                {
                    if (grid.IsValidPosition(end.x, end.y))
                    {
                        if (start != end)
                        {
                            // Main logic buried 6 levels deep
                            var path = new List<Vector2Int>();
                            // ...
                            return path;
                        }
                    }
                }
            }
        }
    }
    return new List<Vector2Int>();
}
```

### ✅ DO: Early Exit in Loops

```csharp
private List<Vector2Int> GenerateSingleBranch(...)
{
    var branch = new List<Vector2Int>();
    var visited = new HashSet<Vector2Int>();

    for (int i = 0; i < maxLength; i++)
    {
        // Early exit: already visited (prevents oscillation)
        if (visited.Contains(current)) break;

        branch.Add(current);
        visited.Add(current);

        var availableDirections = GetAvailableDirections(grid, current);

        // Early exit: dead end reached
        if (availableDirections.Count == 0) break;

        // Continue with main logic
        var nextDirection = availableDirections[random.Next(availableDirections.Count)];
        current = GridUtility.GetNeighborInDirection(current, nextDirection);
    }

    return branch;
}
```

### ✅ DO: Early Exit with Continue

```csharp
foreach (var district in districts)
{
    // Early exit: skip unknown types
    if (!_districtRoadGenerators.TryGetValue(district.Type, out var generator))
    {
        continue; // Skip this iteration
    }

    // Main logic for valid districts
    var districtRoads = generator.GenerateDistrictRoads(grid, district, config, random);
    MarkPathInGrid(grid, districtRoads, isMainPath: false);
}
```

---

## Dependency Injection

### Principle
**Use constructor injection for dependencies. Register services in DI container.**

### Why?
- Loose coupling
- Easy to test (inject mocks)
- Easy to swap implementations
- Clear dependencies

### ✅ DO: Constructor Injection

```csharp
public class HierarchicalRoadGenerator : IPathGenerator
{
    private readonly IPathGenerator _arteryGenerator;
    private readonly IDistrictGenerator _districtGenerator;
    private readonly Dictionary<DistrictType, IDistrictRoadGenerator> _districtRoadGenerators;

    // Constructor with DI
    public HierarchicalRoadGenerator(
        IPathGenerator arteryGenerator,
        IDistrictGenerator districtGenerator,
        Dictionary<DistrictType, IDistrictRoadGenerator> districtRoadGenerators)
    {
        _arteryGenerator = arteryGenerator ?? throw new ArgumentNullException(nameof(arteryGenerator));
        _districtGenerator = districtGenerator ?? throw new ArgumentNullException(nameof(districtGenerator));
        _districtRoadGenerators = districtRoadGenerators ?? throw new ArgumentNullException(nameof(districtRoadGenerators));
    }

    // Parameterless constructor for backward compatibility
    public HierarchicalRoadGenerator()
        : this(
            new MainArteryGenerator(),
            new VoronoiDistrictGenerator(),
            new Dictionary<DistrictType, IDistrictRoadGenerator>
            {
                { DistrictType.Downtown, new DowntownRoadGenerator() },
                { DistrictType.Suburban, new SuburbanRoadGenerator() }
            })
    {
    }
}
```

### ❌ DON'T: Hard-Code Dependencies

```csharp
// BAD: Cannot swap implementations or test in isolation
public class HierarchicalRoadGenerator : IPathGenerator
{
    public List<Vector2Int> GeneratePath(...)
    {
        var arteryGen = new MainArteryGenerator(); // Hard-coded!
        var districtGen = new VoronoiDistrictGenerator(); // Hard-coded!

        // ... rest of code
    }
}
```

### ✅ DO: Register Services in DI

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
public static void Init()
{
    var serviceCollection = new ServiceCollection();

    // Register all services
    serviceCollection.AddTransient<IPathGenerator, HierarchicalRoadGenerator>();
    serviceCollection.AddTransient<IDistrictGenerator, VoronoiDistrictGenerator>();
    serviceCollection.AddTransient<IBranchGenerator, RandomBranchGenerator>();
    serviceCollection.AddTransient<IRoadSpawner, UnityRoadSpawner>();

    _serviceProvider = serviceCollection.BuildServiceProvider();
}
```

### ✅ DO: Use Service Lifetimes Correctly

```csharp
// Transient: New instance every time (for stateless or non-shared stateful services)
serviceCollection.AddTransient<IPathGenerator, BiasedRandomWalkGenerator>();

// Singleton: Single shared instance (ONLY for truly global state)
// Be careful: can cause cross-contamination if service has mutable state
serviceCollection.AddSingleton<GlobalConfigManager>();

// In this codebase: Use Transient for everything to avoid state pollution
serviceCollection.AddTransient<IRoadSpawner, UnityRoadSpawner>(); // Has state, must be transient
```

### ✅ DO: Lazy Initialization for Edit Mode

```csharp
public static T GetService<T>()
{
    // Lazy initialization guard for edit-mode and unit tests
    if (_serviceProvider == null)
    {
        Init();
    }

    return _serviceProvider.GetRequiredService<T>();
}
```

---

## Interface-Based Design

### Principle
**Define interfaces for core abstractions. Program against interfaces, not implementations.**

### Why?
- Testability (inject mocks)
- Flexibility (swap implementations)
- Clear contracts
- Decoupling

### ✅ DO: Define Clear Interfaces

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

### ✅ DO: Use Interface for Flexibility

```csharp
public interface IDistrictRoadGenerator
{
    /// <summary>
    /// Generate roads within a specific district based on its type
    /// </summary>
    List<Vector2Int> GenerateDistrictRoads(
        IGrid<RoadTileData> grid,
        District district,
        RoadGenerationConfig config,
        System.Random random);
}

// Multiple implementations
public class DowntownRoadGenerator : IDistrictRoadGenerator { /* Dense grid */ }
public class SuburbanRoadGenerator : IDistrictRoadGenerator { /* Organic curves */ }
public class RuralRoadGenerator : IDistrictRoadGenerator { /* Sparse winding */ }
```

### ✅ DO: Generic Interfaces for Reusability

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

// Can be used with any data type
var roadGrid = new Grid2D<RoadTileData>(20, 20);
var heightGrid = new Grid2D<float>(50, 50);
```

### ❌ DON'T: Create Interfaces with Only One Implementation (Unless)

```csharp
// BAD: Interface with no abstraction value
public interface IConfigManager
{
    void LoadConfig();
}

public class ConfigManager : IConfigManager
{
    void LoadConfig() { /* only implementation */ }
}

// GOOD: Interfaces are useful when:
// 1. You expect multiple implementations
// 2. You need to mock for testing
// 3. You want to swap implementations via DI
```

---

## Pure Functions vs Systems

### Principle
**Separate pure functions (no side effects) from systems (coordinate state and side effects).**

### Why?
- Pure functions are easy to test
- Pure functions are easy to parallelize
- Systems handle Unity/state management
- Clear separation of concerns

### ✅ DO: Pure Functions in Utility Classes

```csharp
// Pure function: Same input always gives same output, no side effects
public static class GridUtility
{
    public static Vector3 GridToWorldPosition(int x, int y, RoadGenerationConfig config)
    {
        float worldX = (x - config.gridWidth / 2f) * config.tileSize;
        float worldZ = (y - config.gridHeight / 2f) * config.tileSize;
        return new Vector3(worldX, 0, worldZ);
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
}
```

### ✅ DO: Systems for State Management

```csharp
// System: Manages state, coordinates pure functions, handles Unity integration
public class ProceduralRoads : MonoBehaviour
{
    // State
    private IGrid<RoadTileData> _grid;
    private List<Vector2Int> _mainPath;
    private System.Random _random;

    // Services
    private IPathGenerator _pathGenerator;
    private IRoadSpawner _roadSpawner;

    public void GenerateRoads()
    {
        // Coordinate pure functions and manage state
        InitializeState();
        TryGenerateMainPath();
        FillTileTypes();
        _roadSpawner.SpawnRoads(_grid, config, _roadContainer.transform);
    }
}
```

### ✅ DO: Make Pure Functions Static

```csharp
// Pure functions should be static (no instance state)
public static class RoadTileUtility
{
    public static RoadTileData DetermineTileType(
        IGrid<RoadTileData> grid,
        Vector2Int position,
        float roundaboutProbability,
        System.Random random)
    {
        // Takes input, returns output, no side effects
        var tile = grid.Get(position.x, position.y);

        // ... determine type based on neighbors ...

        return tile; // Returns new value, doesn't modify grid
    }
}
```

### ❌ DON'T: Mix Pure Logic with Side Effects

```csharp
// BAD: Function modifies state and returns value (two responsibilities)
public RoadTileData DetermineTileType(Vector2Int position)
{
    var tile = _grid.Get(position.x, position.y); // Accessing instance state

    // Determine type...
    tile.Type = RoadTileType.Corner;

    _grid.Set(position.x, position.y, tile); // Side effect!

    return tile;
}

// GOOD: Separate pure logic from state modification
public static RoadTileData DetermineTileType(IGrid<RoadTileData> grid, Vector2Int position)
{
    var tile = grid.Get(position.x, position.y);
    // Determine type...
    tile.Type = RoadTileType.Corner;
    return tile; // Caller decides whether to save
}

// Caller handles side effects
var updatedTile = RoadTileUtility.DetermineTileType(grid, pos);
grid.Set(pos.x, pos.y, updatedTile);
```

---

## Extension Methods

### Principle
**Use extension methods to add behavior to data structures without inheritance.**

### Why?
- Keep data structures pure (just data)
- Add behavior where it's needed
- Avoid polluting base classes
- Improve readability

### ✅ DO: Extension Methods for Data Structures

```csharp
// Data structure: Pure data, no behavior
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
}

// Extension methods: Add behavior without modifying struct
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
}
```

### ✅ DO: Use Extension Methods for Readability

```csharp
// Usage looks like instance methods
var tile = grid.Get(x, y);

// Early exit: already special tile
if (tile.IsSpecial()) continue; // Reads naturally!

// Early exit: empty tile
if (tile.IsEmpty()) continue;

// Get connection count
int connections = tile.GetConnectionCount();
```

### ❌ DON'T: Put Complex Logic in Extension Methods

```csharp
// BAD: Extension method doing too much
public static RoadTileData DetermineTypeAndUpdateNeighbors(
    this RoadTileData tile,
    IGrid<RoadTileData> grid)
{
    // 50 lines of complex logic...
    // Updates multiple grid cells...
    // Has side effects...

    // This should be a static utility function instead
}

// GOOD: Keep extensions simple, delegate complex logic
public static RoadTileData DetermineType(this RoadTileData tile, IGrid<RoadTileData> grid)
{
    return RoadTileUtility.DetermineTileType(grid, tile.GridPosition, 0.3f, new Random());
}
```

---

## Data Structures

### Principle
**Keep data structures pure (just data). Use structs for small data, classes for larger/mutable data.**

### Why?
- Clear separation of data and behavior
- Easy to serialize
- Easy to understand
- Performance benefits (structs)

### ✅ DO: Pure Data Structures

```csharp
// Struct: Small, immutable-ish data
[System.Serializable]
public struct RoadTileData
{
    public RoadTileType Type;
    public int Rotation;
    public Vector2Int GridPosition;
    public bool IsMainPath;

    // Static factory method
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

// Class: Larger, reference type data
[System.Serializable]
public class District
{
    public DistrictType Type;
    public List<Vector2Int> Bounds;
    public Vector2Int CenterPoint;
    public int TargetRoadDensity;
    public List<Vector2Int> BoundaryRoads;

    public District(DistrictType type)
    {
        Type = type;
        Bounds = new List<Vector2Int>();
        BoundaryRoads = new List<Vector2Int>();
    }
}
```

### ✅ DO: Use Enums for Fixed Sets

```csharp
public enum RoadTileType
{
    None,
    Straight,
    Corner,
    TIntersection,
    FourWay,
    Roundabout,
    DeadEnd,
    Start,
    Exit
}

public enum DistrictType
{
    Downtown,
    Suburban,
    Rural,
    Industrial
}

public enum Direction
{
    North,
    South,
    East,
    West
}
```

### ✅ DO: Use [System.Serializable] for Unity

```csharp
// Makes it visible in Unity Inspector and serializable
[System.Serializable]
public class RoadGenerationConfig
{
    [Header("Grid Settings")]
    [Tooltip("Width of the grid in tiles")]
    public int gridWidth = 20;

    [Range(0f, 1f)]
    [Tooltip("Road density")]
    public float roadDensity = 0.5f;
}
```

### ❌ DON'T: Mix Data and Behavior in Data Classes

```csharp
// BAD: Data class with behavior
public class RoadTileData
{
    public RoadTileType Type;
    public int Rotation;

    // BAD: Behavior in data class
    public void CalculateConnections(IGrid<RoadTileData> grid)
    {
        // Complex logic...
    }

    public RoadTileType DetermineType()
    {
        // More complex logic...
    }
}

// GOOD: Keep data separate from behavior
public struct RoadTileData { /* just data */ }

public static class RoadTileUtility
{
    public static RoadTileData DetermineTileType(...) { /* behavior */ }
}
```

---

## Error Handling

### Principle
**Fail fast with clear error messages. Use exceptions for exceptional cases, return empty/default for expected failures.**

### Why?
- Catches bugs early
- Clear error messages help debugging
- Prevents silent failures
- Explicit failure modes

### ✅ DO: Validate Inputs

```csharp
public List<Vector2Int> GeneratePath(
    IGrid<RoadTileData> grid,
    Vector2Int start,
    Vector2Int end,
    RoadGenerationConfig config,
    System.Random random)
{
    // Fail fast: null checks with ArgumentNullException
    if (grid == null) throw new ArgumentNullException(nameof(grid));
    if (config == null) throw new ArgumentNullException(nameof(config));
    if (random == null) throw new ArgumentNullException(nameof(random));

    // Early exit: invalid but expected cases (return empty)
    if (!grid.IsValidPosition(start.x, start.y)) return new List<Vector2Int>();
    if (!grid.IsValidPosition(end.x, end.y)) return new List<Vector2Int>();

    // ... rest of logic
}
```

### ✅ DO: Return Empty for Expected Failures

```csharp
private List<Vector2Int> GenerateSingleBranch(...)
{
    var branch = new List<Vector2Int>();

    // Early exit: invalid starting position (expected case)
    if (!grid.IsValidPosition(current.x, current.y)) return branch; // Return empty

    // Early exit: position already occupied (expected case)
    if (grid.Get(current.x, current.y).Type != RoadTileType.None) return branch;

    // ... generation logic

    return branch;
}
```

### ✅ DO: Use Try-Catch for Unity Integration

```csharp
public void GenerateRoads()
{
    // Validate configuration
    try
    {
        config.Validate();
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Invalid configuration: {e.Message}");
        return; // Early exit
    }

    // ... rest of generation
}
```

### ✅ DO: Log Warnings for Recoverable Issues

```csharp
private bool TryGenerateMainPath()
{
    const int MaxRetries = 5;

    for (int attempt = 0; attempt < MaxRetries; attempt++)
    {
        // ... generation logic ...

        // Early exit: path generation failed
        if (_mainPath.Count == 0)
        {
            Debug.LogWarning($"Path generation attempt {attempt + 1} failed");
            continue; // Retry
        }

        return true;
    }

    // All retries failed
    return false;
}
```

### ❌ DON'T: Swallow Exceptions Silently

```csharp
// BAD: Silent failure
try
{
    config.Validate();
}
catch
{
    // Do nothing - user has no idea what failed!
}

// GOOD: Log and handle
try
{
    config.Validate();
}
catch (System.Exception e)
{
    Debug.LogError($"Configuration validation failed: {e.Message}");
    return; // Exit gracefully
}
```

### ❌ DON'T: Use Exceptions for Control Flow

```csharp
// BAD: Using exceptions for expected cases
public Vector2Int GetNextPosition(Vector2Int current)
{
    try
    {
        var next = current + direction;
        if (!grid.IsValidPosition(next.x, next.y))
        {
            throw new InvalidPositionException(); // BAD!
        }
        return next;
    }
    catch (InvalidPositionException)
    {
        // Try alternate direction
        return GetAlternateDirection(current);
    }
}

// GOOD: Use return values for expected cases
public bool TryGetNextPosition(Vector2Int current, out Vector2Int next)
{
    next = current + direction;

    if (!grid.IsValidPosition(next.x, next.y))
    {
        return false; // Expected failure
    }

    return true;
}
```

---

## Naming Conventions

### Principle
**Use clear, descriptive names. Follow C# conventions.**

### Why?
- Self-documenting code
- Consistency across codebase
- Reduces need for comments
- Industry standard

### ✅ DO: Follow C# Naming Conventions

```csharp
// PascalCase for public members, types, and methods
public class RoadGenerator { }
public interface IPathGenerator { }
public enum RoadTileType { }

public void GenerateRoads() { }
public int TileCount { get; set; }

// camelCase for parameters and local variables
public void Generate(int gridSize, float density)
{
    var roadCount = 0;
    var mainPath = new List<Vector2Int>();
}

// _camelCase with underscore for private fields
private IPathGenerator _pathGenerator;
private List<Vector2Int> _mainPath;
private System.Random _random;

// SCREAMING_SNAKE_CASE for constants
private const int MAX_RETRIES = 5;
private const float DEFAULT_DENSITY = 0.5f;
```

### ✅ DO: Use Descriptive Names

```csharp
// GOOD: Clear what it does
public List<Vector2Int> GenerateMainArtery(Vector2Int start, Vector2Int end) { }

// BAD: Unclear abbreviations
public List<Vector2Int> GenMA(Vector2Int s, Vector2Int e) { }

// GOOD: Clear variable names
var districtBoundaries = new List<Vector2Int>();
var roadDensityTarget = 0.5f;
var maximumBranchLength = 10;

// BAD: Unclear abbreviations
var db = new List<Vector2Int>();
var rdt = 0.5f;
var mbl = 10;
```

### ✅ DO: Boolean Variables as Questions

```csharp
// GOOD: Reads like a question
public bool IsValidPosition(int x, int y) { }
public bool HasRoadNeighbor(Vector2Int pos) { }
public bool CanGenerateBranch() { }

// Usage reads naturally
if (grid.IsValidPosition(x, y))
{
    // ...
}

if (tile.IsOccupied())
{
    // ...
}
```

### ✅ DO: Prefix Interfaces with 'I'

```csharp
public interface IPathGenerator { }
public interface IDistrictGenerator { }
public interface IRoadSpawner { }
public interface IGrid<T> { }
```

### ❌ DON'T: Use Unclear Abbreviations

```csharp
// BAD
var cfg = GetConfig();
var rng = new Random();
var pos = new Vector2Int();
var dir = Direction.North;

// GOOD
var config = GetConfig();
var random = new Random();
var position = new Vector2Int();
var direction = Direction.North;

// Exception: Well-known abbreviations are OK
var id = GetUserId();
var url = GetApiUrl();
var html = GenerateHtml();
```

---

## Unity Integration

### Principle
**Keep Unity-specific code isolated in MonoBehaviour classes. Core logic should be Unity-independent.**

### Why?
- Testable without Unity
- Can run logic in isolation
- Faster iteration (no Unity reload)
- Portable to other frameworks

### ✅ DO: Isolate Unity Code in MonoBehaviour

```csharp
// MonoBehaviour: Unity integration only (glue code)
public class ProceduralRoads : MonoBehaviour
{
    [SerializeField] private RoadGenerationConfig config;

    // Services (Unity-independent)
    private IPathGenerator _pathGenerator;
    private IRoadSpawner _roadSpawner;

    // State
    private IGrid<RoadTileData> _grid;
    private GameObject _roadContainer;

    // Unity callbacks
    private void Awake()
    {
        Initialize();
    }

    // Public API (Unity-friendly)
    public void GenerateRoads()
    {
        // Orchestrate Unity-independent services
        InitializeState();
        TryGenerateMainPath();
        FillTileTypes();
        _roadSpawner.SpawnRoads(_grid, config, _roadContainer.transform);
    }
}
```

### ✅ DO: Keep Core Logic Unity-Independent

```csharp
// Unity-independent: Can be tested without Unity
public class BiasedRandomWalkGenerator : IPathGenerator
{
    public List<Vector2Int> GeneratePath(
        IGrid<RoadTileData> grid,
        Vector2Int start,
        Vector2Int end,
        RoadGenerationConfig config,
        System.Random random)
    {
        // No Unity dependencies!
        // Uses only:
        // - Standard C# types (List, Random, etc.)
        // - Unity structs (Vector2Int - but these work outside Unity)
        // - Custom interfaces (IGrid)

        var path = new List<Vector2Int>();
        // ... pure C# logic ...
        return path;
    }
}
```

### ✅ DO: Use [SerializeField] for Inspector Visibility

```csharp
public class ProceduralRoads : MonoBehaviour
{
    // Private but visible in Inspector
    [SerializeField] private RoadGenerationConfig config;

    // Tooltip for designers
    [Tooltip("Configuration for road generation")]
    [SerializeField] private RoadGenerationConfig config;

    // Header for organization
    [Header("Generation Settings")]
    [SerializeField] private int gridWidth = 20;
    [SerializeField] private int gridHeight = 20;

    [Header("Prefab References")]
    [SerializeField] private GameObject straightPrefab;
    [SerializeField] private GameObject cornerPrefab;
}
```

### ✅ DO: Use Range Attributes for Validation

```csharp
[Range(0f, 1f)]
public float roadDensity = 0.5f;

[Range(0, 100)]
public int maxBranches = 10;
```

### ❌ DON'T: Use Unity Types in Core Logic

```csharp
// BAD: Core logic depends on Unity
public class PathGenerator
{
    public List<Vector2Int> GeneratePath(Transform startTransform, Transform endTransform)
    {
        // Using Transform means this can only run in Unity!
        var start = startTransform.position;
        var end = endTransform.position;
        // ...
    }
}

// GOOD: Use Unity-independent types
public class PathGenerator
{
    public List<Vector2Int> GeneratePath(Vector2Int start, Vector2Int end)
    {
        // Can be tested without Unity
        // ...
    }
}
```

---

## Testing Patterns

### Principle
**Write testable code. Test pure functions in isolation. Mock dependencies for integration tests.**

### Why?
- Catch bugs early
- Refactor with confidence
- Document behavior
- Regression prevention

### ✅ DO: Test Pure Functions

```csharp
[Test]
public void GridToWorldPosition_ConvertsCorrectly()
{
    // Arrange
    var config = new RoadGenerationConfig
    {
        gridWidth = 20,
        gridHeight = 20,
        tileSize = 10f
    };

    // Act
    var worldPos = GridUtility.GridToWorldPosition(0, 0, config);

    // Assert
    Assert.AreEqual(-100f, worldPos.x); // (0 - 20/2) * 10
    Assert.AreEqual(0f, worldPos.y);
    Assert.AreEqual(-100f, worldPos.z);
}

[Test]
public void GetNeighborInDirection_North_ReturnsCorrectPosition()
{
    // Arrange
    var position = new Vector2Int(5, 5);

    // Act
    var neighbor = GridUtility.GetNeighborInDirection(position, Direction.North);

    // Assert
    Assert.AreEqual(new Vector2Int(5, 6), neighbor);
}
```

### ✅ DO: Test Without Unity Dependencies

```csharp
[Test]
public void BiasedRandomWalkGenerator_GeneratesPath()
{
    // Arrange: Create services without Unity
    var grid = new Grid2D<RoadTileData>(10, 10);
    var config = new RoadGenerationConfig
    {
        gridWidth = 10,
        gridHeight = 10,
        pathWindingness = 0.3f
    };
    var generator = new BiasedRandomWalkGenerator();
    var random = new System.Random(42); // Fixed seed for deterministic tests

    var start = new Vector2Int(0, 0);
    var end = new Vector2Int(9, 9);

    // Act
    var path = generator.GeneratePath(grid, start, end, config, random);

    // Assert
    Assert.IsNotEmpty(path);
    Assert.AreEqual(start, path[0]);
    Assert.AreEqual(end, path[path.Count - 1]);
}
```

### ✅ DO: Use Mocks for Dependencies

```csharp
[Test]
public void HierarchicalRoadGenerator_UsesInjectedDependencies()
{
    // Arrange: Create mock dependencies
    var mockArteryGen = new Mock<IPathGenerator>();
    var mockDistrictGen = new Mock<IDistrictGenerator>();
    var mockDistrictRoadGens = new Dictionary<DistrictType, IDistrictRoadGenerator>();

    // Setup mock behavior
    mockArteryGen
        .Setup(g => g.GeneratePath(It.IsAny<IGrid<RoadTileData>>(), It.IsAny<Vector2Int>(),
                                    It.IsAny<Vector2Int>(), It.IsAny<RoadGenerationConfig>(),
                                    It.IsAny<System.Random>()))
        .Returns(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 1) });

    // Act: Inject mocks
    var generator = new HierarchicalRoadGenerator(
        mockArteryGen.Object,
        mockDistrictGen.Object,
        mockDistrictRoadGens);

    // ... test behavior

    // Assert: Verify mock was called
    mockArteryGen.Verify(g => g.GeneratePath(
        It.IsAny<IGrid<RoadTileData>>(),
        It.IsAny<Vector2Int>(),
        It.IsAny<Vector2Int>(),
        It.IsAny<RoadGenerationConfig>(),
        It.IsAny<System.Random>()), Times.Once);
}
```

### ✅ DO: Test Edge Cases

```csharp
[Test]
public void GeneratePath_StartEqualsEnd_ReturnsSingleElement()
{
    var grid = new Grid2D<RoadTileData>(10, 10);
    var config = new RoadGenerationConfig();
    var generator = new BiasedRandomWalkGenerator();
    var random = new System.Random();

    var start = new Vector2Int(5, 5);
    var end = new Vector2Int(5, 5); // Same as start

    var path = generator.GeneratePath(grid, start, end, config, random);

    Assert.AreEqual(1, path.Count);
    Assert.AreEqual(start, path[0]);
}

[Test]
public void GeneratePath_InvalidStart_ReturnsEmpty()
{
    var grid = new Grid2D<RoadTileData>(10, 10);
    var config = new RoadGenerationConfig();
    var generator = new BiasedRandomWalkGenerator();
    var random = new System.Random();

    var start = new Vector2Int(-1, -1); // Invalid
    var end = new Vector2Int(5, 5);

    var path = generator.GeneratePath(grid, start, end, config, random);

    Assert.IsEmpty(path);
}
```

---

## Common Pitfalls & Solutions

### Pitfall 1: Mutable Shared State

**Problem:**
```csharp
// BAD: Singleton with mutable state causes cross-contamination
public class RoadSpawner : IRoadSpawner
{
    private static RoadSpawner _instance;
    private List<GameObject> _spawnedObjects = new List<GameObject>(); // Shared!

    public static RoadSpawner Instance => _instance ??= new RoadSpawner();
}

// Generator 1 spawns roads -> adds to list
// Generator 2 spawns roads -> sees Generator 1's roads!
```

**Solution:**
```csharp
// GOOD: Transient service, each generator gets its own instance
serviceCollection.AddTransient<IRoadSpawner, UnityRoadSpawner>();

// Each call gets fresh instance with empty _spawnedObjects list
var spawner1 = DI.GetService<IRoadSpawner>(); // New instance
var spawner2 = DI.GetService<IRoadSpawner>(); // Different instance
```

### Pitfall 2: Not Clearing State Between Retries

**Problem:**
```csharp
// BAD: Previous attempt's markers block future attempts
for (int attempt = 0; attempt < MaxRetries; attempt++)
{
    PlaceStartAndExit();
    var path = GeneratePath();

    if (path.Count == 0) continue; // Retry, but markers are still there!
}
```

**Solution:**
```csharp
// GOOD: Clear state before each retry
for (int attempt = 0; attempt < MaxRetries; attempt++)
{
    // Clear previous attempt's markers
    if (attempt > 0)
    {
        foreach (var pos in _grid.GetAllPositions())
        {
            _grid.Set(pos.x, pos.y, RoadTileData.Empty(pos.x, pos.y));
        }
    }

    PlaceStartAndExit();
    var path = GeneratePath();
    if (path.Count == 0) continue;
}
```

### Pitfall 3: Visiting Same Cell Multiple Times

**Problem:**
```csharp
// BAD: Can oscillate between two cells
var current = start;
while (current != end)
{
    var next = GetRandomNeighbor(current);
    // Might return previous position!
    current = next;
}
```

**Solution:**
```csharp
// GOOD: Track visited cells
var visited = new HashSet<Vector2Int>();
var current = start;
visited.Add(current);

while (current != end)
{
    // Early exit: already visited
    if (visited.Contains(current)) break;

    visited.Add(current);
    var next = GetRandomNeighbor(current);
    current = next;
}
```

### Pitfall 4: Missing Null Checks on Optional Parameters

**Problem:**
```csharp
// BAD: Null reference if grid is null
public void ProcessGrid(IGrid<RoadTileData> grid)
{
    for (int x = 0; x < grid.Width; x++) // NullReferenceException!
    {
        // ...
    }
}
```

**Solution:**
```csharp
// GOOD: Early exit for null
public void ProcessGrid(IGrid<RoadTileData> grid)
{
    // Early exit: null check
    if (grid == null) throw new ArgumentNullException(nameof(grid));

    for (int x = 0; x < grid.Width; x++)
    {
        // ...
    }
}
```

---

## Code Review Checklist

When reviewing code (your own or others'), check for:

### Architecture
- [ ] Are dependencies injected via constructor?
- [ ] Are interfaces used for abstractions?
- [ ] Is core logic Unity-independent?
- [ ] Are pure functions separated from systems?

### Code Quality
- [ ] Are there early exit patterns instead of deep nesting?
- [ ] Are variable names clear and descriptive?
- [ ] Are functions doing one thing?
- [ ] Is there proper error handling?

### Data Structures
- [ ] Are data structures pure (no behavior)?
- [ ] Are extension methods used appropriately?
- [ ] Is [System.Serializable] used for Unity classes?

### Testing
- [ ] Can the code be tested without Unity?
- [ ] Are there null checks on inputs?
- [ ] Are edge cases handled?

### Performance
- [ ] Are there any unnecessary allocations in loops?
- [ ] Is state cleared between retries/runs?
- [ ] Are HashSets used for lookups instead of List.Contains()?

---

## Summary

### Core Principles

1. **Early Exit** - Exit functions early when conditions aren't met
2. **Dependency Injection** - Inject dependencies, don't hard-code them
3. **Interface-Based Design** - Program against interfaces, not implementations
4. **Pure Functions** - Separate data transformation from side effects
5. **Extension Methods** - Add behavior to data structures without inheritance
6. **Clear Naming** - Use descriptive names that explain intent
7. **Unity Isolation** - Keep Unity code in MonoBehaviours, core logic Unity-independent
8. **Testability** - Write code that can be tested without Unity

### Quick Reference

```csharp
// Early Exit Pattern
if (invalid) return;
if (empty) continue;
if (error) throw;

// Dependency Injection
public MyClass(IDependency dep) { _dep = dep; }

// Interface-Based Design
public interface IService { void DoWork(); }
public class Service : IService { }

// Pure Functions
public static Result Calculate(Input input) { /* no side effects */ }

// Extension Methods
public static bool IsEmpty(this Data data) { }

// Data Structures
public struct Data { /* just data */ }

// Error Handling
if (x == null) throw new ArgumentNullException(nameof(x));
if (invalid) return empty;

// Unity Integration
public class MyBehaviour : MonoBehaviour { }
[SerializeField] private Config config;
```

---

## Resources

### Further Reading
- [C# Coding Conventions (Microsoft)](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Dependency Injection in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Unit Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

### Tools
- **ReSharper** - Code analysis and refactoring
- **SonarLint** - Code quality and security
- **NUnit** - Unit testing framework
- **Moq** - Mocking framework for tests

---

*This guide is a living document. Update it as new patterns emerge or existing patterns evolve.*
