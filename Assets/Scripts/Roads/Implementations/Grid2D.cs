using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roads
{
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
}
