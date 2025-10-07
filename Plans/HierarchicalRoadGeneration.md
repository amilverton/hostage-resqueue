# Hierarchical Road Generation System - Design Document

## Overview

This document outlines a hierarchical road generation system that replaces the current biased random walk approach with a multi-pass district-based generation system. The system generates realistic road networks with distinct areas (downtown, suburbs, rural) while maintaining a **guaranteed start-to-exit path**.

## Core Principle

**The main artery (start → exit) is generated FIRST and NEVER modified. All other roads are built around it.**

## System Architecture

### Integration with Existing System

This system integrates with the existing DI-based architecture:

```
DI Container
    ├──► IPathGenerator → HierarchicalRoadGenerator (NEW - replaces BiasedRandomWalkGenerator)
    ├──► IBranchGenerator → (Not used in hierarchical system)
    └──► IRoadSpawner → UnityRoadSpawner (unchanged)
```

### New Interfaces

```csharp
public interface IDistrictGenerator
{
    /// <summary>
    /// Subdivide the grid into districts based on major roads
    /// </summary>
    List<District> GenerateDistricts(
        IGrid<RoadTileData> grid,
        List<Vector2Int> mainArtery,
        RoadGenerationConfig config,
        System.Random random);
}

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
```

### New Data Structures

```csharp
public enum DistrictType
{
    Downtown,      // Dense grid pattern, high intersection density
    Suburban,      // Organic curves, cul-de-sacs, loops
    Rural,         // Sparse, winding roads, few intersections
    Industrial     // Regular wide-spaced grid
}

public enum PathGeneratorType
{
    BiasedRandomWalk,
    Hierarchical
}

[System.Serializable]
public class District
{
    public DistrictType Type;
    public List<Vector2Int> Bounds;           // Cells that belong to this district
    public Vector2Int CenterPoint;            // Approximate center for reference
    public int TargetRoadDensity;             // Roads per 100 tiles
    public List<Vector2Int> BoundaryRoads;    // Roads that form district boundaries

    public District(DistrictType type)
    {
        Type = type;
        Bounds = new List<Vector2Int>();
        BoundaryRoads = new List<Vector2Int>();
    }
}
```

### Updated Configuration

```csharp
[System.Serializable]
public class RoadGenerationConfig
{
    // ... existing fields ...

    [Header("Hierarchical Generation Settings")]
    [Tooltip("Use hierarchical district-based generation instead of simple random walk")]
    public bool useHierarchicalGeneration = true;

    [Tooltip("Generator type to use when hierarchical is disabled")]
    public PathGeneratorType fallbackGenerator = PathGeneratorType.BiasedRandomWalk;

    [Tooltip("Number of additional major roads beyond the main artery (0-2 recommended)")]
    [Range(0, 3)]
    public int additionalMajorRoads = 1;

    [Header("District Settings")]
    [Tooltip("Target number of districts to create (actual number may vary)")]
    [Range(2, 8)]
    public int targetDistrictCount = 4;

    [Tooltip("Probability of each district type")]
    public DistrictProbabilities districtProbabilities = new DistrictProbabilities();

    [Header("District Density Settings")]
    [Range(0f, 1f)]
    public float downtownDensity = 0.8f;      // Very dense

    [Range(0f, 1f)]
    public float suburbanDensity = 0.5f;      // Medium density

    [Range(0f, 1f)]
    public float ruralDensity = 0.2f;         // Sparse

    [Range(0f, 1f)]
    public float industrialDensity = 0.4f;    // Medium-sparse
}

[System.Serializable]
public class DistrictProbabilities
{
    [Range(0f, 1f)]
    public float downtown = 0.25f;

    [Range(0f, 1f)]
    public float suburban = 0.4f;

    [Range(0f, 1f)]
    public float rural = 0.25f;

    [Range(0f, 1f)]
    public float industrial = 0.1f;

    /// <summary>
    /// Normalize probabilities to sum to 1.0
    /// </summary>
    public void Normalize()
    {
        float sum = downtown + suburban + rural + industrial;
        if (sum > 0)
        {
            downtown /= sum;
            suburban /= sum;
            rural /= sum;
            industrial /= sum;
        }
    }
}
```

## Generation Algorithm

### Pass 1: Main Artery Generation (Guaranteed Path)

**Purpose**: Create the guaranteed start-to-exit connection that will never be modified.

**Implementation**: `MainArteryGenerator : IPathGenerator`

```csharp
public class MainArteryGenerator : IPathGenerator
{
    public List<Vector2Int> GeneratePath(
        IGrid<RoadTileData> grid,
        Vector2Int start,
        Vector2Int end,
        RoadGenerationConfig config,
        System.Random random)
    {
        // Early exit: invalid positions
        if (!grid.IsValidPosition(start.x, start.y)) return new List<Vector2Int>();
        if (!grid.IsValidPosition(end.x, end.y)) return new List<Vector2Int>();
        if (start == end) return new List<Vector2Int>();

        var path = new List<Vector2Int>();

        // Strategy: Create path with long straight segments and occasional turns
        // This looks more like a highway/main road than random meandering

        var current = start;
        path.Add(current);

        const int minSegmentLength = 5;  // Minimum straight segment before turning
        int currentSegmentLength = 0;

        while (current != end)
        {
            // Calculate direction to target
            Vector2Int toTarget = end - current;

            // Primary direction (horizontal or vertical based on which is larger)
            Direction primaryDir = GetPrimaryDirection(toTarget);

            // If we've traveled long enough in one direction, consider turning
            bool canTurn = currentSegmentLength >= minSegmentLength;

            Direction nextDir;

            if (canTurn && NeedsTurn(current, end))
            {
                // Turn to approach target on other axis
                nextDir = GetSecondaryDirection(toTarget);
                currentSegmentLength = 0;
            }
            else
            {
                // Continue in primary direction
                nextDir = primaryDir;
                currentSegmentLength++;
            }

            // Move in chosen direction
            var next = GridUtility.GetNeighborInDirection(current, nextDir);

            // Early exit: hit boundary or invalid position
            if (!grid.IsValidPosition(next.x, next.y))
            {
                // Try alternate direction
                nextDir = GetAlternateDirection(current, end, grid);
                next = GridUtility.GetNeighborInDirection(current, nextDir);

                if (!grid.IsValidPosition(next.x, next.y))
                {
                    // Failed to generate path
                    return new List<Vector2Int>();
                }
            }

            current = next;
            path.Add(current);

            // Safety check: prevent infinite loops
            if (path.Count > grid.Width * grid.Height)
            {
                return new List<Vector2Int>();
            }
        }

        return path;
    }

    private Direction GetPrimaryDirection(Vector2Int toTarget)
    {
        // Choose based on which axis has more distance to cover
        if (Mathf.Abs(toTarget.x) > Mathf.Abs(toTarget.y))
        {
            return toTarget.x > 0 ? Direction.East : Direction.West;
        }
        else
        {
            return toTarget.y > 0 ? Direction.North : Direction.South;
        }
    }

    private Direction GetSecondaryDirection(Vector2Int toTarget)
    {
        // Choose the other axis
        if (Mathf.Abs(toTarget.x) <= Mathf.Abs(toTarget.y))
        {
            return toTarget.x > 0 ? Direction.East : Direction.West;
        }
        else
        {
            return toTarget.y > 0 ? Direction.North : Direction.South;
        }
    }

    private bool NeedsTurn(Vector2Int current, Vector2Int target)
    {
        Vector2Int diff = target - current;
        // Need to turn if we need to cover distance on both axes
        return Mathf.Abs(diff.x) > 0 && Mathf.Abs(diff.y) > 0;
    }

    private Direction GetAlternateDirection(Vector2Int current, Vector2Int target, IGrid<RoadTileData> grid)
    {
        Vector2Int toTarget = target - current;

        // Try all directions, prefer ones toward target AND valid
        var directions = new List<(Direction dir, float score)>();

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            var neighbor = GridUtility.GetNeighborInDirection(current, dir);

            // Skip invalid positions
            if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;

            Vector2Int newDiff = target - neighbor;
            float distance = newDiff.magnitude;
            directions.Add((dir, -distance)); // Negative so closer = higher score
        }

        // Early exit: no valid directions at all (should never happen on valid grid)
        if (directions.Count == 0)
        {
            return Direction.North; // Fallback
        }

        // Sort by score (closest to target first)
        directions.Sort((a, b) => b.score.CompareTo(a.score));

        return directions[0].dir;
    }
}
```

### Pass 2: Additional Major Roads (Optional)

**Purpose**: Add 0-2 more major roads to create a more complex road network backbone.

**Implementation**: `AdditionalArteryGenerator`

```csharp
public class AdditionalArteryGenerator
{
    public List<List<Vector2Int>> GenerateAdditionalArteries(
        IGrid<RoadTileData> grid,
        List<Vector2Int> mainArtery,
        RoadGenerationConfig config,
        System.Random random)
    {
        var arteries = new List<List<Vector2Int>>();

        // Early exit: no additional roads requested
        if (config.additionalMajorRoads <= 0)
        {
            return arteries;
        }

        // Pick branch points along main artery (not too close to start/end)
        var branchPoints = SelectBranchPoints(mainArtery, config.additionalMajorRoads, random);

        foreach (var branchPoint in branchPoints)
        {
            // Generate a perpendicular road from branch point to grid edge
            var artery = GeneratePerpendicularArtery(grid, mainArtery, branchPoint, random);

            // Early exit: failed to generate
            if (artery.Count == 0) continue;

            arteries.Add(artery);
        }

        return arteries;
    }

    private List<Vector2Int> SelectBranchPoints(List<Vector2Int> mainArtery, int count, System.Random random)
    {
        var branchPoints = new List<Vector2Int>();

        // Don't branch too close to start or end (skip first and last 20%)
        int skipCount = Mathf.Max(1, mainArtery.Count / 5);
        int validStart = skipCount;
        int validEnd = mainArtery.Count - skipCount;

        // Early exit: not enough space
        if (validEnd <= validStart) return branchPoints;

        // Select evenly spaced points
        int validRange = validEnd - validStart;
        int step = validRange / (count + 1);

        for (int i = 1; i <= count; i++)
        {
            int index = validStart + (step * i);
            if (index >= 0 && index < mainArtery.Count)
            {
                branchPoints.Add(mainArtery[index]);
            }
        }

        return branchPoints;
    }

    private List<Vector2Int> GeneratePerpendicularArtery(
        IGrid<RoadTileData> grid,
        List<Vector2Int> mainArtery,
        Vector2Int branchPoint,
        System.Random random)
    {
        var artery = new List<Vector2Int>();

        // Determine perpendicular direction
        Direction mainDirection = GetMainArteryDirection(mainArtery, branchPoint);
        Direction perpendicularDir = GetPerpendicularDirection(mainDirection, random);

        // Walk in perpendicular direction until hitting grid edge
        var current = branchPoint;

        while (grid.IsValidPosition(current.x, current.y))
        {
            artery.Add(current);

            var next = GridUtility.GetNeighborInDirection(current, perpendicularDir);

            // Early exit: hit edge
            if (!grid.IsValidPosition(next.x, next.y)) break;

            current = next;
        }

        return artery;
    }

    private Direction GetMainArteryDirection(List<Vector2Int> mainArtery, Vector2Int point)
    {
        // Find point in artery
        int index = mainArtery.IndexOf(point);

        // Early exit: not found or at end
        if (index < 0 || index >= mainArtery.Count - 1)
        {
            return Direction.North; // Default
        }

        // Look at direction to next point
        Vector2Int toNext = mainArtery[index + 1] - point;

        if (Mathf.Abs(toNext.x) > Mathf.Abs(toNext.y))
        {
            return toNext.x > 0 ? Direction.East : Direction.West;
        }
        else
        {
            return toNext.y > 0 ? Direction.North : Direction.South;
        }
    }

    private Direction GetPerpendicularDirection(Direction mainDir, System.Random random)
    {
        switch (mainDir)
        {
            case Direction.North:
            case Direction.South:
                return random.Next(2) == 0 ? Direction.East : Direction.West;

            case Direction.East:
            case Direction.West:
                return random.Next(2) == 0 ? Direction.North : Direction.South;

            default:
                return Direction.North;
        }
    }
}
```

### Pass 3: District Subdivision

**Purpose**: Divide the grid into districts using major roads as boundaries.

**Implementation**: `VoronoiDistrictGenerator : IDistrictGenerator`

```csharp
public class VoronoiDistrictGenerator : IDistrictGenerator
{
    public List<District> GenerateDistricts(
        IGrid<RoadTileData> grid,
        List<Vector2Int> mainArtery,
        RoadGenerationConfig config,
        System.Random random)
    {
        var districts = new List<District>();

        // Step 1: Place district seeds (centers)
        var seeds = PlaceDistrictSeeds(grid, mainArtery, config, random);

        // Step 2: Assign each cell to nearest seed (Voronoi partitioning)
        var cellAssignments = AssignCellsToSeeds(grid, seeds, mainArtery);

        // Step 3: Create district objects from assignments
        districts = CreateDistrictsFromAssignments(cellAssignments, seeds, config, random);

        return districts;
    }

    private List<Vector2Int> PlaceDistrictSeeds(
        IGrid<RoadTileData> grid,
        List<Vector2Int> mainArtery,
        RoadGenerationConfig config,
        System.Random random)
    {
        var seeds = new List<Vector2Int>();
        int targetCount = config.targetDistrictCount;

        // Try to place seeds away from main artery and away from each other
        const int maxAttempts = 100;
        const int minSeedDistance = 10; // Minimum distance between seeds

        for (int i = 0; i < targetCount && seeds.Count < targetCount; i++)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int x = random.Next(0, grid.Width);
                int y = random.Next(0, grid.Height);
                var candidate = new Vector2Int(x, y);

                // Check if valid position
                if (!grid.IsValidPosition(x, y)) continue;

                // Check if too close to main artery
                float distToArtery = GetMinDistanceToPath(candidate, mainArtery);
                if (distToArtery < 3) continue; // Too close to main road

                // Check if too close to other seeds
                bool tooClose = false;
                foreach (var seed in seeds)
                {
                    if (Vector2Int.Distance(candidate, seed) < minSeedDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose) continue;

                // Valid seed position
                seeds.Add(candidate);
                break;
            }
        }

        return seeds;
    }

    private Dictionary<Vector2Int, int> AssignCellsToSeeds(
        IGrid<RoadTileData> grid,
        List<Vector2Int> seeds,
        List<Vector2Int> mainArtery)
    {
        var assignments = new Dictionary<Vector2Int, int>();

        // Convert main artery to HashSet for fast lookup
        var arterySet = new HashSet<Vector2Int>(mainArtery);

        // For each grid cell, find nearest seed
        foreach (var pos in grid.GetAllPositions())
        {
            // Skip cells that are part of main artery (they're boundaries)
            if (arterySet.Contains(pos)) continue;

            // Find nearest seed
            int nearestSeedIndex = -1;
            float minDistance = float.MaxValue;

            for (int i = 0; i < seeds.Count; i++)
            {
                float distance = Vector2Int.Distance(pos, seeds[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestSeedIndex = i;
                }
            }

            if (nearestSeedIndex >= 0)
            {
                assignments[pos] = nearestSeedIndex;
            }
        }

        return assignments;
    }

    private List<District> CreateDistrictsFromAssignments(
        Dictionary<Vector2Int, int> assignments,
        List<Vector2Int> seeds,
        RoadGenerationConfig config,
        System.Random random)
    {
        var districts = new List<District>();

        // Normalize district probabilities
        config.districtProbabilities.Normalize();

        // Create a district for each seed
        for (int i = 0; i < seeds.Count; i++)
        {
            // Choose district type based on probabilities
            DistrictType type = ChooseDistrictType(config.districtProbabilities, random);

            var district = new District(type)
            {
                CenterPoint = seeds[i]
            };

            // Assign cells to this district
            foreach (var kvp in assignments)
            {
                if (kvp.Value == i)
                {
                    district.Bounds.Add(kvp.Key);
                }
            }

            // Identify boundary roads (cells adjacent to other districts or main artery)
            var districtSet = new HashSet<Vector2Int>(district.Bounds);
            var arterySet = new HashSet<Vector2Int>(mainArtery);

            foreach (var pos in district.Bounds)
            {
                // Check if any neighbor is outside district or on artery
                foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
                {
                    var neighbor = GridUtility.GetNeighborInDirection(pos, dir);

                    if (arterySet.Contains(neighbor) || !districtSet.Contains(neighbor))
                    {
                        district.BoundaryRoads.Add(pos);
                        break;
                    }
                }
            }

            // Set target density based on type
            district.TargetRoadDensity = GetDensityForType(type, config);

            districts.Add(district);
        }

        return districts;
    }

    private DistrictType ChooseDistrictType(DistrictProbabilities probs, System.Random random)
    {
        float roll = (float)random.NextDouble();
        float cumulative = 0f;

        cumulative += probs.downtown;
        if (roll < cumulative) return DistrictType.Downtown;

        cumulative += probs.suburban;
        if (roll < cumulative) return DistrictType.Suburban;

        cumulative += probs.rural;
        if (roll < cumulative) return DistrictType.Rural;

        return DistrictType.Industrial;
    }

    private int GetDensityForType(DistrictType type, RoadGenerationConfig config)
    {
        return type switch
        {
            DistrictType.Downtown => Mathf.RoundToInt(config.downtownDensity * 100),
            DistrictType.Suburban => Mathf.RoundToInt(config.suburbanDensity * 100),
            DistrictType.Rural => Mathf.RoundToInt(config.ruralDensity * 100),
            DistrictType.Industrial => Mathf.RoundToInt(config.industrialDensity * 100),
            _ => 50
        };
    }

    private float GetMinDistanceToPath(Vector2Int point, List<Vector2Int> path)
    {
        float minDist = float.MaxValue;

        foreach (var pathPoint in path)
        {
            float dist = Vector2Int.Distance(point, pathPoint);
            if (dist < minDist)
            {
                minDist = dist;
            }
        }

        return minDist;
    }
}
```

### Pass 4: District-Specific Road Generation

**Purpose**: Generate roads within each district based on its type.

**Implementation**: District-specific generators

```csharp
// Downtown: Dense grid pattern
public class DowntownRoadGenerator : IDistrictRoadGenerator
{
    public List<Vector2Int> GenerateDistrictRoads(
        IGrid<RoadTileData> grid,
        District district,
        RoadGenerationConfig config,
        System.Random random)
    {
        var roads = new List<Vector2Int>();

        // Early exit: empty district
        if (district.Bounds.Count == 0) return roads;

        // Find bounding box of district
        var bounds = GetBoundingBox(district.Bounds);

        // Generate grid pattern: horizontal and vertical lines
        int spacing = 3 + random.Next(2); // 3-4 cells between roads

        // Horizontal roads
        for (int y = bounds.yMin; y <= bounds.yMax; y += spacing)
        {
            for (int x = bounds.xMin; x <= bounds.xMax; x++)
            {
                var pos = new Vector2Int(x, y);

                // Only add if in district bounds and not already occupied
                if (district.Bounds.Contains(pos) &&
                    grid.Get(x, y).Type == RoadTileType.None)
                {
                    roads.Add(pos);
                }
            }
        }

        // Vertical roads
        for (int x = bounds.xMin; x <= bounds.xMax; x += spacing)
        {
            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                var pos = new Vector2Int(x, y);

                // Only add if in district bounds and not already occupied
                if (district.Bounds.Contains(pos) &&
                    grid.Get(x, y).Type == RoadTileType.None)
                {
                    roads.Add(pos);
                }
            }
        }

        return roads;
    }

    private (int xMin, int xMax, int yMin, int yMax) GetBoundingBox(List<Vector2Int> positions)
    {
        int xMin = int.MaxValue, xMax = int.MinValue;
        int yMin = int.MaxValue, yMax = int.MinValue;

        foreach (var pos in positions)
        {
            if (pos.x < xMin) xMin = pos.x;
            if (pos.x > xMax) xMax = pos.x;
            if (pos.y < yMin) yMin = pos.y;
            if (pos.y > yMax) yMax = pos.y;
        }

        return (xMin, xMax, yMin, yMax);
    }
}

// Suburban: Organic curves with cul-de-sacs
public class SuburbanRoadGenerator : IDistrictRoadGenerator
{
    public List<Vector2Int> GenerateDistrictRoads(
        IGrid<RoadTileData> grid,
        District district,
        RoadGenerationConfig config,
        System.Random random)
    {
        var roads = new List<Vector2Int>();

        // Early exit: empty district
        if (district.Bounds.Count == 0) return roads;

        // Create main suburban roads (curved)
        int targetRoadCount = district.Bounds.Count * district.TargetRoadDensity / 100;

        // Generate several curved roads from district edges
        int numMainRoads = 2 + random.Next(2); // 2-3 main roads

        for (int i = 0; i < numMainRoads; i++)
        {
            var mainRoad = GenerateCurvedRoad(grid, district, config, random);
            roads.AddRange(mainRoad);

            // Add cul-de-sacs branching from main road
            var culDeSacs = GenerateCulDeSacs(grid, district, mainRoad, random);
            roads.AddRange(culDeSacs);
        }

        return roads;
    }

    private List<Vector2Int> GenerateCurvedRoad(
        IGrid<RoadTileData> grid,
        District district,
        RoadGenerationConfig config,
        System.Random random)
    {
        var road = new List<Vector2Int>();
        var districtSet = new HashSet<Vector2Int>(district.Bounds);
        var visited = new HashSet<Vector2Int>();

        // Start from a random boundary position
        var startPos = GetRandomBoundaryPosition(district, random);
        if (!districtSet.Contains(startPos)) return road;

        var current = startPos;
        road.Add(current);
        visited.Add(current);

        // Walk with high windingness for curves
        const int maxLength = 20;

        for (int i = 0; i < maxLength; i++)
        {
            var neighbors = GetValidNeighbors(grid, current, districtSet, visited);

            // Early exit: no valid neighbors
            if (neighbors.Count == 0) break;

            // Prefer curved paths: choose direction different from previous
            var next = neighbors[random.Next(neighbors.Count)];

            current = next;
            road.Add(current);
            visited.Add(current);

            // Mark as occupied
            var tile = grid.Get(current.x, current.y);
            tile.Type = RoadTileType.Straight;
            grid.Set(current.x, current.y, tile);
        }

        return road;
    }

    private List<Vector2Int> GenerateCulDeSacs(
        IGrid<RoadTileData> grid,
        District district,
        List<Vector2Int> mainRoad,
        System.Random random)
    {
        var culDeSacs = new List<Vector2Int>();
        var districtSet = new HashSet<Vector2Int>(district.Bounds);

        // Generate 1-3 cul-de-sacs from main road
        int culDeSacCount = 1 + random.Next(3);

        for (int i = 0; i < culDeSacCount; i++)
        {
            // Pick random point on main road
            if (mainRoad.Count == 0) break;

            var branchPoint = mainRoad[random.Next(mainRoad.Count)];

            // Generate short dead-end road (4-6 tiles)
            int length = 4 + random.Next(3);
            var culDeSac = GenerateDeadEndRoad(grid, branchPoint, districtSet, length, random);

            culDeSacs.AddRange(culDeSac);
        }

        return culDeSacs;
    }

    private List<Vector2Int> GenerateDeadEndRoad(
        IGrid<RoadTileData> grid,
        Vector2Int start,
        HashSet<Vector2Int> validCells,
        int length,
        System.Random random)
    {
        var road = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var current = start;

        for (int i = 0; i < length; i++)
        {
            var neighbors = GetValidNeighbors(grid, current, validCells, visited);

            // Early exit: no more space
            if (neighbors.Count == 0) break;

            var next = neighbors[random.Next(neighbors.Count)];

            road.Add(next);
            visited.Add(next);

            // Mark as occupied
            var tile = grid.Get(next.x, next.y);
            tile.Type = RoadTileType.Straight;
            grid.Set(next.x, next.y, tile);

            current = next;
        }

        return road;
    }

    private Vector2Int GetRandomBoundaryPosition(District district, System.Random random)
    {
        // Find cells on district boundary (adjacent to non-district cells or roads)
        var boundaryCells = new List<Vector2Int>();
        var districtSet = new HashSet<Vector2Int>(district.Bounds);

        foreach (var pos in district.Bounds)
        {
            // Check if any neighbor is outside district
            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                var neighbor = GridUtility.GetNeighborInDirection(pos, dir);
                if (!districtSet.Contains(neighbor))
                {
                    boundaryCells.Add(pos);
                    break;
                }
            }
        }

        // Return random boundary cell or center if none found
        return boundaryCells.Count > 0
            ? boundaryCells[random.Next(boundaryCells.Count)]
            : district.CenterPoint;
    }

    private List<Vector2Int> GetValidNeighbors(
        IGrid<RoadTileData> grid,
        Vector2Int position,
        HashSet<Vector2Int> validCells,
        HashSet<Vector2Int> visited)
    {
        var neighbors = new List<Vector2Int>();

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            var neighbor = GridUtility.GetNeighborInDirection(position, dir);

            // Check validity
            if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;
            if (!validCells.Contains(neighbor)) continue;
            if (visited.Contains(neighbor)) continue;
            if (grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None) continue;

            neighbors.Add(neighbor);
        }

        return neighbors;
    }
}

// Rural: Sparse winding roads
public class RuralRoadGenerator : IDistrictRoadGenerator
{
    public List<Vector2Int> GenerateDistrictRoads(
        IGrid<RoadTileData> grid,
        District district,
        RoadGenerationConfig config,
        System.Random random)
    {
        var roads = new List<Vector2Int>();

        // Early exit: empty district
        if (district.Bounds.Count == 0) return roads;

        // Very sparse roads - just 1-2 winding paths
        int roadCount = 1 + (district.Bounds.Count > 100 ? 1 : 0);

        for (int i = 0; i < roadCount; i++)
        {
            var road = GenerateWindingRoad(grid, district, random);
            roads.AddRange(road);
        }

        return roads;
    }

    private List<Vector2Int> GenerateWindingRoad(
        IGrid<RoadTileData> grid,
        District district,
        System.Random random)
    {
        var road = new List<Vector2Int>();
        var districtSet = new HashSet<Vector2Int>(district.Bounds);
        var visited = new HashSet<Vector2Int>();

        // Start from center-ish area
        var current = district.CenterPoint;

        // Adjust to valid position if needed
        if (!districtSet.Contains(current))
        {
            current = district.Bounds[random.Next(district.Bounds.Count)];
        }

        road.Add(current);
        visited.Add(current);

        // Generate long, winding road
        const int maxLength = 30;

        for (int i = 0; i < maxLength; i++)
        {
            var neighbors = GetValidRuralNeighbors(grid, current, districtSet, visited);

            // Early exit: no valid neighbors
            if (neighbors.Count == 0) break;

            // Prefer continuing in same direction (creates long straight segments)
            var next = ChooseRuralDirection(current, road, neighbors, random);

            current = next;
            road.Add(current);
            visited.Add(current);

            // Mark as occupied
            var tile = grid.Get(current.x, current.y);
            tile.Type = RoadTileType.Straight;
            grid.Set(current.x, current.y, tile);
        }

        return road;
    }

    private List<Vector2Int> GetValidRuralNeighbors(
        IGrid<RoadTileData> grid,
        Vector2Int position,
        HashSet<Vector2Int> validCells,
        HashSet<Vector2Int> visited)
    {
        var neighbors = new List<Vector2Int>();

        foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
        {
            var neighbor = GridUtility.GetNeighborInDirection(position, dir);

            if (!grid.IsValidPosition(neighbor.x, neighbor.y)) continue;
            if (!validCells.Contains(neighbor)) continue;
            if (visited.Contains(neighbor)) continue;
            if (grid.Get(neighbor.x, neighbor.y).Type != RoadTileType.None) continue;

            neighbors.Add(neighbor);
        }

        return neighbors;
    }

    private Vector2Int ChooseRuralDirection(
        Vector2Int current,
        List<Vector2Int> road,
        List<Vector2Int> neighbors,
        System.Random random)
    {
        // Try to continue in same direction (creates straight segments)
        if (road.Count >= 2)
        {
            var previous = road[road.Count - 2];
            var direction = current - previous;
            var continuation = current + direction;

            if (neighbors.Contains(continuation))
            {
                // 70% chance to continue straight
                if (random.NextDouble() < 0.7)
                {
                    return continuation;
                }
            }
        }

        // Otherwise pick random
        return neighbors[random.Next(neighbors.Count)];
    }
}

// Industrial: Regular wide-spaced grid
public class IndustrialRoadGenerator : IDistrictRoadGenerator
{
    public List<Vector2Int> GenerateDistrictRoads(
        IGrid<RoadTileData> grid,
        District district,
        RoadGenerationConfig config,
        System.Random random)
    {
        var roads = new List<Vector2Int>();

        // Early exit: empty district
        if (district.Bounds.Count == 0) return roads;

        // Similar to downtown but with wider spacing
        var bounds = GetBoundingBox(district.Bounds);

        int spacing = 5 + random.Next(2); // 5-6 cells (wider than downtown)

        // Horizontal roads
        for (int y = bounds.yMin; y <= bounds.yMax; y += spacing)
        {
            for (int x = bounds.xMin; x <= bounds.xMax; x++)
            {
                var pos = new Vector2Int(x, y);

                if (district.Bounds.Contains(pos) &&
                    grid.Get(x, y).Type == RoadTileType.None)
                {
                    roads.Add(pos);
                }
            }
        }

        // Vertical roads
        for (int x = bounds.xMin; x <= bounds.xMax; x += spacing)
        {
            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                var pos = new Vector2Int(x, y);

                if (district.Bounds.Contains(pos) &&
                    grid.Get(x, y).Type == RoadTileType.None)
                {
                    roads.Add(pos);
                }
            }
        }

        return roads;
    }

    private (int xMin, int xMax, int yMin, int yMax) GetBoundingBox(List<Vector2Int> positions)
    {
        int xMin = int.MaxValue, xMax = int.MinValue;
        int yMin = int.MaxValue, yMax = int.MinValue;

        foreach (var pos in positions)
        {
            if (pos.x < xMin) xMin = pos.x;
            if (pos.x > xMax) xMax = pos.x;
            if (pos.y < yMin) yMin = pos.y;
            if (pos.y > yMax) yMax = pos.y;
        }

        return (xMin, xMax, yMin, yMax);
    }
}
```

### Main Orchestrator: HierarchicalRoadGenerator

**Purpose**: Replace `BiasedRandomWalkGenerator` as the main `IPathGenerator` implementation.

```csharp
public class HierarchicalRoadGenerator : IPathGenerator
{
    private readonly IPathGenerator _arteryGenerator;
    private readonly AdditionalArteryGenerator _additionalArteryGenerator;
    private readonly IDistrictGenerator _districtGenerator;
    private readonly Dictionary<DistrictType, IDistrictRoadGenerator> _districtRoadGenerators;
    private List<District> _lastGeneratedDistricts;

    // Constructor with DI
    public HierarchicalRoadGenerator(
        IPathGenerator arteryGenerator,
        IDistrictGenerator districtGenerator,
        Dictionary<DistrictType, IDistrictRoadGenerator> districtRoadGenerators)
    {
        _arteryGenerator = arteryGenerator ?? throw new ArgumentNullException(nameof(arteryGenerator));
        _districtGenerator = districtGenerator ?? throw new ArgumentNullException(nameof(districtGenerator));
        _districtRoadGenerators = districtRoadGenerators ?? throw new ArgumentNullException(nameof(districtRoadGenerators));
        _additionalArteryGenerator = new AdditionalArteryGenerator(); // Internal utility, not DI
    }

    // Parameterless constructor for backward compatibility (uses defaults)
    public HierarchicalRoadGenerator()
        : this(
            new MainArteryGenerator(),
            new VoronoiDistrictGenerator(),
            new Dictionary<DistrictType, IDistrictRoadGenerator>
            {
                { DistrictType.Downtown, new DowntownRoadGenerator() },
                { DistrictType.Suburban, new SuburbanRoadGenerator() },
                { DistrictType.Rural, new RuralRoadGenerator() },
                { DistrictType.Industrial, new IndustrialRoadGenerator() }
            })
    {
    }

    public List<District> GetGeneratedDistricts()
    {
        return _lastGeneratedDistricts != null
            ? new List<District>(_lastGeneratedDistricts)
            : new List<District>();
    }

    public List<Vector2Int> GeneratePath(
        IGrid<RoadTileData> grid,
        Vector2Int start,
        Vector2Int end,
        RoadGenerationConfig config,
        System.Random random)
    {
        // This method now orchestrates the entire hierarchical generation
        // It returns the main path for compatibility, but generates entire network

        // Pass 1: Generate main artery (guaranteed start-to-exit path)
        var mainArtery = _arteryGenerator.GeneratePath(grid, start, end, config, random);

        // Early exit: failed to generate main path
        if (mainArtery.Count == 0)
        {
            return new List<Vector2Int>();
        }

        // Mark main artery in grid
        MarkPathInGrid(grid, mainArtery, isMainPath: true);

        // Pass 2: Generate additional major roads (optional)
        var additionalArteries = _additionalArteryGenerator.GenerateAdditionalArteries(
            grid, mainArtery, config, random);

        foreach (var artery in additionalArteries)
        {
            MarkPathInGrid(grid, artery, isMainPath: true);
        }

        // Pass 3: Generate districts
        var districts = _districtGenerator.GenerateDistricts(grid, mainArtery, config, random);

        // Store districts for later retrieval
        _lastGeneratedDistricts = districts;

        // Pass 4: Generate roads within each district
        foreach (var district in districts)
        {
            // Get appropriate generator for this district type
            if (!_districtRoadGenerators.TryGetValue(district.Type, out var generator))
            {
                continue; // Skip unknown types
            }

            // Generate roads for this district
            var districtRoads = generator.GenerateDistrictRoads(grid, district, config, random);

            // Mark district roads in grid
            MarkPathInGrid(grid, districtRoads, isMainPath: false);
        }

        // Return main artery for compatibility with existing system
        return mainArtery;
    }

    private void MarkPathInGrid(IGrid<RoadTileData> grid, List<Vector2Int> path, bool isMainPath)
    {
        foreach (var pos in path)
        {
            var tile = grid.Get(pos.x, pos.y);

            // Early exit: already special tile
            if (tile.Type == RoadTileType.Start || tile.Type == RoadTileType.Exit)
            {
                continue;
            }

            // Mark as occupied (type will be determined later)
            tile.Type = RoadTileType.Straight; // Placeholder
            tile.IsMainPath = isMainPath;
            tile.GridPosition = pos;

            grid.Set(pos.x, pos.y, tile);
        }
    }
}
```

## Integration with Existing System

### Update DI Registration

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
public static void Init()
{
    var serviceCollection = new ServiceCollection();

    // Register both path generators
    serviceCollection.AddTransient<BiasedRandomWalkGenerator>();
    serviceCollection.AddTransient<HierarchicalRoadGenerator>();

    // Register main IPathGenerator (defaults to hierarchical)
    serviceCollection.AddTransient<IPathGenerator>(sp => sp.GetRequiredService<HierarchicalRoadGenerator>());

    // Branch generator kept for fallback compatibility
    serviceCollection.AddTransient<IBranchGenerator, RandomBranchGenerator>();

    // Road spawner
    serviceCollection.AddTransient<IRoadSpawner, UnityRoadSpawner>();

    // Register district generators for DI injection into HierarchicalRoadGenerator
    serviceCollection.AddTransient<IDistrictGenerator, VoronoiDistrictGenerator>();
    serviceCollection.AddTransient<MainArteryGenerator>();

    // Register district-specific road generators
    serviceCollection.AddTransient<DowntownRoadGenerator>();
    serviceCollection.AddTransient<SuburbanRoadGenerator>();
    serviceCollection.AddTransient<RuralRoadGenerator>();
    serviceCollection.AddTransient<IndustrialRoadGenerator>();

    _serviceProvider = serviceCollection.BuildServiceProvider();

    Debug.Log("DI Container initialized with Hierarchical Road Generation");
}
```

### ProceduralRoads Changes

The `ProceduralRoads` MonoBehaviour requires minimal changes:

```csharp
public void Initialize(
    IPathGenerator pathGenerator = null,
    IBranchGenerator branchGenerator = null,
    IRoadSpawner roadSpawner = null)
{
    // Check if we should use hierarchical or fallback generator
    if (config.useHierarchicalGeneration)
    {
        _pathGenerator = pathGenerator ?? DI.GetService<HierarchicalRoadGenerator>();
        _branchGenerator = null; // Not used in hierarchical mode
    }
    else
    {
        // Fallback mode
        _pathGenerator = pathGenerator ?? DI.GetService<BiasedRandomWalkGenerator>();
        _branchGenerator = branchGenerator ?? DI.GetService<IBranchGenerator>();
    }

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

    // Mark main path in grid (if not already done by hierarchical generator)
    if (!config.useHierarchicalGeneration)
    {
        MarkPathInGrid(_mainPath, isMainPath: true);
    }

    // Branch generation (only for non-hierarchical mode)
    if (!config.useHierarchicalGeneration && _branchGenerator != null)
    {
        var branches = _branchGenerator.GenerateBranches(_grid, _mainPath, config, _random);
        MarkPathInGrid(branches, isMainPath: false);
    }

    // Determine tile types
    FillTileTypes();

    // Spawn prefabs
    CreateRoadContainer();
    _roadSpawner.SpawnRoads(_grid, config, _roadContainer.transform);

    string mode = config.useHierarchicalGeneration ? "hierarchical" : "standard";
    Debug.Log($"Generated {mode} road network with main path: {_mainPath.Count} tiles");
}
```

## District Data Access

### Public API for Building Placement

Add to `ProceduralRoads`:

```csharp
private List<District> _districts;

public List<District> GetDistricts()
{
    return _districts != null ? new List<District>(_districts) : new List<District>();
}

public District GetDistrictAt(int x, int y)
{
    // Early exit: no districts
    if (_districts == null) return null;

    var position = new Vector2Int(x, y);

    foreach (var district in _districts)
    {
        if (district.Bounds.Contains(position))
        {
            return district;
        }
    }

    return null;
}

public DistrictType? GetDistrictTypeAt(int x, int y)
{
    var district = GetDistrictAt(x, y);
    return district?.Type;
}
```

Store districts after generation:

```csharp
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

        // Generate path (which generates entire network for hierarchical)
        _mainPath = _pathGenerator.GeneratePath(_grid, startPos, exitPos, config, _random);

        // If using hierarchical generator, extract district data
        if (config.useHierarchicalGeneration && _pathGenerator is HierarchicalRoadGenerator hierarchicalGen)
        {
            _districts = hierarchicalGen.GetGeneratedDistricts();
        }

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
```

The `HierarchicalRoadGenerator` already stores districts in the updated implementation above (see line where `_lastGeneratedDistricts = districts;` is set in Pass 3).

## File Structure

```
Assets/
└── Scripts/
    ├── Core/
    │   └── DI.cs
    │
    └── Roads/
        ├── Data/
        │   ├── RoadTileType.cs (updated with DeadEnd)
        │   ├── GridEdge.cs
        │   ├── Direction.cs
        │   ├── RoadTileData.cs
        │   ├── RoadGenerationConfig.cs (updated)
        │   ├── District.cs (NEW)
        │   └── DistrictType.cs (NEW)
        │
        ├── Interfaces/
        │   ├── IGrid.cs
        │   ├── IPathGenerator.cs
        │   ├── IBranchGenerator.cs (deprecated)
        │   ├── IRoadSpawner.cs
        │   ├── IDistrictGenerator.cs (NEW)
        │   └── IDistrictRoadGenerator.cs (NEW)
        │
        ├── Implementations/
        │   ├── Grid2D.cs
        │   ├── BiasedRandomWalkGenerator.cs (kept for fallback)
        │   ├── HierarchicalRoadGenerator.cs (NEW - main)
        │   ├── MainArteryGenerator.cs (NEW)
        │   ├── AdditionalArteryGenerator.cs (NEW)
        │   ├── VoronoiDistrictGenerator.cs (NEW)
        │   ├── DowntownRoadGenerator.cs (NEW)
        │   ├── SuburbanRoadGenerator.cs (NEW)
        │   ├── RuralRoadGenerator.cs (NEW)
        │   ├── IndustrialRoadGenerator.cs (NEW)
        │   └── UnityRoadSpawner.cs
        │
        ├── Utilities/
        │   ├── GridUtility.cs
        │   ├── RoadTileUtility.cs
        │   └── RoadTileDataExtensions.cs
        │
        └── ProceduralRoads.cs (updated)
```

## Implementation Checklist

### Phase 1: Data Structures
- [ ] Create `DistrictType` enum
- [ ] Create `District` class
- [ ] Update `RoadGenerationConfig` with hierarchical settings
- [ ] Create `DistrictProbabilities` class

### Phase 2: New Interfaces
- [ ] Create `IDistrictGenerator` interface
- [ ] Create `IDistrictRoadGenerator` interface

### Phase 3: Main Artery Generation
- [ ] Implement `MainArteryGenerator` (guaranteed start-to-exit)
- [ ] Test main artery generation in isolation
- [ ] Implement `AdditionalArteryGenerator`
- [ ] Test with additional arteries

### Phase 4: District Subdivision
- [ ] Implement `VoronoiDistrictGenerator`
- [ ] Test district generation visually (debug render)
- [ ] Verify districts don't overlap

### Phase 5: District-Specific Generators
- [ ] Implement `DowntownRoadGenerator` (grid pattern)
- [ ] Implement `SuburbanRoadGenerator` (organic with cul-de-sacs)
- [ ] Implement `RuralRoadGenerator` (sparse winding)
- [ ] Implement `IndustrialRoadGenerator` (wide-spaced grid)
- [ ] Test each generator individually

### Phase 6: Orchestration
- [ ] Implement `HierarchicalRoadGenerator` (orchestrator)
- [ ] Integrate with existing `ProceduralRoads` MonoBehaviour
- [ ] Update DI registration
- [ ] Remove branch generator dependency

### Phase 7: District Data Access
- [ ] Add district storage to `ProceduralRoads`
- [ ] Implement `GetDistricts()` API
- [ ] Implement `GetDistrictAt()` API
- [ ] Implement `GetDistrictTypeAt()` API

### Phase 8: Testing & Polish
- [ ] Test with small grids (10x10)
- [ ] Test with medium grids (25x25)
- [ ] Test with large grids (50x50)
- [ ] Verify start-to-exit path always exists
- [ ] Verify districts look visually distinct
- [ ] Test building placement using district data
- [ ] Optimize performance if needed

## Benefits Over Previous System

1. **Guaranteed start-to-exit path** - Main artery generated first, never modified
2. **Realistic road layouts** - Different patterns for different district types
3. **Building placement guidance** - Know which district type each cell belongs to
4. **Scalable** - Works on any grid size with appropriate density settings
5. **Modular** - Easy to add new district types or road patterns via DI
6. **Maintains existing architecture** - Still uses DI, interfaces, pure functions
7. **Backward compatible** - Can toggle between hierarchical and simple generation via config flag

## Configuration Examples

### Small Town (20x20)
```csharp
config.targetDistrictCount = 3;
config.additionalMajorRoads = 0;
config.districtProbabilities.downtown = 0.33f;
config.districtProbabilities.suburban = 0.5f;
config.districtProbabilities.rural = 0.17f;
config.districtProbabilities.industrial = 0f;
```

### Medium City (40x40)
```csharp
config.targetDistrictCount = 5;
config.additionalMajorRoads = 1;
config.districtProbabilities.downtown = 0.3f;
config.districtProbabilities.suburban = 0.4f;
config.districtProbabilities.rural = 0.2f;
config.districtProbabilities.industrial = 0.1f;
```

### Large Metropolis (60x60)
```csharp
config.targetDistrictCount = 8;
config.additionalMajorRoads = 2;
config.districtProbabilities.downtown = 0.35f;
config.districtProbabilities.suburban = 0.35f;
config.districtProbabilities.rural = 0.15f;
config.districtProbabilities.industrial = 0.15f;
```

## Next Steps

1. Review this design document
2. Approve architecture and approach
3. Begin implementation starting with Phase 1 (data structures)
4. Implement incrementally, testing each phase
5. Integrate with building placement system once roads are working
