using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// Modifies the gravity of the current scene.
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Gravity Modifier")]
    public class GravityModifier : TriInspectorMonoBehaviour
    {
        [Tooltip("The gravity value.")]
        [SerializeField] private Vector3 gravity = Physics.gravity;
        [Tooltip("Whether the gravity value is relative to this transform.")]
        [SerializeField] private bool relativeToTransform = true;

        /// <summary>
        /// Gets or sets the current scene gravity.
        /// </summary>
        public Vector3 Gravity {
            get => gravity;
            set => gravity = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the gravity is relative to the current transform.
        /// </summary>
        public bool RelativeToTransform {
            get => relativeToTransform;
            set => relativeToTransform = value;
        }

        private void Reset()
        {
            gravity = Physics.gravity;
        }

        private void FixedUpdate()
        {
            Physics.gravity = relativeToTransform ? transform.TransformDirection(gravity) : gravity;
        }
    }
}
