using System;
using UnityEngine;

namespace Roads
{
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
        public GameObject deadEndPrefab;     // Single-connection road terminus
        public GameObject startPrefab;       // Entry point marker
        public GameObject exitPrefab;        // Exit point marker

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
}
