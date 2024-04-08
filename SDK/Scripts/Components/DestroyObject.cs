using System;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// Destroy a <see cref="GameObject"/>.
    /// </summary>
    [HideMonoScript]
    public class DestroyObject : TriInspectorMonoBehaviour
    {
        [SerializeField] private bool self;
        [Required]
        [HideIf(nameof(self))]
        [SerializeField] private GameObject @object;
        [Tooltip("Whether to automatically destroy this object on Start().")]
        [LabelText("Destroy On Start")] public bool autoDestroy = true;
        [Tooltip("The delay to use for the object destroy.")]
        [Min(0)] public float destroyDelay = 0;

        /// <summary>
        /// Gets or sets the object to destroy.
        /// </summary>
        public GameObject Object { get => @object; set => @object = value; }

        private void Reset()
        {
            self = true;
        }

        private void Start()
        {
            if (autoDestroy)
                Destroy();
        }

        /// <summary>
        /// Destroy the object.
        /// </summary>
        public void Destroy()
        {
            if (!enabled) return;
            if (self)
                Destroy(gameObject, destroyDelay);
            else
                Destroy(@object ? @object : gameObject, destroyDelay);
        }
    }
}