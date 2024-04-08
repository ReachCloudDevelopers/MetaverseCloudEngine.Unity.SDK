using MetaverseCloudEngine.Unity.Inputs.Components;
using MetaverseCloudEngine.Unity.Physix.Abstract;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// A type of <see cref="IPhysicsProvider"/> that uses <see cref="Rigidbody"/> for physics.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [Experimental]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Rigidbody Physics Provider")]
    [DefaultExecutionOrder(int.MaxValue - 10)]
    public class RigidbodyPhysicsProvider : TriInspectorMonoBehaviour, IPhysicsProvider
    {
        #region Constants

        /// <summary>
        /// The tick rate of grounded checks.
        /// </summary>
        private const float GroundedUpdateInterval = 1f / 20;

        #endregion

        #region Inspector

        [Header("Grounding")]
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private float minGroundNormalDot = 0.3f;
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;

        [Header("Launching")]
        [SerializeField] private float launchHeight = 10;
        [SerializeField] private bool launchingEnabled = true;
        [SerializeField] private float launchCooldown = 0.5f;
        [Tooltip("If true, requires that the jump player input flag is enabled in order to launch.")]
        [SerializeField] private bool readPlayerInputAPI = true;

        [Header("Physics Materials")]
        [SerializeField] private PhysicMaterial fallingPhysMat;
        [SerializeField] private PhysicMaterial groundedPhysMat;

        [Header("Events")]
        [SerializeField] private UnityEvent onLaunch;
        [SerializeField] private UnityEvent onGroundEnter;
        [SerializeField] private UnityEvent onGroundExit;

        #endregion

        #region Private Fields

        private Transform m_Transform;
        private Rigidbody m_Rigidbody;
        private CapsuleCollider m_Collider;

        private bool m_HasRigidbody;
        private float m_LaunchCooldownTime;
        private float m_FixedGroundedUpdateTime;
        private float m_RenderGroundedUpdateTime;

        /// <summary>
        /// A cached array of <see cref="Raycast"/>s that are used
        /// for ground checking.
        /// </summary>
        private static readonly RaycastHit[] GroundHits = new RaycastHit[15];

        #endregion

        #region Properties
        
        public float LaunchCooldown
        {
            get => launchCooldown;
            set => launchCooldown = value;
        }

        /// <summary>
        /// The layers that are considered the ground.
        /// </summary>
        public LayerMask GroundCheckLayers => groundLayers;

        /// <summary>
        /// The <see cref="UnityEngine.Rigidbody"/> that belongs to this <see cref="RigidbodyPhysicsProvider"/>.
        /// </summary>
        public Rigidbody Rigidbody {
            get {
                if (!m_HasRigidbody && m_Rigidbody == null)
                {
                    m_Rigidbody = GetComponent<Rigidbody>();
                    m_HasRigidbody = true;
                }
                return m_Rigidbody;
            }
        }

        /// <summary>
        /// The velocity of the <see cref="Rigidbody"/>.
        /// </summary>
        public Vector3 Velocity {
            get => Rigidbody.velocity;
            set => Rigidbody.velocity = value;
        }

        /// <summary>
        /// Gets or sets the current <see cref="Transform"/> position.
        /// </summary>
        public Vector3 Position
        {
            get => m_Transform.position;
            set
            {
                if (m_Rigidbody.interpolation == RigidbodyInterpolation.Interpolate)
                    m_Rigidbody.MovePosition(value);
                else m_Transform.position = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use interpolation.
        /// </summary>
        public bool Interpolate
        {
            get => Rigidbody.interpolation == RigidbodyInterpolation.Interpolate;
            set => Rigidbody.interpolation = value ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        }

        /// <summary>
        /// Gets or sets the rotation of the <see cref="Rigidbody"/> or the <see cref="Transform"/> based on <see cref="UseTransformRotation"/>.
        /// </summary>
        public Quaternion Rotation {
            get => UseTransformRotation ? m_Transform.rotation : Rigidbody.rotation;
            set {
                if (UseTransformRotation)
                    m_Transform.rotation = value;
                else 
                    Rigidbody.rotation = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="Rigidbody"/> is grounded.
        /// </summary>
        public bool IsGrounded { get; private set; } = true;

        /// <summary>
        /// The contact point where the <see cref="CapsuleCollider"/> is touching.
        /// </summary>
        public Vector3 GroundContactPoint { get; private set; }

        /// <summary>
        /// The collider object of the ground we're touching.
        /// </summary>
        public Collider GroundCollider { get; private set; }

        /// <summary>
        /// The distance from the ground.
        /// </summary>
        public float GroundDistance { get; private set; }

        /// <summary>
        /// The collider attached to the <see cref="GameObject"/>.
        /// </summary>
        public CapsuleCollider CapsuleCollider => m_Collider;

        /// <summary>
        /// Whether to enable or disable the ability for this <see cref="IPhysicsProvider"/> to perform <see cref="Launch"/>.
        /// </summary>
        public bool LaunchingEnabled {
            get => launchingEnabled;
            set => launchingEnabled = value;
        }

        /// <summary>
        /// The height of launching.
        /// </summary>
        public float LaunchHeight {
            get => launchHeight;
            set => launchHeight = value;
        }

        /// <summary>
        /// Gets or sets <see cref="Rigidbody.mass"/>.
        /// </summary>
        public float Mass {
            get => Rigidbody.mass;
            set => Rigidbody.mass = value;
        }

        /// <summary>
        /// Gets or sets a value whether the <see cref="Rigidbody"/> is kinematic.
        /// </summary>
        public bool IsKinematic {
            get => Rigidbody.isKinematic;
            set => Rigidbody.isKinematic = value;
        }

        /// <summary>
        /// An extra distance to apply to the ground check.
        /// </summary>
        public float GroundCheckDistance {
            get => groundCheckDistance;
            set => groundCheckDistance = value;
        }

        /// <summary>
        /// Whether to use transform rotation instead of <see cref="Rigidbody.rotation"/>.
        /// </summary>
        private bool UseTransformRotation => !m_Rigidbody || m_Rigidbody.isKinematic || m_Rigidbody.interpolation != RigidbodyInterpolation.Interpolate;

        #endregion

        #region Unity Events

        protected virtual void Awake()
        {
            m_Transform = transform;
            m_Collider = GetComponent<CapsuleCollider>();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            DrawGroundingGizmos();
        }

        private void Update()
        {
            CheckIsGrounded(ref m_RenderGroundedUpdateTime, false);
        }

        protected virtual void FixedUpdate()
        {
            CheckIsGrounded(ref m_FixedGroundedUpdateTime);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Launches the <see cref="Rigidbody"/> by using AddForce.
        /// </summary>
        public void Launch()
        {
            if (!launchingEnabled || !IsGrounded) return;
            if (MVUtils.CachedTime < m_LaunchCooldownTime) return;
            if (readPlayerInputAPI && !PlayerInputAPI.JumpInputEnabled) return;

            var height = launchHeight * (readPlayerInputAPI ? PlayerInputAPI.JumpMultiplier : 1f);
            if (height <= 0) return;

            var rbs = GetComponentsInChildren<Rigidbody>();
            var up = m_Transform.up;
            foreach (var rb in rbs)
            {
                if (rb.isKinematic)
                    continue;
                var rbVelocity = rb.velocity;
                rbVelocity = Vector3.ProjectOnPlane(rbVelocity, up);
                rbVelocity += up * height;
                rb.velocity = rbVelocity;
            }

            IsGrounded = false;
            m_LaunchCooldownTime = MVUtils.CachedTime + 0.3f;
            onLaunch?.Invoke();
            onGroundExit?.Invoke();
        }

        #endregion

        #region Protected Methods

        protected virtual bool IsGroundedOverride() => false;

        #endregion

        #region Private Methods

        private void CheckIsGrounded(ref float lastUpdateTime, bool allowFalse = true)
        {
            if (IsGroundedOverride())
            {
                IsGrounded = true;
                return;
            }

            var time = MVUtils.CachedTime;
            if (time < m_LaunchCooldownTime)
            {
                if (allowFalse)
                    IsGrounded = false;
                return;
            }

            if (time < lastUpdateTime)
                return;

            lastUpdateTime = time + GroundedUpdateInterval;

            var scale = m_Transform.lossyScale.y;
            var origin = m_Transform.TransformPoint(m_Collider.center);
            var radius = scale * m_Collider.radius;
            var dir = -m_Transform.up;
            var skinWidth = radius * 0.1f;
            var extraDistance = IsGrounded ? groundCheckDistance : 0.01f;
            var distance = scale * (m_Collider.height / 2f + skinWidth + extraDistance);

            if (Time.time >= m_LaunchCooldownTime)
            {
                var hitCount = Physics.SphereCastNonAlloc(origin, radius - skinWidth, dir, GroundHits, distance, groundLayers, QueryTriggerInteraction.Ignore);
                var nearestHitDistance = Mathf.Infinity;
                RaycastHit nearestHit = default;
                var didHit = false;

                for (var i = 0; i < hitCount; i++)
                {
                    var groundHit = GroundHits[i];
                    var hitCollider = groundHit.collider;
                    if (hitCollider.gameObject.layer == Physics.IgnoreRaycastLayer)
                        continue;

                    if (groundHit.distance > nearestHitDistance)
                        continue;

                    if ((hitCollider.attachedRigidbody ? hitCollider.attachedRigidbody.gameObject : hitCollider.gameObject).TryGetComponent(out XRBaseInteractable interactable) && interactable.isSelected)
                        continue;

                    if (groundHit.transform.IsChildOf(m_Transform))
                        continue;

                    const string playerTag = "Player";
                    if (hitCollider.CompareTag(playerTag))
                        continue;

                    var dot = Vector3.Dot(m_Transform.up, groundHit.normal);
                    if (dot > minGroundNormalDot && dot > 0)
                    {
                        nearestHit = groundHit;
                        nearestHitDistance = groundHit.distance;
                        didHit = true;
                    }
                }

                if (didHit)
                {
                    if (!IsGrounded) onGroundEnter?.Invoke();
                    IsGrounded = true;
                    GroundDistance = nearestHit.distance;
                    GroundCollider = nearestHit.collider;
                    GroundContactPoint = nearestHit.point;
                    m_Collider.sharedMaterial = groundedPhysMat;
                    return;
                }
            }

            if (allowFalse)
            {
                if (IsGrounded) onGroundExit?.Invoke();
                GroundDistance = 0;
                GroundCollider = null;
                GroundContactPoint = m_Transform.position;
                IsGrounded = false;
                m_Collider.sharedMaterial = fallingPhysMat;
            }
        }

        private void DrawGroundingGizmos()
        {
            var tr = transform;
            var col = GetComponent<CapsuleCollider>();
            if (col && tr)
            {
                var origin = tr.TransformPoint(col.center);
                var scale = transform.localScale.y;
                var radius = scale * col.radius;
                var dir = -tr.up;
                var skinWidth = radius * 0.1f;
                var distance = scale * (col.height / 2f + skinWidth + groundCheckDistance);

                Gizmos.DrawWireSphere(origin, radius);
                Gizmos.DrawRay(origin, dir * distance);
                Gizmos.DrawWireSphere(origin + dir * distance, radius);
            }
        }

        #endregion
    }
}