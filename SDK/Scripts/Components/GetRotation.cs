using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class GetRotation : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField] private Transform target;
        [SerializeField] private bool getOnStart = true;

        [Space]
        [SerializeField] private UnityEvent<Quaternion> onGetWorldRotation;
        [SerializeField] private UnityEvent<Quaternion> onGetLocalRotation;
        [SerializeField] private UnityEvent<Vector3> onGetWorldRotationEuler;
        [SerializeField] private UnityEvent<Vector3> onGetLocalRotationEuler;

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

            onGetWorldRotation?.Invoke(target.rotation);
            onGetWorldRotationEuler?.Invoke(target.eulerAngles);

            onGetLocalRotation?.Invoke(target.localRotation);
            onGetLocalRotationEuler?.Invoke(target.localEulerAngles);
        }
    }
}
