using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class GetParent : TriInspectorMonoBehaviour
    {
        [Required][SerializeField] private Transform child;
        [SerializeField] private bool getOnStart = true;
        [SerializeField] private bool root;
        [HideIf(nameof(root))]
        [SerializeField] private int itterations = 1;

        [Space]
        [SerializeField] private UnityEvent<Transform> onGet;
        [SerializeField] private UnityEvent<GameObject> onGetGameObject;
        [SerializeField] private UnityEvent onNoParent;

        private bool _hasStarted;

        public Transform Child { get => child; set => child = value; }

        private void Reset()
        {
            child = transform;
        }

        private void Start()
        {
            _hasStarted = true;
            if (getOnStart)
                Get();
        }

        public void Get()
        {
            if (!_hasStarted && getOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            if (!child)
                return;

            Transform parent = null;
            if (root)
            {
                parent = child.root;
            }
            else
            {
                Transform currentChild = child;
                for (int i = 0; i < itterations; i++)
                {
                    currentChild = child.parent;
                    if (currentChild == null)
                        break;
                }

                parent = currentChild;
            }

            if (parent)
            {
                onGet?.Invoke(parent);
                onGetGameObject?.Invoke(parent.gameObject);
            }
            else
            {
                onNoParent?.Invoke();
            }
        }
    }
}