using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// This component ensures that the XR HMD tracking point is at the center of the <see cref="trackingPoints"/> any horizontal movement will be applied to the Root Transform.
    /// </summary>
    [DefaultExecutionOrder(-int.MaxValue)]
    public class XROriginTrackingOffset : MonoBehaviour
    {
        [Tooltip("An initial delay to apply before performing any movements. This could be useful if some pre-processing occurs on the HMD before we want to perform a move.")]
        [SerializeField] private float initialDelay = 0.1f;
        [Tooltip("The object that will be offset by the tracking points.")]
        [SerializeField] private Transform rootTransform;
        [Tooltip("If true: when this object is disabled the local position will be reset.")]
        [SerializeField] private bool resetTrackingOffsetOnDisable = true;
        [Tooltip("The points that are being tracked.")]
        [SerializeField] private List<Transform> trackingPoints = new();

        private Transform _transform;
        private Rigidbody _rootRigidbody;
        private Vector3 _lastTrackingOrigin;
        private float _cooldown;

        private void Awake()
        {
            _transform = transform;
            if (!rootTransform)
                enabled = false;
            else
                _rootRigidbody = rootTransform.GetComponent<Rigidbody>();
        }

        private void Reset()
        {
            rootTransform = transform.parent;
        }

        private void OnEnable()
        {
            _lastTrackingOrigin = Vector3.zero;
            _cooldown = MVUtils.CachedTime + initialDelay;
        }

        private void OnDisable()
        {
            if (resetTrackingOffsetOnDisable)
                _transform.localPosition = Vector3.zero;
        }

        private void LateUpdate()
        {
            UpdateTrackingOffset();
        }

        private void UpdateTrackingOffset()
        {
            if (trackingPoints.Count == 0 || !TryCalculateTrackingOrigin(out Vector3 origin))
            {
                Off();
                _cooldown = MVUtils.CachedTime + initialDelay;
                return;
            }

            if (MVUtils.CachedTime < _cooldown)
            {
                Off();
                return;
            }

            var originDelta = origin - _lastTrackingOrigin;
            originDelta.y = 0f;
            _lastTrackingOrigin = origin;

            _transform.Translate(-originDelta, Space.Self);
            if (_rootRigidbody && _rootRigidbody.interpolation == RigidbodyInterpolation.Interpolate)
                _rootRigidbody.MovePosition(_rootRigidbody.position + _rootRigidbody.rotation * originDelta);
            else rootTransform.Translate(originDelta, Space.Self);
            return;

            void Off()
            {
                _transform.localPosition = Vector3.zero;
                _lastTrackingOrigin = Vector3.zero;
            }
        }

        private bool TryCalculateTrackingOrigin(out Vector3 origin)
        {
            origin = Vector3.zero;

            if (trackingPoints.Count == 0)
                return false;

            var any = false;
            foreach (var point in trackingPoints.Where(point => point && point.gameObject.activeInHierarchy))
            {
                origin += _transform.InverseTransformPoint(point.position);
                any = true;
            }

            if (!any)
                return false;

            origin /= trackingPoints.Count;
            return true;
        }

        public void AddTrackingPoint(Transform t)
        {
            trackingPoints.Add(t);
            CleanUpTrackingPoints();
        }

        public void RemoveTrackingPoint(Transform t)
        {
            trackingPoints.Remove(t);
            CleanUpTrackingPoints();
        }

        private void CleanUpTrackingPoints() => trackingPoints.RemoveAll(x => !x);
    }
}