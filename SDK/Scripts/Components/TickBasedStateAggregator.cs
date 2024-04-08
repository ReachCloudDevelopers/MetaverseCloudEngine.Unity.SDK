using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Components
{
    public class TickBasedStateAggregator : TriInspectorMonoBehaviour
    {
        [Min(1)] public int minimumTicks = 1;
        public bool allowNegativeTicks;
        [Header("Events")]
        [Tooltip("Occurs when this check list item is ticked.")]
        public UnityEvent onTicked;
        [FormerlySerializedAs("onCompleted")]
        [Tooltip("Occurs whenever the number of ticks equals the Minimum Ticks value. " +
                 "Will only occur the first time the Minimum Ticks is reached.")]
        public UnityEvent onFullyTicked;
        [FormerlySerializedAs("onUncompleted")]
        [Tooltip("Occurs when this check list item is uncompleted.")]
        public UnityEvent onUnFullyTicked;

        private int _ticks;
        private bool _initialized;

        /// <summary>
        /// Gets a value indicating whether this <see cref="TickBasedStateAggregator"/> is completed.
        /// </summary>
        public bool Completed { get; private set; }

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (!_initialized)
            {
                _initialized = true;
                onUnFullyTicked?.Invoke();
            }
        }

        public void Tick()
        {
            _initialized = true;
            _ticks++;
            onTicked?.Invoke();
            if (!Completed && _ticks >= minimumTicks)
            {
                onFullyTicked?.Invoke();
                Completed = true;
            }
        }
        
        public void UnTick()
        {
            _initialized = true;
            if (_ticks <= 0 && !allowNegativeTicks) return;
            _ticks--;
            if (Completed && _ticks < minimumTicks)
            {
                Completed = false;
                onUnFullyTicked?.Invoke();
            }
        }
    }
}