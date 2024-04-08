using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class GetChild : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField] private Transform parent;
        [Required]
        [SerializeField] private string childPath;
        [SerializeField] private bool getOnStart = true;

        [Space]
        [SerializeField] private UnityEvent<Transform> onChildFound;
        [SerializeField] private UnityEvent<GameObject> onChildGameObjectFound;
        [SerializeField] private UnityEvent onNotFound;

        private bool _hasStarted;

        public Transform Parent { get => parent; set => parent = value; }
        public string ChildPath { get => childPath; set => childPath = value; }

        private void Start()
        {
            _hasStarted = true;
            if (getOnStart)
                Get();
        }

        private void Reset()
        {
            parent = transform;
        }

        public void Get()
        {
            if (!_hasStarted && getOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            if (!parent)
                return;

            if (string.IsNullOrEmpty(childPath))
            {
                onNotFound?.Invoke();
                return;
            }

            Transform child = parent.Find(childPath);
            if (child != null)
            {
                onChildFound?.Invoke(child);
                onChildGameObjectFound?.Invoke(child.gameObject);
                return;
            }

            onNotFound?.Invoke();
        }
    }
}
