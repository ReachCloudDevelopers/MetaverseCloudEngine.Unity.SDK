using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Use this component to layer multiple true/false states into a single boolean. 
    /// This may be useful if more than one behavior manages the active state
    /// of a game object or component.
    /// </summary>
    public class GameObjectBooleanStateAggregator : TriInspectorMonoBehaviour
    {
        [Tooltip("Invoked with the boolean value of the combined state.")]
        public UnityEvent<bool> onValueChanged;
        [Tooltip("Invoked when the state becomes true.")]
        public UnityEvent onTrue;
        [Tooltip("Invoked when the state becomes false.")]
        public UnityEvent onFalse;

        private List<GameObject> _flags;
        private List<DestroyCallback> _notifiers;

        private bool _isTrue;
        private bool _firstCheck;

        private void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            if (_notifiers != null)
            {
                foreach (var notify in _notifiers)
                    notify.Cancel();
                _notifiers = null;
            }
        }

        /// <summary>
        /// Adds a true state.
        /// </summary>
        public void True() => True(gameObject);

        /// <summary>
        /// Adds a false state.
        /// </summary>
        public void False() => False(gameObject);

        /// <summary>
        /// Adds a state with the given true/false value.
        /// </summary>
        /// <param name="value"></param>
        public void Value(bool value)
        {
            if (value) True();
            else False();
        }

        /// <summary>
        /// Adds a true state with a custom context.
        /// </summary>
        /// <param name="context">The game object context. When this object is destroyed, the specified true state will be subtracted.</param>
        public void True(GameObject context)
        {
            _flags ??= new List<GameObject>();
            _flags.Add(context);

            if (context != gameObject)
            {
                _notifiers ??= new List<DestroyCallback>();
                _notifiers.Add(context.OnDestroy(NotifyDestroyed));
            }

            CheckValue();
        }

        /// <summary>
        /// Adds a false state with a custom context.
        /// </summary>
        /// <param name="context">The game object context. When this object is destroyed, the specified false state will be subtracted.</param>
        public void False(GameObject context)
        {
            if (_notifiers != null)
            {
                _notifiers = _notifiers.Where(x => x).ToList();
                var notifier = _notifiers.FirstOrDefault(x => x && x.gameObject == context);
                if (notifier != null) notifier.Cancel();
                if (_notifiers.Count == 0) _notifiers = null;
            }

            if (_flags != null && _flags.Remove(context))
            {
                if (_flags.Count == 0) _flags = null;
                CheckValue();
            }
        }

        private void NotifyDestroyed(DestroyCallback go)
        {
            False(go.gameObject);
        }

        private void CheckValue()
        {
            var isTrue = _flags is { Count: > 0 };
            if (_isTrue == isTrue && _firstCheck) 
                return;
            
            _firstCheck = true;
            _isTrue = isTrue;
            if (isTrue) onTrue?.Invoke();
            else onFalse?.Invoke();
            onValueChanged?.Invoke(_isTrue);
        }
    }
}
