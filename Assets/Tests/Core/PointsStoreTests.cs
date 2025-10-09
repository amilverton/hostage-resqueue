using NUnit.Framework;
using Roads.Example;

namespace Tests.Core
{
    public class PointsStoreTests
    {
        [Test]
        public void PointsStore_Constructor_InitializesPointsToZero()
        {
            // Arrange & Act
            var store = new PointsStore();

            // Assert
            Assert.AreEqual(0, store.Points.Value);
        }

        [Test]
        public void PointsStore_SetPoints_UpdatesPoints()
        {
            // Arrange
            var store = new PointsStore();

            // Act
            store.SetPoints(100);

            // Assert
            Assert.AreEqual(100, store.Points.Value);
        }

        [Test]
        public void PointsStore_SetPoints_TriggersEvent()
        {
            // Arrange
            var store = new PointsStore();
            int eventCallCount = 0;
            int receivedValue = -1;

            store.Points.OnValueChanged += (oldVal, newVal) =>
            {
                eventCallCount++;
                receivedValue = newVal;
            };

            // Act
            store.SetPoints(50);

            // Assert
            Assert.AreEqual(1, eventCallCount);
            Assert.AreEqual(50, receivedValue);
        }

        [Test]
        public void PointsStore_SetPoints_WithNegativeValue_ClampsToZero()
        {
            // Arrange
            var store = new PointsStore();

            // Act
            store.SetPoints(-100);

            // Assert
            Assert.AreEqual(0, store.Points.Value);
        }

        [Test]
        public void PointsStore_SetPoints_WithMaxValue_Works()
        {
            // Arrange
            var store = new PointsStore();

            // Act
            store.SetPoints(int.MaxValue);

            // Assert
            Assert.AreEqual(int.MaxValue, store.Points.Value);
        }

        [Test]
        public void PointsStore_SaveState_ReturnsCurrentState()
        {
            // Arrange
            var store = new PointsStore();
            store.SetPoints(250);

            // Act
            var state = store.SaveState();

            // Assert
            Assert.IsNotNull(state);
            Assert.AreEqual(250, state.points);
        }

        [Test]
        public void PointsStore_LoadState_RestoresState()
        {
            // Arrange
            var store = new PointsStore();
            var state = new PointsStoreState(500);

            // Act
            store.LoadState(state);

            // Assert
            Assert.AreEqual(500, store.Points.Value);
        }

        [Test]
        public void PointsStore_LoadState_WithNull_SetsToZero()
        {
            // Arrange
            var store = new PointsStore();
            store.SetPoints(100); // Set to non-zero first

            // Act
            store.LoadState(null);

            // Assert
            Assert.AreEqual(0, store.Points.Value);
        }

        [Test]
        public void PointsStore_SaveAndLoad_Roundtrip_PreservesData()
        {
            // Arrange
            var store1 = new PointsStore();
            store1.SetPoints(777);

            // Act - Save from first store
            var state = store1.SaveState();

            // Act - Load into second store
            var store2 = new PointsStore();
            store2.LoadState(state);

            // Assert
            Assert.AreEqual(777, store2.Points.Value);
        }

        [Test]
        public void PointsStore_Dispose_CleansUpResources()
        {
            // Arrange
            var store = new PointsStore();
            int eventCallCount = 0;

            store.Points.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            store.Dispose();
            store.SetPoints(100);

            // Assert
            Assert.AreEqual(100, store.Points.Value);
            Assert.AreEqual(0, eventCallCount); // Event should not fire after disposal
        }

        [Test]
        public void PointsStoreState_DefaultConstructor_InitializesToZero()
        {
            // Arrange & Act
            var state = new PointsStoreState();

            // Assert
            Assert.AreEqual(0, state.points);
        }

        [Test]
        public void PointsStoreState_ConstructorWithValue_SetsValue()
        {
            // Arrange & Act
            var state = new PointsStoreState(999);

            // Assert
            Assert.AreEqual(999, state.points);
        }

        [Test]
        public void PointsStore_SetSameValue_DoesNotTriggerEventTwice()
        {
            // Arrange
            var store = new PointsStore();
            store.SetPoints(100);

            int eventCallCount = 0;
            store.Points.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            store.SetPoints(100);

            // Assert
            Assert.AreEqual(0, eventCallCount); // Should not trigger because value didn't change
        }

        [Test]
        public void PointsStore_LoadState_WithNegativePoints_ClampsToZero()
        {
            // Arrange
            var store = new PointsStore();
            var state = new PointsStoreState(-50);

            // Act
            store.LoadState(state);

            // Assert
            Assert.AreEqual(0, store.Points.Value);
        }
    }
}
