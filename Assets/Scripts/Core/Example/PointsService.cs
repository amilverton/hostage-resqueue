using UnityEngine;

namespace Roads.Example
{
    public interface IPointsService
    {
        int GetPoints();
        void AddPoints(int points);
        void RemovePoints(int points);
        bool CanAfford(int cost);
        bool TrySpendPoints(int cost);
        void ResetPoints();
    }


    public class PointsService : IPointsService
    {
        private readonly IPointsStore _pointsStore;

        public PointsService(IPointsStore pointsStore)
        {
            _pointsStore = pointsStore;
        }


        public int GetPoints()
        {
            return _pointsStore.Points;
        }

        /// <summary>
        /// Adds points to the current total. Validates that points is non-negative.
        /// </summary>
        public void AddPoints(int points)
        {
            if (points < 0)
            {
                return;
            }

            var currentPoints = _pointsStore.Points;
            var newPoints = currentPoints + points;

            // Prevent overflow
            if (newPoints < currentPoints)
            {
                newPoints = int.MaxValue;
            }

            _pointsStore.SetPoints(newPoints);
        }

        /// <summary>
        /// Removes points from the current total. Will not go below zero.
        /// </summary>
        public void RemovePoints(int points)
        {
            if (points < 0)
            {
                return;
            }

            var currentPoints = _pointsStore.Points;
            var newPoints = Mathf.Max(0, currentPoints - points);
            _pointsStore.SetPoints(newPoints);
        }

        /// <summary>
        /// Checks if the player has enough points to afford a cost.
        /// </summary>
        public bool CanAfford(int cost)
        {
            return _pointsStore.Points >= cost;
        }

        /// <summary>
        /// Attempts to spend points. Returns true if successful, false if insufficient points.
        /// </summary>
        public bool TrySpendPoints(int cost)
        {
            if (cost < 0)
            {
                return false;
            }

            if (!CanAfford(cost))
            {
                return false;
            }

            RemovePoints(cost);
            return true;
        }

        /// <summary>
        /// Resets points to zero.
        /// </summary>
        public void ResetPoints()
        {
            _pointsStore.SetPoints(0);
        }
    }
}