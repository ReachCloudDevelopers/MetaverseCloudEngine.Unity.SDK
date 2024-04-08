using MetaverseCloudEngine.Unity.Attributes;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [DefaultExecutionOrder(ExecutionOrder.Initialization)]
    public class UnityUpdateEventCallbacks : MonoBehaviour
    {
        [System.Serializable]
        public class Events
        {
            public UnityEvent onUpdate = new();
            public UnityEvent onFixedUpdate = new();
            public UnityEvent onLateUpdate = new();
        }

        public Events events = new();

        private void Update() => events.onUpdate?.Invoke();
        private void FixedUpdate() => events.onFixedUpdate?.Invoke();
        private void LateUpdate() => events.onLateUpdate?.Invoke();
    }
}
