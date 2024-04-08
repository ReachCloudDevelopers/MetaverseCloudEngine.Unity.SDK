using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class GetPosition : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField] private Transform target;
        [SerializeField] private bool getOnStart = true;

        [Space]
        [SerializeField] private UnityEvent<Vector3> onGetWorldPosition;
        [SerializeField] private UnityEvent<Vector3> onGetLocalPosition;

        private bool _hasStarted;

        public Transform Target { get => target; set => target = value; }

        private void Reset()
        {
            target = transform;
        }

        private void Start()
        {
            _hasStarted = true;

            if (!target)
                target = transform;

            if (getOnStart)
                Get();
        }

        public void Get()
        {
            if (!_hasStarted && getOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            if (!target)
                return;

            onGetWorldPosition?.Invoke(target.position);
            onGetLocalPosition?.Invoke(target.localPosition);
        }
    }
}
