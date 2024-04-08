using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix
{
    /// <summary>
    /// A component that adds a force to a rigidbody.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Add Force")]
    public class AddForce : TriInspectorMonoBehaviour
    {
        [Required]
        [Tooltip("The rigidbody to add the force to.")]
        [SerializeField] private Rigidbody body;
        [Tooltip("Whether to apply the force on start.")]
        [SerializeField] private bool applyOnStart = true;
        [Tooltip("The multiplier to apply to the force.")]
        [SerializeField, Min(0)] private float multiplier = 1f;
        [Tooltip("The force to apply.")]
        [SerializeField] private Vector3 force;
        [Tooltip("The space to apply the force in.")]
        [SerializeField] private Space space = Space.Self;
        [Tooltip("The force mode to use.")]
        [SerializeField] private ForceMode forceMode = ForceMode.VelocityChange;

        private bool _hasStarted;

        /// <summary>
        /// Gets or sets a multiplier to apply to the force.
        /// </summary>
        public float Multiplier { get => multiplier; set => multiplier = value; }
        /// <summary>
        /// Gets or sets the rigidbody to add the force to.
        /// </summary>
        public Rigidbody Body { get => body; set => body = value; }
        /// <summary>
        /// Gets or sets the force to apply.
        /// </summary>
        public Vector3 Force { get => force; set => force = value; }
        /// <summary>
        /// Gets or sets the mode to apply the force in.
        /// </summary>
        public ForceMode ForceMode { get => forceMode; set => forceMode = value; }

        private void Start()
        {
            _hasStarted = true;
            if (applyOnStart)
                Apply();
        }

        /// <summary>
        /// Applies the force to the rigidbody.
        /// </summary>
        public void Apply()
        {
            if (!_hasStarted && applyOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            if (!body || body.isKinematic)
                return;

            switch (space)
            {
                case Space.World:
                    body.AddForce(force * multiplier, forceMode);
                    break;
                case Space.Self:
                    body.AddForce(transform.rotation * force * multiplier, forceMode);
                    break;
            }
        }
    }
}
