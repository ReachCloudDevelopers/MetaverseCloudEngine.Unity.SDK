using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// A type of <see cref="RigidbodyPhysicsProvider"/> that uses hovering.
    /// </summary>
    [DefaultExecutionOrder(-int.MaxValue)]
    [HideMonoScript]
    [Experimental]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Rigidbody Physics Provider (Hover Step)")]
    public class SteppableRigidbodyPhysicsProvider : RigidbodyPhysicsProvider
    {
        [Header("Step")]
        [SerializeField] private float stepHeightOffset = 0.25f;
        [SerializeField, Min(0f)] private float dampening = 5f;
        [SerializeField] private float groundCorrectionSpeed = 1f;

        private Rigidbody _rb;
        private Transform _tr;

        private float _correctionBeginTime;
        private float _correctionEndTime;

        public bool HoveringEnabled { get; set; } = true;

        protected override void Awake()
        {
            base.Awake();

            _rb = Rigidbody;
            _tr = transform;

            UpdateTime(Time.time, true);
        }

        private void Update()
        {
            if (HoveringEnabled)
            {
                HoverAtGroundHeightDuringUpdate();
                UpdateTime(Time.time);
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (HoveringEnabled)
            {
                HoverAtGroundHeightDuringFixedUpdate();
                HoverAtGroundHeightDuringUpdate();
                UpdateTime(Time.time);
            }
        }

        private void HoverAtGroundHeightDuringUpdate()
        {
            if (_rb.isKinematic) return;
            if (IsGrounded)
            {
                var targetLocalPos = _tr.localPosition;
                var smoothLocalPos = targetLocalPos;
                var targetPos = GroundContactPoint + (_tr.up * stepHeightOffset);
                targetPos = _tr.parent ? _tr.parent.InverseTransformPoint(targetPos) : targetPos;
                targetLocalPos.y = Mathf.Lerp(targetLocalPos.y, targetPos.y, GetGroundT());

                if (dampening > 0)
                    smoothLocalPos.y = Mathf.MoveTowards(smoothLocalPos.y, targetLocalPos.y, Time.deltaTime * (1 / dampening));
                else smoothLocalPos = targetLocalPos;

                _tr.localPosition = smoothLocalPos;
            }
        }

        private void HoverAtGroundHeightDuringFixedUpdate()
        {
            if (_rb.isKinematic) return;
            if (IsGrounded)
            {
                var rigidbodyRotation = _rb.rotation;
                var relativeVelocity = Quaternion.Inverse(rigidbodyRotation) * _rb.velocity;

                if (relativeVelocity.y <= 0.25f)
                {
                    if (Mathf.Abs(relativeVelocity.x) < 0.25f) relativeVelocity.x = 0;
                    if (Mathf.Abs(relativeVelocity.z) < 0.25f) relativeVelocity.z = 0;
                    relativeVelocity.y *= 1f - GetGroundT();
                    _rb.velocity = rigidbodyRotation * relativeVelocity;
                }
            }
        }

        private float GetGroundT()
        {
            return Mathf.InverseLerp(_correctionBeginTime, _correctionEndTime, Time.time);
        }

        private void UpdateTime(float time, bool force = false)
        {
            if (!force && (IsGrounded || _rb.isKinematic))
                return;

            _correctionBeginTime = time;
            _correctionEndTime = time + groundCorrectionSpeed;
        }
    }
}