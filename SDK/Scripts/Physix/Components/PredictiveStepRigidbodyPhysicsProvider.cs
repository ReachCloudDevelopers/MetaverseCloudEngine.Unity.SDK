using MetaverseCloudEngine.Unity.Locomotion.Components;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Rigidbody Physics Provider (Predictive Step)")]
    [HideMonoScript]
    [Experimental]
    public class PredictiveStepRigidbodyPhysicsProvider : RigidbodyPhysicsProvider
    {
        [Header("Step Raycast")]
        [SerializeField] private float rayOrigin;
        [SerializeField, Min(0)] private float lowerRayHeight = 0.015f;
        [SerializeField, Min(0)] private float lowerRayDistance = 0.075f;
        [SerializeField, Min(0)] private float upperRayHeight = 0.7f;
        [SerializeField, Min(0)] private float stepAdjustmentSpeed = 2f;
        [SerializeField, Range(0, 90f)] private float minimumSurfaceAngle = 20f;
        [SerializeField] private bool requireGrounded;

        private ContinuousMovementProvider _mp;
        private Rigidbody _rb;
        private Transform _tr;
        private CapsuleCollider _col;
        private bool _isSteppingUp;
        private float _stepUpResetCooldown;

        public bool RequireGrounded { get => requireGrounded; set => requireGrounded = value; }
        public float LowerRayHeight { get => lowerRayHeight; set => lowerRayHeight = value; }
        public float LowerRayDistance { get => lowerRayDistance; set => lowerRayDistance = value; }
        public float UpperRayHeight { get => upperRayHeight; set => upperRayHeight = value; }
        public float StepAdjustmentSpeed { get => stepAdjustmentSpeed; set => stepAdjustmentSpeed = value; }
        public float MinimumSurfaceAngle { get => minimumSurfaceAngle; set => minimumSurfaceAngle = value; }

        protected override void Awake()
        {
            base.Awake();

            _tr = transform;
            _col = GetComponent<CapsuleCollider>();
            _rb = GetComponent<Rigidbody>();
            _mp = GetComponent<ContinuousMovementProvider>();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            DoStep();
        }

        protected override bool IsGroundedOverride() => _isSteppingUp;

        private void DoStep()
        {
            if ((!requireGrounded || IsGrounded) && SweepRay())
            {
                StepUp();
                _isSteppingUp = true;
                _stepUpResetCooldown = MVUtils.CachedTime + 0.25f;
            }
            else
            {
                if (MVUtils.CachedTime > _stepUpResetCooldown)
                    _isSteppingUp = false;
            }
        }

        private void StepUp()
        {
            _tr.position += stepAdjustmentSpeed * Time.deltaTime * (_rb.rotation * Vector3.up);
            _rb.SetLinearVelocity(Vector3.ProjectOnPlane(_rb.linearVelocity, _tr.up));
        }

        private bool SweepRay()
        {
            var rbRotation = _rb.rotation;
            var inputVector = Quaternion.Inverse(rbRotation) * _mp.DesiredSpeed.normalized;
            var rayCenter = rbRotation * inputVector;
            var rayLeft = rbRotation * Quaternion.Euler(0, -45, 0) * inputVector;
            var rayRight = rbRotation * Quaternion.Euler(0, 45, 0) * inputVector;
            var sweep = DoStepRay(rayCenter) || DoStepRay(rayLeft) || DoStepRay(rayRight);
            return sweep;
        }

        private bool DoStepRay(Vector3 rayDir)
        {
            var up = _rb.rotation * Vector3.up;
            var pos = _rb.position;
            var rayLowerOrigin = pos + (lowerRayHeight + rayOrigin) * up;

            if (!Physics.Raycast(
                    rayLowerOrigin,
                    rayDir,
                    out var lowerHit,
                    _col.radius + lowerRayDistance,
                    GroundCheckLayers,
                    QueryTriggerInteraction.Ignore)) 
                return false;
            
            var isTooFlat = Vector3.Dot(lowerHit.normal, up) > minimumSurfaceAngle / 90;
            if (isTooFlat) 
                return false;
            
            var rayUpperOrigin = pos + (upperRayHeight + rayOrigin) * up;
            return !Physics.Raycast(
                rayUpperOrigin,
                rayDir,
                out _,
                _col.radius + lowerRayDistance,
                GroundCheckLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}