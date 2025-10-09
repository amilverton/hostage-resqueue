using System;
using System.Collections.Generic;

namespace Core.Example
{
    public delegate void OnValueChanged<T>(T oldValue, T newValue);

    public class EventVariable<T> : IDisposable
    {
        private T _value;
        private readonly bool _invokeOnlyOnChange;

        public T Value => _value;

        public static implicit operator T(EventVariable<T> variable) => variable._value;
        public static implicit operator EventVariable<T>(T value) => new EventVariable<T>(value);

        // Strongly-typed event for better type safety
        public event OnValueChanged<T> OnValueChanged;

        private void InvokeValueChanged(T oldValue, T newValue)
        {
            OnValueChanged?.Invoke(oldValue, newValue);
        }

        /// <summary>
        /// Creates an EventVariable with default value.
        /// </summary>
        /// <param name="invokeOnlyOnChange">If true, only invokes event when value actually changes</param>
        public EventVariable(bool invokeOnlyOnChange = true)
        {
            _invokeOnlyOnChange = invokeOnlyOnChange;
            _value = default;
        }

        /// <summary>
        /// Creates an EventVariable with an initial value.
        /// </summary>
        /// <param name="value">Initial value</param>
        /// <param name="invokeOnlyOnChange">If true, only invokes event when value actually changes</param>
        /// <param name="invokeImmediately">If true, invokes OnValueChanged immediately with initial value</param>
        public EventVariable(T value, bool invokeOnlyOnChange = true, bool invokeImmediately = false)
        {
            _invokeOnlyOnChange = invokeOnlyOnChange;
            _value = value;

            if (invokeImmediately)
            {
                InvokeValueChanged(default, value);
            }
        }

        /// <summary>
        /// Sets a new value and optionally triggers the OnValueChanged event.
        /// </summary>
        public void SetValue(T value, bool forceInvoke = false)
        {
            T oldValue = _value;

            // Check if value actually changed
            bool hasChanged = !EqualityComparer<T>.Default.Equals(_value, value);

            _value = value;

            // Invoke event based on settings
            if (forceInvoke || !_invokeOnlyOnChange || hasChanged)
            {
                InvokeValueChanged(oldValue, value);
            }
        }

        /// <summary>
        /// Gets the current value.
        /// </summary>
        public T GetValue() => _value;

        /// <summary>
        /// Sets value without triggering events (silent update).
        /// </summary>
        public void SetValueSilent(T value)
        {
            _value = value;
        }

        public void Dispose()
        {
            OnValueChanged = null;
            _value = default;
        }

        public override string ToString() => _value?.ToString() ?? "null";
    }
}