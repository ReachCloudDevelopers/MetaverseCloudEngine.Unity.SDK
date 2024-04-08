using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(ScrollRect))]
    public class ExtraScrollRectEvents : MonoBehaviour
    {
        [Serializable]
        public class Events
        {
            public UnityEvent onUpperLimitEnter;
            public UnityEvent onLowerLimitEnter;
        }

        public Events events = new();

        private ScrollRect _scrollRect;
        private bool _lowerLimit;
        private bool _upperLimit;

        private void Awake() => _scrollRect = GetComponent<ScrollRect>();
        private void OnEnable() => _scrollRect.onValueChanged.AddListener(OnValueChanged);
        private void OnDisable() => _scrollRect.onValueChanged.RemoveListener(OnValueChanged);

        private void OnValueChanged(Vector2 value)
        {
            CheckUpperLimit(value);
            CheckLowerLimit(value);
        }

        private void CheckUpperLimit(Vector2 value)
        {
            if (value.y >= 0.9f)
            {
                if (!_upperLimit)
                    events.onUpperLimitEnter?.Invoke();
                _upperLimit = true;
                _lowerLimit = false;
            }
            else
            {
                _upperLimit = false;
            }
        }

        private void CheckLowerLimit(Vector2 value)
        {
            if (value.y <= 0.1f)
            {
                if (!_lowerLimit)
                    events.onLowerLimitEnter?.Invoke();
                _lowerLimit = true;
                _upperLimit = false;
            }
            else
            {
                _lowerLimit = false;
            }
        }
    }
}