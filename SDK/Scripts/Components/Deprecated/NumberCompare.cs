using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class NumberCompare : MonoBehaviour
    {
        [Flags]
        public enum ComparisonType
        {
            Equals,
            LessThan,
            GreaterThan,
        }

        public int targetValue;
        public ComparisonType comparisonType;
        public UnityEvent onTrue;
        public UnityEvent onFalse;
        public UnityEvent<bool> onValue;

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
