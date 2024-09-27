using MetaverseCloudEngine.Unity.Async;
using System;
using System.Collections;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A component that waits for a specified number of seconds before invoking an event.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Gameplay/Wait For Seconds")]
    public class WaitForSeconds : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// Events that this timer will invoke.
        /// </summary>
        [Serializable]
        public class WaitEvents
        {
            [Tooltip("Invoked when the wait is started.")]
            public UnityEvent onStarted = new();
            [Tooltip("Invoked when the wait is completed.")]
            public UnityEvent onFinished = new();
            [Tooltip("Invoked when the wait is finished in a completed state.")]
            public UnityEvent onCompleted = new();
            [Tooltip("Invoked when the wait is finished in a cancelled state.")]
            public UnityEvent onCancelled = new();
            [Tooltip("Invoked when the progress of the wait is updated.")]
            public UnityEvent<float> onProgress = new();
            [Tooltip("Invoked with the inverse of the progress of the wait is updated.")]
            public UnityEvent<float> onInverseProgress = new();
        }

        [Tooltip("The time to wait in seconds.")]
        [Min(0)] public float delay = 1;
        [Tooltip("Whether to wait when this component is initially enabled.")]
        public bool waitOnStart = true;
        [Tooltip("Whether to wait every time this component is re-enabled.")]
        public bool waitOnEnable;
        [Tooltip("Events that this timer will trigger.")]
        public WaitEvents events = new();

        private float _waitEndTime;
        private bool _isStarted;

        /// <summary>
        /// Whether the wait is currently active.
        /// </summary>
        public bool IsWaiting { get; private set; }

        /// <summary>
        /// Gets or sets the delay value.
        /// </summary>
        public float Delay { get => delay; set => delay = value; }

        private void Start()
        {
            if (waitOnStart) Wait();
            _isStarted = true;
        }

        private void OnEnable()
        {
            if (!waitOnEnable) return;
            if (!_isStarted && waitOnStart) return;
            Wait();
        }

        private void OnDisable()
        {
            Cancel();
        }

        /// <summary>
        /// Forces the current wait operation into a completed state.
        /// </summary>
        public void Complete()
        {
            if (!IsWaiting) return;
            IsWaiting = false;
            StopAllCoroutines();
            SetProgress(1);
            events.onCompleted?.Invoke();
            events.onFinished?.Invoke();
        }

        /// <summary>
        /// Cancel the wait if <see cref="IsWaiting"/> is true.
        /// </summary>
        public void Cancel()
        {
            if (!IsWaiting) return;
            IsWaiting = false;
            StopAllCoroutines();
            SetProgress(0);
            events.onCancelled?.Invoke();
            events.onFinished?.Invoke();
        }
        
        /// <summary>
        /// Starts the wait.
        /// </summary>
        public void Wait()
        {
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this) return;
                if (!isActiveAndEnabled) return;
                _waitEndTime = Time.time + delay;
                if (IsWaiting) StopAllCoroutines();
                IsWaiting = true;
                SetProgress(0);
                events.onStarted?.Invoke();
                StartCoroutine(WaitRoutine());
            });
        }

        #region Deprecated

        [Obsolete("Please use '" + nameof(Cancel) + "' instead.")]
        public void CancelTimer() => Cancel();

        [Obsolete("Please use '" + nameof(Wait) + "' instead.")]
        public void StartTimer() => Wait();

        #endregion

        private void SetProgress(float p)
        {
            events.onProgress?.Invoke(p);
            events.onInverseProgress?.Invoke(1 - p);
        }

        private IEnumerator WaitRoutine()
        {
            var startTime = MVUtils.CachedTime;
            while (MVUtils.CachedTime < _waitEndTime)
            {
                var p = (MVUtils.CachedTime - startTime) / delay;
                SetProgress(p);
                yield return null;
            }
            try
            {
                Complete();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}