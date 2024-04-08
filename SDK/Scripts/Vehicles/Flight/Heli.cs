using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Vehicles.Flight
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class Heli : MonoBehaviour
    {
        [SerializeField] private float flySpeed = 15f;
        [Tooltip("The speed of the heli's tilt in degrees per second.")]
        [SerializeField] private float rollPitchSpeed = 1f;
        [SerializeField] private float yawSpeed = 0.25f;
        [SerializeField] private float maxTiltAngle = 25f;
        [SerializeField] private float liftSpeed = 5f;
        [SerializeField] private float hoverForceMultiplier = 0.99f;

        [Header("Events")]
        [SerializeField] private UnityEvent onTakeOff;
        [SerializeField] private UnityEvent onLand;

        private Transform _transform;
        private Rigidbody _rb;

        private bool _isFlying;
        private float _liftInput;
        private float _yawInput;
        private Vector3 _moveDirection;
        private Vector3 _facingDirection;

        /// <summary>
        /// True or false to specify when the Heli is flying or not.
        /// </summary>
        public bool IsFlying => _isFlying;

        private void Awake()
        {
            _transform = transform;
            _rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            RollPitch();
            Yaw();
        }

        private void FixedUpdate()
        {
            Accelarate();
            Lift();
            Hover();
        }

        private void Accelarate()
        {
            if (_isFlying)
            {
                Vector3 up = GetUp();
                Vector3 moveDir = _moveDirection.FlattenDirection(up);
                _rb.AddForce(_rb.mass * flySpeed * Mathf.Abs(Vector3.Dot(moveDir, _transform.forward.FlattenDirection(up))) * moveDir);
            }
        }

        private void Hover()
        {
            if (_isFlying)
                _rb.AddForce(GetUp() * (_rb.mass * Mathf.Abs(Physics.gravity.y) * hoverForceMultiplier));
        }

        private void Lift()
        {
            if (_isFlying)
                _rb.AddForce(GetUp() * _liftInput * liftSpeed * _rb.mass);
        }

        private void RollPitch()
        {
            Vector3 up = GetUp();
            Quaternion currentRot = Quaternion.LookRotation(_transform.forward.FlattenDirection(up), up);
            float sqrMagnitude = _moveDirection.sqrMagnitude;
            int multiplier = sqrMagnitude > 0 && _isFlying ? 1 : 0;

            Quaternion targetRot = sqrMagnitude > 0 && _isFlying ? Quaternion.LookRotation(_moveDirection.FlattenDirection(up), up) : currentRot;
            targetRot = Quaternion.Inverse(currentRot) * targetRot;

            Quaternion tilt = currentRot * Quaternion.AngleAxis(maxTiltAngle * multiplier, targetRot * Vector3.right);
            Quaternion desiredRot = Quaternion.Slerp(_transform.rotation, tilt, rollPitchSpeed * Time.deltaTime);

            _transform.rotation = desiredRot;
        }

        private Vector3 GetUp()
        {
            return _transform.parent ? _transform.parent.up : Vector3.up;
        }

        private void Yaw()
        {
            if (!_isFlying)
                return;

            float sqrMagnitude = _facingDirection.sqrMagnitude;
            if (sqrMagnitude > 0)
            {
                Quaternion targetRot = Quaternion.LookRotation(_facingDirection.FlattenDirection(_transform.up), _transform.up);
                _transform.rotation = Quaternion.Slerp(_transform.rotation, targetRot, yawSpeed * Time.deltaTime);
            }
            else
            {
                Quaternion rot = Quaternion.LookRotation(_transform.forward.FlattenDirection(_transform.up), _transform.up);
                rot *= Quaternion.Euler(0, _yawInput * yawSpeed * Time.deltaTime, 0);
                _transform.rotation = rot;
            }
        }

        /// <summary>
        /// Start flying.
        /// </summary>
        public void TakeOff()
        {
            if (_isFlying)
                return;
            _isFlying = true;
            onTakeOff?.Invoke();
        }

        /// <summary>
        /// Stop flying.
        /// </summary>
        public void Land()
        {
            if (!_isFlying)
                return;
            _isFlying = false;
            onLand?.Invoke();
        }

        /// <summary>
        /// Look at a particular world direction. Set this to <see cref="Vector3.zero"/> to stop looking in a particular direction and just
        /// use the forward direction of the transform.
        /// </summary>
        /// <param name="worldDirection">The direction to look at.</param>
        public void LookAt(Vector3 worldDirection)
        {
            if (worldDirection.magnitude > 1)
                worldDirection.Normalize();
            _facingDirection = worldDirection;
        }

        /// <summary>
        /// Apply yaw to the Heli.
        /// </summary>
        /// <param name="yaw"></param>
        public void Yaw(float yaw)
        {
            yaw = Mathf.Clamp(yaw, -1, 1);
            _yawInput = yaw;
        }

        /// <summary>
        /// Move towards a particular world direction.
        /// </summary>
        /// <param name="worldDirection">The world direction to move towards.</param>
        public void Move(Vector3 worldDirection)
        {
            if (worldDirection.magnitude > 1)
                worldDirection.Normalize();
            _moveDirection = worldDirection;
        }

        /// <summary>
        /// Lift up/down with the plane.
        /// </summary>
        /// <param name="lift">The lift value, this can be between -1 and 1.</param>
        public void Lift(float lift)
        {
            lift = Mathf.Clamp(lift, -1, 1);
            _liftInput = lift;
        }
    }
}
