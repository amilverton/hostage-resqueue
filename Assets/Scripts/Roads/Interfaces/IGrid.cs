using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
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
}
