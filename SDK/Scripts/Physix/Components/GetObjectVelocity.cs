using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// Measures an objects velocity and outputs the value to an event.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Get Object Velocity")]
    [HideMonoScript]
    public class GetObjectVelocity : TriInspectorMonoBehaviour
    {
        [SerializeField] private bool useTransform;
        [ShowIf(nameof(useTransform))]
        [SerializeField] private bool useLocalVelocity = true;
        [ShowIf(nameof(useTransform))]
        [Required]
        [SerializeField] private Transform transf;
        [HideIf(nameof(useTransform))]
        [Required]
        [SerializeField] private Rigidbody body;

        [Space]
        [SerializeField] private bool everyFrame = true;
        [SerializeField, Min(0)] private float minMagnitude = 0;
        [SerializeField] private UnityEvent<Vector3> onGetVelocity;
        [SerializeField] private UnityEvent<float> onGetVelocityMagnitude;
        [SerializeField] private UnityEvent onAboveMin;
        [SerializeField] private UnityEvent onBelowMin;

        private Vector3 _lastPosition;
        private Vector3 _transformVelocity;
        private bool _belowMin;
        private bool _firstGet;

        /// <summary>
        /// Gets or sets the minimum magnitude.
        /// </summary>
        public float MinMagnitude { get => minMagnitude; set => minMagnitude = value; }

        private void Reset()
        {
            transf = transform;
            body = GetComponent<Rigidbody>();
        }

        private void OnTransformParentChanged()
        {
            if (useTransform)
            {
                _lastPosition = GetTransformPosition();
            }
        }

        private void Start()
        {
            if (useTransform)
            {
                _lastPosition = GetTransformPosition();
            }
        }

        private void Update()
        {
            UpdateTransformVelocity();

            if (everyFrame)
            {
                Get();
            }
        }

        /// <summary>
        /// Performs the velocity check.
        /// </summary>
        public void Get()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var velocity = GetVelocity();
            var mag = velocity.magnitude;

            if (mag <= minMagnitude)
            {
                if (_firstGet || !_belowMin)
                {
                    onBelowMin?.Invoke();
                    _firstGet = false;
                    _belowMin = true;
                }
            }
            else
            {
                if (_firstGet || _belowMin)
                {
                    onAboveMin?.Invoke();
                    _firstGet = false;
                    _belowMin = false;
                }
            }

            onGetVelocity?.Invoke(velocity);
            onGetVelocityMagnitude?.Invoke(mag);
        }

        private void UpdateTransformVelocity()
        {
            if (!useTransform)
                return;
            var newPos = GetTransformPosition();
            _transformVelocity = (newPos - _lastPosition) / Time.deltaTime;
            _lastPosition = newPos;
        }

        private Vector3 GetTransformPosition()
        {
            return useLocalVelocity ? transf.localPosition : transf.position;
        }

        private Vector3 GetVelocity()
        {
            return useTransform ? _transformVelocity : body.velocity;
        }
    }
}
