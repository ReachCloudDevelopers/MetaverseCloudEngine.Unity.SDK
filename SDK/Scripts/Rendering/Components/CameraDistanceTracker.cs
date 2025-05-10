using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    /// <summary>
    /// A helper component that allows objects to measure their distance from <see cref="Camera.main"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Rendering/Camera Distance Tracker")]
    public class CameraDistanceTracker : TriInspectorMonoBehaviour, IMeasureCameraDistance
    {
        public enum DistanceMeasurementType
        {
            Position,
            NearestPointOnCollider
        }

        [Min(0)]
        [SerializeField] private float minDistance;
        [Min(0)]
        [SerializeField] private float maxDistance;
        [FormerlySerializedAs("measurmentType")]
        [SerializeField] private DistanceMeasurementType measurementType;
        [SerializeField] private Collider[] nearestPointColliders = Array.Empty<Collider>();

        [Header("Events")]
        public UnityEvent onBelowMinDistance = new();
        public UnityEvent onAboveMinDistance = new();
        public UnityEvent onBelowMaxDistance = new();
        public UnityEvent onAboveMaxDistance = new();

        private Transform _transform;

        private bool _isMaxDistance = true;
        private bool _isMinDistance;

        private bool _initMinDistance;
        private bool _initMaxDistance;

        private float _maxDistance;
        private float _minDistance;

        public float MaxDistance {
            get { return maxDistance; }
            set {
                maxDistance = value;
                _maxDistance = value * value;
            }
        }

        public float MinDistance {
            get { return minDistance; }
            set {
                minDistance = value;
                _minDistance = value * value;
            }
        }

        public Vector3 CameraMeasurementPosition => _transform.position;

        private void Awake()
        {
            _transform = transform;
            _maxDistance = maxDistance * maxDistance;
            _minDistance = minDistance * minDistance;

            onAboveMinDistance?.Invoke();
            onAboveMaxDistance?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_transform) _transform = transform;
            Color color = Gizmos.color;
            Gizmos.color = Color.red;
            if (maxDistance > 0) Gizmos.DrawWireSphere(_transform.position, maxDistance);
            Gizmos.color = Color.green;
            if (minDistance > 0) Gizmos.DrawWireSphere(_transform.position, minDistance);
            Gizmos.color = color;
        }

        private void OnValidate()
        {
            _maxDistance = maxDistance * maxDistance;
            _minDistance = minDistance * minDistance;
        }

        private void OnEnable()
        {
            CameraDistanceManager.Instance.AddMeasurer(this);
        }

        private void OnDisable()
        {
            if (MetaverseProgram.IsQuitting) return;
            CameraDistanceManager.Instance.RemoveMeasurer(this);
        }

        public void OnCameraDistance(Camera cam, float sqrDistance)
        {
            if (measurementType == DistanceMeasurementType.NearestPointOnCollider && nearestPointColliders is { Length: > 0 })
            {
                Vector3 currentPos = _transform.position;
                float nearestDistance = Mathf.Infinity;
                Transform camT = cam.transform;
                for (int i = 0; i < nearestPointColliders.Length; i++)
                {
                    Collider collider = nearestPointColliders[i];
                    if (!collider) continue;

                    Vector3 point = collider.ClosestPoint(camT.position);
                    float distance = Vector3.SqrMagnitude(currentPos - point);
                    if (distance < nearestDistance)
                        sqrDistance = distance;
                }
            }

            CheckMaxDistance(sqrDistance);
            CheckMinDistance(sqrDistance);
        }

        private void CheckMaxDistance(float sqrDistance)
        {
            if (sqrDistance > _maxDistance || _initMaxDistance)
            {
                if (!_isMaxDistance)
                {
                    onAboveMaxDistance?.Invoke();
                    _isMaxDistance = true;
                }
            }
            else
            {
                if (_isMaxDistance || _initMaxDistance)
                {
                    onBelowMaxDistance?.Invoke();
                    _isMaxDistance = false;
                }
            }

            _initMaxDistance = false;
        }

        private void CheckMinDistance(float sqrDistance)
        {
            if (sqrDistance <= _minDistance)
            {
                if (!_isMinDistance || _initMinDistance)
                {
                    onBelowMinDistance?.Invoke();
                    _isMinDistance = true;
                }
            }
            else
            {
                if (_isMinDistance || _initMinDistance)
                {
                    onAboveMinDistance?.Invoke();
                    _isMinDistance = false;
                }
            }

            _initMinDistance = false;
        }
    }
}
