using UnityEngine;

namespace Roads
{
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
}
