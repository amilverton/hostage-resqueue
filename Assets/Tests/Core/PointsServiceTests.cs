using NUnit.Framework;
using Roads.Example;

namespace Tests.Core
{
    public class PointsServiceTests
    {
        private IPointsStore _store;
        private IPointsService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new PointsStore();
            _service = new PointsService(_store);
        }

        [TearDown]
        public void TearDown()
        {
            (_store as PointsStore)?.Dispose();
        }

        [Test]
        public void PointsService_GetPoints_ReturnsCurrentPoints()
        {
            // Arrange
            _store.SetPoints(150);

            // Act
            int points = _service.GetPoints();

            // Assert
            Assert.AreEqual(150, points);
        }

        [Test]
        public void PointsService_AddPoints_IncreasesPoints()
        {
            // Arrange
            _store.SetPoints(50);

            // Act
            _service.AddPoints(25);

            // Assert
            Assert.AreEqual(75, _service.GetPoints());
        }

        [Test]
        public void PointsService_AddPoints_WithZero_DoesNotChange()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            _service.AddPoints(0);

            // Assert
            Assert.AreEqual(100, _service.GetPoints());
        }

        [Test]
        public void PointsService_AddPoints_WithNegative_DoesNotAdd()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            _service.AddPoints(-50);

            // Assert
            Assert.AreEqual(100, _service.GetPoints()); // Should remain unchanged
        }

        [Test]
        public void PointsService_AddPoints_LargeValue_HandlesOverflow()
        {
            // Arrange
            _store.SetPoints(int.MaxValue - 10);

            // Act
            _service.AddPoints(20); // This would overflow

            // Assert
            Assert.AreEqual(int.MaxValue, _service.GetPoints()); // Should cap at max
        }

        [Test]
        public void PointsService_RemovePoints_DecreasesPoints()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            _service.RemovePoints(30);

            // Assert
            Assert.AreEqual(70, _service.GetPoints());
        }

        [Test]
        public void PointsService_RemovePoints_WithZero_DoesNotChange()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            _service.RemovePoints(0);

            // Assert
            Assert.AreEqual(100, _service.GetPoints());
        }

        [Test]
        public void PointsService_RemovePoints_WithNegative_DoesNotRemove()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            _service.RemovePoints(-50);

            // Assert
            Assert.AreEqual(100, _service.GetPoints()); // Should remain unchanged
        }

        [Test]
        public void PointsService_RemovePoints_MoreThanAvailable_ClampsToZero()
        {
            // Arrange
            _store.SetPoints(50);

            // Act
            _service.RemovePoints(100);

            // Assert
            Assert.AreEqual(0, _service.GetPoints());
        }

        [Test]
        public void PointsService_CanAfford_WithSufficientPoints_ReturnsTrue()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            bool result = _service.CanAfford(50);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void PointsService_CanAfford_WithExactPoints_ReturnsTrue()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            bool result = _service.CanAfford(100);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void PointsService_CanAfford_WithInsufficientPoints_ReturnsFalse()
        {
            // Arrange
            _store.SetPoints(50);

            // Act
            bool result = _service.CanAfford(100);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void PointsService_TrySpendPoints_WithSufficientPoints_ReturnsTrue()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            bool result = _service.TrySpendPoints(30);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(70, _service.GetPoints());
        }

        [Test]
        public void PointsService_TrySpendPoints_WithInsufficientPoints_ReturnsFalse()
        {
            // Arrange
            _store.SetPoints(50);

            // Act
            bool result = _service.TrySpendPoints(100);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(50, _service.GetPoints()); // Points should remain unchanged
        }

        [Test]
        public void PointsService_TrySpendPoints_WithNegativeCost_ReturnsFalse()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            bool result = _service.TrySpendPoints(-50);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(100, _service.GetPoints()); // Points should remain unchanged
        }

        [Test]
        public void PointsService_TrySpendPoints_WithZeroCost_ReturnsTrue()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            bool result = _service.TrySpendPoints(0);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(100, _service.GetPoints());
        }

        [Test]
        public void PointsService_ResetPoints_SetsPointsToZero()
        {
            // Arrange
            _store.SetPoints(500);

            // Act
            _service.ResetPoints();

            // Assert
            Assert.AreEqual(0, _service.GetPoints());
        }

        [Test]
        public void PointsService_MultipleOperations_WorksCorrectly()
        {
            // Arrange
            _store.SetPoints(0);

            // Act
            _service.AddPoints(100);
            _service.RemovePoints(20);
            _service.AddPoints(50);
            bool canAfford = _service.CanAfford(100);
            bool spent = _service.TrySpendPoints(30);

            // Assert
            Assert.AreEqual(100, _service.GetPoints());
            Assert.IsTrue(canAfford);
            Assert.IsTrue(spent);
        }

        [Test]
        public void PointsService_EventPropagation_TriggersOnStoreChanges()
        {
            // Arrange
            int eventCallCount = 0;
            _store.Points.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            _service.AddPoints(50);
            _service.RemovePoints(10);
            _service.TrySpendPoints(20);
            _service.ResetPoints();

            // Assert
            Assert.AreEqual(4, eventCallCount); // Should trigger 4 times
        }

        [Test]
        public void PointsService_TrySpendPoints_ExactAmount_Success()
        {
            // Arrange
            _store.SetPoints(100);

            // Act
            bool result = _service.TrySpendPoints(100);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _service.GetPoints());
        }
    }
}
