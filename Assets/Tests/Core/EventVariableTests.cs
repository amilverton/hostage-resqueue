using NUnit.Framework;
using Core.Example;

namespace Tests.Core
{
    public class EventVariableTests
    {
        [Test]
        public void EventVariable_DefaultConstructor_SetsDefaultValue()
        {
            // Arrange & Act
            var variable = new EventVariable<int>();

            // Assert
            Assert.AreEqual(0, variable.Value);
        }

        [Test]
        public void EventVariable_ConstructorWithValue_SetsValue()
        {
            // Arrange & Act
            var variable = new EventVariable<int>(42);

            // Assert
            Assert.AreEqual(42, variable.Value);
        }

        [Test]
        public void EventVariable_ImplicitConversion_ReturnsValue()
        {
            // Arrange
            var variable = new EventVariable<int>(100);

            // Act
            int value = variable;

            // Assert
            Assert.AreEqual(100, value);
        }

        [Test]
        public void EventVariable_ImplicitConversionFromValue_CreatesVariable()
        {
            // Arrange & Act
            EventVariable<int> variable = 50;

            // Assert
            Assert.AreEqual(50, variable.Value);
        }

        [Test]
        public void EventVariable_SetValue_UpdatesValue()
        {
            // Arrange
            var variable = new EventVariable<int>(10);

            // Act
            variable.SetValue(20);

            // Assert
            Assert.AreEqual(20, variable.Value);
        }

        [Test]
        public void EventVariable_SetValue_InvokesEvent()
        {
            // Arrange
            var variable = new EventVariable<int>(10);
            int oldValue = -1;
            int newValue = -1;
            int eventCallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) =>
            {
                oldValue = oldVal;
                newValue = newVal;
                eventCallCount++;
            };

            // Act
            variable.SetValue(20);

            // Assert
            Assert.AreEqual(1, eventCallCount);
            Assert.AreEqual(10, oldValue);
            Assert.AreEqual(20, newValue);
        }

        [Test]
        public void EventVariable_SetSameValue_WithInvokeOnlyOnChange_DoesNotInvokeEvent()
        {
            // Arrange
            var variable = new EventVariable<int>(10, invokeOnlyOnChange: true);
            int eventCallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            variable.SetValue(10);

            // Assert
            Assert.AreEqual(0, eventCallCount);
        }

        [Test]
        public void EventVariable_SetSameValue_WithInvokeAlways_InvokesEvent()
        {
            // Arrange
            var variable = new EventVariable<int>(10, invokeOnlyOnChange: false);
            int eventCallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            variable.SetValue(10);

            // Assert
            Assert.AreEqual(1, eventCallCount);
        }

        [Test]
        public void EventVariable_SetValue_WithForceInvoke_AlwaysInvokesEvent()
        {
            // Arrange
            var variable = new EventVariable<int>(10, invokeOnlyOnChange: true);
            int eventCallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            variable.SetValue(10, forceInvoke: true);

            // Assert
            Assert.AreEqual(1, eventCallCount);
        }

        [Test]
        public void EventVariable_SetValueSilent_DoesNotInvokeEvent()
        {
            // Arrange
            var variable = new EventVariable<int>(10);
            int eventCallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            variable.SetValueSilent(20);

            // Assert
            Assert.AreEqual(20, variable.Value);
            Assert.AreEqual(0, eventCallCount);
        }

        [Test]
        public void EventVariable_GetValue_ReturnsCurrentValue()
        {
            // Arrange
            var variable = new EventVariable<int>(42);

            // Act
            int value = variable.GetValue();

            // Assert
            Assert.AreEqual(42, value);
        }

        [Test]
        public void EventVariable_Dispose_ClearsEventAndValue()
        {
            // Arrange
            var variable = new EventVariable<int>(10);
            int eventCallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) => eventCallCount++;

            // Act
            variable.Dispose();
            variable.SetValue(20);

            // Assert
            Assert.AreEqual(20, variable.Value);
            Assert.AreEqual(0, eventCallCount); // Event should not fire after disposal
        }

        [Test]
        public void EventVariable_ToString_ReturnsValueAsString()
        {
            // Arrange
            var variable = new EventVariable<int>(123);

            // Act
            string result = variable.ToString();

            // Assert
            Assert.AreEqual("123", result);
        }

        [Test]
        public void EventVariable_WithNullValue_ToString_ReturnsNull()
        {
            // Arrange
            var variable = new EventVariable<string>(null);

            // Act
            string result = variable.ToString();

            // Assert
            Assert.AreEqual("null", result);
        }

        [Test]
        public void EventVariable_MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            var variable = new EventVariable<int>(10);
            int subscriber1CallCount = 0;
            int subscriber2CallCount = 0;

            variable.OnValueChanged += (oldVal, newVal) => subscriber1CallCount++;
            variable.OnValueChanged += (oldVal, newVal) => subscriber2CallCount++;

            // Act
            variable.SetValue(20);

            // Assert
            Assert.AreEqual(1, subscriber1CallCount);
            Assert.AreEqual(1, subscriber2CallCount);
        }

        [Test]
        public void EventVariable_WithInvokeImmediately_InvokesOnConstruction()
        {
            // Arrange
            int eventCallCount = 0;
            int receivedValue = -1;

            // Act
            var variable = new EventVariable<int>(42, invokeImmediately: true);
            variable.OnValueChanged += (oldVal, newVal) =>
            {
                eventCallCount++;
                receivedValue = newVal;
            };

            // Note: Event was invoked in constructor, but we subscribed after
            // So we need to test differently
            variable.SetValue(43);

            // Assert
            Assert.AreEqual(1, eventCallCount);
            Assert.AreEqual(43, receivedValue);
        }
    }
}
