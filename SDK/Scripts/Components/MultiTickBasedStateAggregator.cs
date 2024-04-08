using System;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Adds the ability to perform operations whenever a series of behavioral
    /// tasks are completed.
    /// </summary>
    public class MultiTickBasedStateAggregator : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// A single item in a <see cref="MultiTickBasedStateAggregator"/>.
        /// </summary>
        [Serializable]
        public class TickItem
        {
            [FormerlySerializedAs("name")] 
            [Tooltip("The name of the item. Should be unique.")]
            public string identifier;
            [Tooltip("The number of 'ticks' needed for the list item to complete. By default it's just one.")]
            [Min(1)] public int minimumTicks = 1; 
            public bool allowNegativeTicks;

            [Header("Events")]
            [Tooltip("Occurs when this check list item is ticked.")]
            public UnityEvent onTicked;
            [FormerlySerializedAs("onCompleted")]
            [Tooltip("Occurs whenever the number of ticks equals the Minimum Ticks value. Will only occur the first time the Minimum Ticks is reached.")]
            public UnityEvent onFullyTicked;
            [FormerlySerializedAs("onUncompleted")]
            [Tooltip("Occurs when this check list item is uncompleted.")]
            public UnityEvent onUnFullyTicked;

            private int _ticks;

            /// <summary>
            /// Gets a value indicating whether this <see cref="TickItem"/> is completed.
            /// </summary>
            public bool Completed { get; private set; }

            public void Tick()
            {
                _ticks++;
                onTicked?.Invoke();
                if (_ticks >= minimumTicks)
                {
                    onFullyTicked?.Invoke();
                    Completed = true;
                }
            }
            
            public void UnTick()
            {
                if (_ticks <= 0 && !allowNegativeTicks) return;
                _ticks--;
                if (Completed)
                {
                    Completed = false;
                    onUnFullyTicked?.Invoke();
                }
            }
        }

        [Tooltip("The tick items.")]
        public TickItem[] items;
        [FormerlySerializedAs("onCompleted")] 
        [Tooltip("Occurs once when all items are fully ticked.")]
        public UnityEvent onFullyTicked;
        [FormerlySerializedAs("onUncompleted")]
        [Tooltip("Occurs when we were completed but not all items are fully ticked.")]
        public UnityEvent onUnFullyTicked;

        private bool _initialized;

        /// <summary>
        /// Gets a value indicating whether all <see cref="items"/> are completed.
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
                foreach (var item in items)
                {
                    if (item.Completed)
                        item.onUnFullyTicked?.Invoke();
                }

                onUnFullyTicked?.Invoke();
            }
        }

        /// <summary>
        /// Call this to "tick" the item with the given <paramref name="itemID"/>.
        /// </summary>
        /// <param name="itemID">The name of the check list item.</param>
        public void Tick(string itemID)
        {
            _initialized = true;
            if (Completed)
                return;

            var item = items.FirstOrDefault(x => x.identifier == itemID);
            if (item != null)
            {
                item.Tick();
                if (items.All(x => x.Completed))
                {
                    onFullyTicked?.Invoke();
                    Completed = true;
                }
            }
        }

        /// <summary>
        /// Call this to "un-tick" the item with the given <paramref name="itemID"/>.
        /// </summary>
        /// <param name="itemID"></param>
        public void UnTick(string itemID)
        {
            _initialized = true;
            var item = items.FirstOrDefault(x => x.identifier == itemID);
            if (item != null)
            {
                item.UnTick();
                if (Completed && !items.All(x => x.Completed))
                {
                    onUnFullyTicked?.Invoke();
                    Completed = false;
                }
            }
        }
    }
}
