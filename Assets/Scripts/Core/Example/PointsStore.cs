using System;
using Core.Example;
using UnityEngine;

namespace Roads.Example
{
    public interface IPointsStore
    {
        EventVariable<int> Points { get; }
        void SetPoints(int points);
        void LoadState(PointsStoreState state);
        PointsStoreState SaveState();
    }

    /// <summary>
    /// Serializable state for persistence (save/load system).
    /// </summary>
    [Serializable]
    public class PointsStoreState
    {
        public int points;

        public PointsStoreState()
        {
            points = 0;
        }

        public PointsStoreState(int points)
        {
            this.points = points;
        }
    }

    public class PointsStore : IPointsStore, IDisposable
    {
        public EventVariable<int> Points { get; private set; }

        private const int MinPoints = 0;
        private const int MaxPoints = int.MaxValue;

        public PointsStore()
        {
            // Set defaults - using invokeOnlyOnChange to prevent duplicate events
            Points = new EventVariable<int>(0, invokeOnlyOnChange: true);
        }

        /// <summary>
        /// Sets points with validation. Clamps between MinPoints and MaxPoints.
        /// </summary>
        public void SetPoints(int points)
        {
            // Clamp to valid range
            int clampedPoints = Mathf.Clamp(points, MinPoints, MaxPoints);
            Points.SetValue(clampedPoints);
        }

        /// <summary>
        /// Loads state from a saved state object.
        /// </summary>
        public void LoadState(PointsStoreState state)
        {
            if (state == null)
            {
                SetPoints(0);
                return;
            }

            SetPoints(state.points);
        }

        /// <summary>
        /// Saves current state to a serializable object.
        /// </summary>
        public PointsStoreState SaveState()
        {
            var state = new PointsStoreState(Points.Value);
            return state;
        }

        // For disposing on game cleanup
        public void Dispose()
        {
            Points?.Dispose();
        }
    }
}