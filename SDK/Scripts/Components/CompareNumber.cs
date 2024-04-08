using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Compare two number values.
    /// </summary>
    public class CompareNumber : MonoBehaviour
    {
        /// <summary>
        /// The comparison type.
        /// </summary>
        [Flags]
        public enum ComparisonType
        {
            Equals = 1,
            LessThan = 2,
            GreaterThan = 4,
        }

        [Tooltip("The target of the comparison.")]
        public float targetValue;
        [Tooltip("The comparison type.")]
        public ComparisonType comparisonType;
        [Tooltip("Invoked when the comparison is true.")]
        public UnityEvent onTrue;
        [Tooltip("Invoked when the comparison is false.")]
        public UnityEvent onFalse;
        [Tooltip("Invoked with the true/false value of the comparison.")]
        public UnityEvent<bool> onValue;

        /// <summary>
        /// Starts the comparison.
        /// </summary>
        /// <param name="value">The value to compare.</param>
        public void Compare(float value)
        {
            if (comparisonType.HasFlag(ComparisonType.Equals) && value.Equals(targetValue))
            {
                OnValue(true);
                return;
            }

            if (comparisonType.HasFlag(ComparisonType.GreaterThan) && value > targetValue)
            {
                OnValue(true);
                return;
            }

            if (comparisonType.HasFlag(ComparisonType.LessThan) && value < targetValue)
            {
                OnValue(true);
                return;
            }

            OnValue(false);
        }

        /// <summary>
        /// Starts the comparison.
        /// </summary>
        /// <param name="value">The value to compare.</param>
        public void Compare(int value)
        {
            Compare((float)value);
        }

        private void OnValue(bool isTrue)
        {
            if (isTrue) onTrue?.Invoke();
            else onFalse?.Invoke();
            onValue?.Invoke(isTrue);
        }
    }
}
