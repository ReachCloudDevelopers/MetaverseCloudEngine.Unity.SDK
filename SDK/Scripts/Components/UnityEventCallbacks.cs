using System;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component that provides MonoBehavior lifetime events. These events are primarily targetting
    /// activation and deactivations. Please use <see cref="UnityUpdateEventCallbacks"/> for Update, FixedUpdate, LateUpdate
    /// event callbacks.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Initialization)]
    [ExecuteInEditMode]
    [HideMonoScript]
    public class UnityEventCallbacks : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// A set of events that expose the mono-behavior Unity events to the Unity editor.
        /// </summary>
        [Serializable]
        public class Events
        {
            [Tooltip("Invoked when the Awake method is called on this component.")]
            public UnityEvent onAwake = new();
            [Tooltip("Invoked when the OnApplicationPause method is called with 'pause' set to false.")]
            public UnityEvent onApplicationResume = new();
            [Tooltip("Invoked when the OnApplicationPause method is called with 'pause' set to true.")]
            public UnityEvent onApplicationPause = new();
            [Tooltip("Invoked when this Mono Behaviour is destroyed.")]
            public UnityEvent onDestroy = new();
            [Tooltip("Invoked when this Mono Behaviour is enabled.")]
            public UnityEvent onEnable = new();
            [Tooltip("Invoked when this Mono Behaviour is disabled.")]
            public UnityEvent onDisable = new();
            [Tooltip("Invoked after this Mono Behaviour has awakened but before it is updated.")]
            public UnityEvent onStart = new();
        }

        [Tooltip("If true, executes the callbacks in edit mode. This is useful for debugging and testing.")]
        public bool executeInEditMode;
        [Tooltip("If true, defers any event callbacks until the meta space is initialized.")]
        public bool deferUntilInitialized;

        [Tooltip("Events that expose Unity callbacks to the Editor.")]
        public Events events = new();
        
        private Action _currentDeferredSingleAction;

        private void Awake()
        {
            if (!CanRun()) return;
            RunDeferred(() => events.onAwake?.Invoke(), () => gameObject.activeInHierarchy);
        }

        private void OnApplicationPause(bool pause)
        {
            if (!CanRun()) return;
            RunDeferred(() =>
            {
                if (!isActiveAndEnabled)
                    return;
                if (pause) events.onApplicationPause?.Invoke();
                else events.onApplicationResume?.Invoke(); 
            });
        }

        private void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            if (!CanRun()) return;
            RunDeferred(() => events.onDestroy?.Invoke());
        }

        private void OnEnable()
        {
            if (!CanRun()) return;
            RunDeferredSingle(() => events.onEnable?.Invoke(), () => isActiveAndEnabled);
        }

        private void OnDisable()
        {
            if (MetaverseProgram.IsQuitting) return;
            if (!CanRun()) return;
            RunDeferredSingle(() => events.onDisable?.Invoke(), () => !isActiveAndEnabled);
        }

        private void Start()
        {
            if (!CanRun()) return;
            RunDeferred(() => events.onStart?.Invoke(), () => isActiveAndEnabled);
        }

        private bool CanRun()
        {
#if UNITY_EDITOR
            return Application.isPlaying || executeInEditMode;
#else
            return true;
#endif
        }
        
        private void RunDeferredSingle(Action action, Func<bool> condition = null)
        {
            if (!deferUntilInitialized || MetaSpace.Instance is null)
            {
                action?.Invoke();
                return;
            }
            _currentDeferredSingleAction = action;
            MetaSpace.OnReady(() =>
            {
                if (!this) return;
                if (condition?.Invoke() ?? true)
                    _currentDeferredSingleAction?.Invoke();
                _currentDeferredSingleAction = null;
            });
        }

        private void RunDeferred(Action action, Func<bool> delayedCondition = null)
        {
            if (!deferUntilInitialized || MetaSpace.Instance is null)
            {
                action?.Invoke();
                return;
            }
            MetaSpace.OnReady(() =>
            {
                if (!this)
                    return;
                if (delayedCondition?.Invoke() ?? true)
                    action?.Invoke();
            });
        }
    }
}
