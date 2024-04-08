using System.Linq;

using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A component that finds a <see cref="GameObject"/> by name or tag.
    /// </summary>
    [HideMonoScript]
    public class FindGameObject : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The type of find to perform.
        /// </summary>
        public enum FindType
        {
            /// <summary>
            /// Find by name.
            /// </summary>
            Name,
            /// <summary>
            /// Find by tag.
            /// </summary>
            Tag,
            /// <summary>
            /// Find by name and tag.
            /// </summary>
            NameAndTag,
        }

        [Tooltip("The type of find to perform.")]
        [SerializeField] private FindType findType = FindType.Name;
        [Required]
        [ShowIf(nameof(FindWithName))]
        [Tooltip("The name of the object to find.")]
        [SerializeField] private string withName;
        [Required]
        [ShowIf(nameof(FindWithTag))]
        [Tooltip("The tag of the object to find.")]
        [SerializeField] private string withTag;

        [Space]
        [Tooltip("Whether to automatically find the object on Start().")]
        [SerializeField] private bool findOnStart = true;

        [Space]
        [Tooltip("The event to invoke if the object is found.")]
        [SerializeField] private UnityEvent<GameObject> onFound;
        [Tooltip("The event to invoke if the object is found.")]
        [SerializeField] private UnityEvent<Transform> onFoundTransform;
        [Tooltip("The event to invoke if the object is not found.")]
        [SerializeField] private UnityEvent onNotFound;

        private bool _hasStarted;

        private bool FindWithName => findType == FindType.Name || findType == FindType.NameAndTag;
        private bool FindWithTag => findType == FindType.Tag || findType == FindType.NameAndTag;

        /// <summary>
        /// Gets or sets the name of the object to find.
        /// </summary>
        public string WithName {
            get => withName;
            set => withName = value;
        }

        /// <summary>
        /// Gets or sets the tag of the object to find.
        /// </summary>
        public string WithTag {
            get => withTag;
            set => withTag = value;
        }

        private void Start()
        {
            _hasStarted = true;

            if (findOnStart)
                Find();
        }

        /// <summary>
        /// Find the object.
        /// </summary>
        public void Find()
        {
            if (!_hasStarted && findOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            GameObject go = null;
            switch (findType)
            {
                case FindType.Name:
                    go = GameObject.Find(withName);
                    break;
                case FindType.Tag:
                    go = GameObject.FindWithTag(withTag);
                    break;
                case FindType.NameAndTag:
                    GameObject[] gos = GameObject.FindGameObjectsWithTag(withTag);
                    go = gos.FirstOrDefault(x => x.name.Equals(withName));
                    break;
            }

            if (go && go.scene == gameObject.scene)
            {
                onFound?.Invoke(go);
                onFoundTransform?.Invoke(go.transform);
            }
            else onNotFound?.Invoke();
        }
    }
}
