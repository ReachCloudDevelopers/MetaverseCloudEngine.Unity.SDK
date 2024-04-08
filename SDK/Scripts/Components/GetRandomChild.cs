using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class GetRandomChild : TriInspectorMonoBehaviour
    {
        [Required]
        [SerializeField] private Transform parent;
        [SerializeField] private bool ignoreInactive = true;
        [SerializeField] private bool getOnStart = true;
        [SerializeField] private bool recursive = true;

        [Space]
        [SerializeField] private UnityEvent<Transform> onRandomChild;
        [SerializeField] private UnityEvent<GameObject> onRandomChildGameObject;
        [SerializeField] private UnityEvent onNotFound;

        private bool _hasStarted;

        public Transform Parent { get => parent; set => parent = value; }

        private void Reset()
        {
            parent = transform;
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

            if (!parent)
                return;

            Transform[] children = parent.GetComponentsInChildren<Transform>(true).Where(x => (!ignoreInactive || x.gameObject.activeInHierarchy) && x != parent && (recursive || x.parent == parent)).ToArray();
            if (children.Length == 0)
            {
                onNotFound?.Invoke();
                return;
            }

            Transform child = children[Random.Range(0, children.Length)];
            onRandomChild?.Invoke(child);
            onRandomChildGameObject?.Invoke(child.gameObject);
        }
    }
}
