using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component to set the parent of a transform.
    /// </summary>
    [HideMonoScript]
    public class SetParent : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// Settings for setting the parent transform.
        /// </summary>
        [System.Serializable]
        public class SetParentTransformSettings
        {
            public bool worldPositionStays;
            public bool setLocalPos;
            public Vector3 localPos;
            public bool setLocalRot;
            public Vector3 localRot;
            public bool setLocalScale;
            public Vector3 localScale = Vector3.one;
        }

        /// <summary>
        /// Events for setting the parent transform.
        /// </summary>
        [System.Serializable]
        public class SetParentTransformEvents
        {
            public UnityEvent onSetNull;
            public UnityEvent<Transform> onSetTransform;
            public UnityEvent<GameObject> onSetGameObject;
        }

        [Tooltip("The transform to set the parent of. If null, the transform of this game object will be used.")]
        [Required][SerializeField] private Transform child;
        [Tooltip("The parent transform to set. If null, the parent will be set to null.")]
        [SerializeField] private Transform parent;
        [Tooltip("Whether to set the parent on Start.")]
        public bool setOnStart;
        [Tooltip("Whether to allow setting the parent to null.")]
        public bool allowNull;
        [Tooltip("Settings for setting the parent transform.")]
        public SetParentTransformSettings transformSettings;
        [Tooltip("Events for setting the parent transform.")]
        public SetParentTransformEvents events;

        private bool _hasStarted;

        /// <summary>
        /// Gets or sets the transform to set the parent of.
        /// </summary>
        public Transform Child {
            get => child;
            set => child = value;
        }

        /// <summary>
        /// Gets or sets the parent transform to set.
        /// </summary>
        public Transform Parent {
            get => parent;
            set => parent = value;
        }

        private void Reset()
        {
            child = transform;
        }

        private void Start()
        {
            _hasStarted = true;

            if (!child)
                child = transform;

            if (setOnStart)
                Set(parent);
        }

        /// <summary>
        /// Sets the parent transform.
        /// </summary>
        public void Set()
        {
            Set(parent);
        }

        /// <summary>
        /// Sets the parent transform.
        /// </summary>
        /// <param name="p">The parent transform to set. If null, the parent will be set to null.</param>
        public void Set(Transform p)
        {
            if (setOnStart && !_hasStarted)
                return;

            if (!isActiveAndEnabled)
                return;

            if (!child)
                return;

            if (p == null && !allowNull)
                return;

            child.SetParent(p, transformSettings.worldPositionStays);
            if (transformSettings.setLocalPos) child.localPosition = transformSettings.localPos;
            if (transformSettings.setLocalRot) child.localRotation = Quaternion.Euler(transformSettings.localRot);
            if (transformSettings.setLocalScale) child.localScale = transformSettings.localScale;

            if (p == null) events.onSetNull?.Invoke();
            else
            {
                events.onSetTransform?.Invoke(p);
                events.onSetGameObject?.Invoke(p.gameObject);
            }
        }

        /// <summary>
        /// Sets the parent transform.
        /// </summary>
        /// <param name="p">The parent game object to set. If null, the parent will be set to null.</param>
        public void Set(GameObject p)
        {
            Set(p.transform);
        }

        /// <summary>
        /// Sets the parent transform to null.
        /// </summary>
        public void SetNull()
        {
            Set((Transform)null);
        }
    }
}