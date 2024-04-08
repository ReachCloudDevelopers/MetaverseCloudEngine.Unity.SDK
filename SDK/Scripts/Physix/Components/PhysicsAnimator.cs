using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Physix.Abstract;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// Animates an <see cref="IPhysicsProvider"/>.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.PreInitialization)]
    [DisallowMultipleComponent]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Physics Animator")]
    public class PhysicsAnimator : TriInspectorMonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float minVelocityValue = 0.1f;
        [SerializeField] private float minVelocityOutput = 1f;
        [SerializeField] private float maxVelocityValue = 8;
        [SerializeField] private float animationBlendRate = 0.1f;
        [SerializeField] private float velocitySmoothness = 5f;

        private Transform _transform;
        private Transform _physicsTransform;
        private IPhysicsProvider _physicsProvider;

        private Vector3 _velocity;
        private Vector3 _smoothVelocity;
        private Vector3 _lastPosition;
        private float _minVelocitySqrMag;
        private float _minVelocityOutputSqrMag;
        private float _maxVelocitySqrMag;

        private static int VelocityXParam => Animator.StringToHash("VelocityX");
        private static int VelocityYParam => Animator.StringToHash("VelocityY");
        private static int VelocityZParam => Animator.StringToHash("VelocityZ");
        private static int GroundedParam => Animator.StringToHash("IsGrounded");

        public Animator Animator {
            get => animator;
            set => animator = value;
        }

        private void Reset()
        {
            EnsureComponents();
        }

        private void OnValidate()
        {
            _minVelocitySqrMag = minVelocityValue * minVelocityValue;
            _maxVelocitySqrMag = maxVelocityValue * maxVelocityValue;
            _minVelocityOutputSqrMag = minVelocityOutput * minVelocityOutput;
        }

        private void OnTransformParentChanged()
        {
            InitPhysicsProvider();
        }

        private void Awake()
        {
            _minVelocitySqrMag = minVelocityValue * minVelocityValue;
            _maxVelocitySqrMag = maxVelocityValue * maxVelocityValue;
            _minVelocityOutputSqrMag = minVelocityOutput * minVelocityOutput;

            InitPhysicsProvider();
            EnsureComponents();
        }
        
        private void Update()
        {
            CalculateVelocity();
            UpdateAnimation();
        }

        private void InitPhysicsProvider()
        {
            _physicsProvider ??= gameObject.GetComponentInParent<IPhysicsProvider>();
            _physicsProvider ??= gameObject.GetComponentInChildren<IPhysicsProvider>();
            if (_physicsProvider is not null)
            {
                if (!_physicsTransform) _physicsTransform = ((Component)_physicsProvider).transform;
                _lastPosition = _physicsTransform.localPosition;   
            }
            else
            {
                enabled = false;
                MetaverseProgram.Logger.Log("Disabled PhysicsAnimator because no IPhysicsProvider was found.");
            }
        }

        private void EnsureComponents()
        {
            _transform = transform;

            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator) animator = GetComponentInParent<Animator>();
            }
        }

        private void UpdateAnimation()
        {
            if (!animator || _physicsProvider is null)
                return;

            var relativeVelocity = _transform.InverseTransformDirection(_velocity);
            relativeVelocity = Vector3.ProjectOnPlane(relativeVelocity, Vector3.up);

            var mag = relativeVelocity.sqrMagnitude;
            if (mag < _minVelocitySqrMag)
                relativeVelocity = Vector3.zero;
            else
            {
                var inverseLerp = Mathf.InverseLerp(_minVelocityOutputSqrMag, _maxVelocitySqrMag, mag);
                var newMagnitude = Mathf.Lerp(_minVelocityOutputSqrMag, _maxVelocitySqrMag, inverseLerp);
                relativeVelocity = relativeVelocity.normalized * Mathf.Sqrt(newMagnitude);
            }

            if (mag > _maxVelocitySqrMag)
                relativeVelocity = Vector3.ClampMagnitude(relativeVelocity, maxVelocityValue);

            var deltaTime = Time.deltaTime;
            _smoothVelocity = Vector3.Lerp(_smoothVelocity, relativeVelocity, deltaTime * velocitySmoothness);
            animator.SetFloat(VelocityXParam, _smoothVelocity.x, animationBlendRate, deltaTime);
            animator.SetFloat(VelocityYParam, _smoothVelocity.y, animationBlendRate, deltaTime);
            animator.SetFloat(VelocityZParam, _smoothVelocity.z, animationBlendRate, deltaTime);
            animator.SetBool(GroundedParam, _physicsProvider.IsGrounded);
        }

        private void CalculateVelocity()
        {
            if (!_physicsTransform || _physicsProvider is null)
                return;

            var diff = _physicsTransform.localPosition - _lastPosition;
            var delta = diff / Time.deltaTime;
            _velocity = _physicsTransform.parent ? _physicsTransform.parent.TransformVector(delta) : delta;
            _lastPosition = _physicsTransform.localPosition;
        }
    }
}
