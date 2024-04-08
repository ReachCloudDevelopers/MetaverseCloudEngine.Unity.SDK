using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Invoke an event. This essentially wraps the <see cref="UnityEvent"/> class.
    /// </summary>
    [HideMonoScript]
    public class EventInvoker : TriInspectorMonoBehaviour
    {
        [Tooltip("The callback event to invoke.")]
        public UnityEvent onInvoked;
        [Tooltip("Whether to ignore the component's enabled state.")]
        public bool ignoreEnabled;

        private void Start() { /* for enabled/disabled toggle */ }

        /// <summary>
        /// Invoke the unity event.
        /// </summary>
        public void Invoke()
        {
            if (ignoreEnabled || isActiveAndEnabled)
                onInvoked?.Invoke();
        }
    }
}
