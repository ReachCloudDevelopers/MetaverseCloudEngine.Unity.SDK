using UnityEngine;
using UnityEngine.Events;

using System.Collections.Generic;
using System;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// A helper class to do more reliable (but slower) unity trigger detection.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Trigger Callbacks")]
    public class TriggerCallbacks : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// Callbacks for trigger detection.
        /// </summary>
        [Serializable]
        public class CallbackEvents
        {
            [Tooltip("Called when a game object has entered the trigger.")]
            public UnityEvent<GameObject> onEnter = new();
            [Tooltip("Called when a game object has exited the trigger.")]
            public UnityEvent<GameObject> onExit = new();
        }

        /// <summary>
        /// Options for filtering objects entering and exiting.
        /// </summary>
        [Serializable]
        public class FilterOptions
        {
            [Tooltip("Tags to compare the entered colliders to.")]
            public string[] tags = Array.Empty<string>();
            [Tooltip("Names required by the entered colliders.")]
            public string[] names = Array.Empty<string>();
            public bool checkAttachedRigidbody = true;
        }

        [Tooltip("Options for filtering objects entering and exiting.")]
        public FilterOptions filterOptions = new();
        [Tooltip("Callbacks used for trigger detection.")]
        public CallbackEvents callbacks = new();

        /// <summary>
        /// A list that tracks all of the entered colliders.
        /// </summary>
        private readonly List<GameObject> _enteredColliders = new(25);

        /// <summary>
        /// A list that tracks all of the exited colliders.
        /// </summary>
        private readonly List<GameObject> _exitedColliders = new(25);

        private void FixedUpdate()
        {
            // Process all of the stayed colliders.
            Process();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isActiveAndEnabled)
                return;

            if (!_enteredColliders.Contains(other.gameObject))
            {
                _enteredColliders.Add(other.gameObject);

                if (!_exitedColliders.Remove(other.gameObject))
                    TriggerEntered(other.gameObject);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isActiveAndEnabled)
                return;

            if (!_enteredColliders.Contains(other.gameObject))
            {
                _enteredColliders.Add(other.gameObject);

                if (!_exitedColliders.Remove(other.gameObject))
                    TriggerEntered(other.gameObject);
            }
        }

        /// <summary>
        /// Process all of the entered colliders and queue them for exit.
        /// </summary>
        private void Process()
        {
            for (int i = _exitedColliders.Count - 1; i >= 0; i--)
            {
                if (_exitedColliders[i])
                    TriggerExited(_exitedColliders[i]);
                _exitedColliders.RemoveAt(i);
            }

            for (int i = _enteredColliders.Count - 1; i >= 0; i--)
            {
                if (_enteredColliders[i])
                    _exitedColliders.Add(_enteredColliders[i]);
                _enteredColliders.RemoveAt(i);
            }
        }

        /// <summary>
        /// An artificial trigger exit function that is called if the <see cref="OnTriggerStay(Collider)"/> has
        /// not detected any colliders in the last frame.
        /// </summary>
        /// <param name="other">The collider that exited (may be null).</param>
        private void TriggerExited(GameObject other)
        {
            if (!other) return;
            if (MatchesFilter(other))
                callbacks.onExit?.Invoke(other);
        }

        /// <summary>
        /// This is invoked when the trigger is entered. This will only be called once, ideally, until the collider
        /// exits the trigger again.
        /// </summary>
        /// <param name="other">The collider that exited the trigger.</param>
        private void TriggerEntered(GameObject other)
        {
            if (MatchesFilter(other))
                callbacks.onEnter?.Invoke(other);
        }

        private bool MatchesFilter(GameObject other)
        {
            if (filterOptions == null)
                return false;

            GameObject root;

            if (filterOptions.checkAttachedRigidbody)
            {
                GameObject attachedRb = other.TryGetComponent(out Collider threeD) && threeD.attachedRigidbody 
                    ? threeD.attachedRigidbody.gameObject 
                    : other.TryGetComponent(out Collider2D twoD) && twoD.attachedRigidbody 
                        ? twoD.attachedRigidbody.gameObject 
                        : null;

                root = attachedRb ? attachedRb : other;
            }
            else root = other;

            if (filterOptions.tags != null)
                for (int i = 0; i < filterOptions.tags.Length; i++)
                {
                    if (root.CompareTag(filterOptions.tags[i]))
                        return true;
                }

            if (filterOptions.names != null)
                for (int i = 0; i < filterOptions.names.Length; i++)
                {
                    if (root.name.Equals(filterOptions.names[i])) 
                        return true;
                }

            return 
                (filterOptions.names == null || filterOptions.names.Length == 0) &&
                (filterOptions.tags == null || filterOptions.tags.Length == 0);
        }
    }
}