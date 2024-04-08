using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class RandomlyPickable : TriInspectorMonoBehaviour
    {
        [InfoBox("Use the " + nameof(PickRandom) + " component for picking.")]
        [SerializeField, Range(0, 1f)] private float weight = 1f;

        [Space]
        [Tooltip("Invoked when this pickable is picked.")]
        [SerializeField] private UnityEvent onPicked;

        /// <summary>
        /// The pickable weight.
        /// </summary>
        public float Weight { get => weight; set => weight = value; }

        private void Start() { /* for enabled/disabled toggle. */ }

        /// <summary>
        /// Triggers the <see cref="onPicked"/> callback.
        /// </summary>
        public void TriggerPicked()
        {
            onPicked?.Invoke();
        }
    }
}