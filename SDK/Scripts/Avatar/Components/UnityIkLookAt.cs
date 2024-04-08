using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [HideMonoScript]
    public class UnityIkLookAt : TriInspectorMonoBehaviour
    {
        public enum LookAtMatch
        {
            Direction,
            Point,
        }
        
        public Transform target;
        public Animator animator;
        public int layerIndex;
        public int order;

        [Header("Settings")]
        public LookAtMatch match;
        public float lookAtDirectionDistance = 100;
        public float weightLerpSpeed = 10;
        
        [Header("Weights")]
        [Range(0, 1)] public float headWeight = 0.5f;
        [Range(0, 1)] public float eyesWeight = 0.5f;
        [Range(0, 1)] public float bodyWeight = 0.5f;

        [Header("Angle Check")]
        public Transform referenceAngle;
        public float maxAngle = 85;
        public UnityEvent onMaxAngleExceeded;
        public UnityEvent onWithinMaxAngle;

        private UnityAnimatorIKCallbacks _callbacks;
        private float _lerpWeight;
        private bool _wasMaxAngleExceeded;
        private bool _maxAngleExceeded;
        private Vector3 _targetLookAtPos;
        private Transform _transform;

        private void Awake()
        {
            _transform = transform;
            if (animator)
                _callbacks = animator.gameObject.GetOrAddComponent<UnityAnimatorIKCallbacks>();
        }
        
        private void Reset()
        {
            animator = this.GetNearestComponent<Animator>();
        }

        private void OnEnable()
        {
            if (_callbacks) _callbacks.RegisterIKCallback(OnAnimatorIKCallback, order);
            if (target) CalculateLookAtPos(out _, true);
        }

        private void OnDisable()
        {
            if (_callbacks) _callbacks.UnRegisterIKCallback(OnAnimatorIKCallback);
            _lerpWeight = 0;
        }

        private void LateUpdate()
        {
            TargetLookAtUpdate();
        }

        private void OnAnimatorIKCallback(int index)
        {
            if (!animator)
                enabled = false;

            if (index != layerIndex)
                return;

            if (!target)
                return;

            TargetLookAtUpdate();

            var overallWeight = _maxAngleExceeded ? 0 : 1;
            if (weightLerpSpeed > 0)
                _lerpWeight = Mathf.Lerp(_lerpWeight, overallWeight, Time.deltaTime * weightLerpSpeed);
            else _lerpWeight = overallWeight;
            animator.SetLookAtPosition(_transform.TransformPoint(_targetLookAtPos));
            animator.SetLookAtWeight(_lerpWeight, bodyWeight, headWeight, eyesWeight);
        }

        private void TargetLookAtUpdate()
        {
            _targetLookAtPos = _transform.InverseTransformPoint(CalculateLookAtPos(out _maxAngleExceeded));
        }

        private Vector3 CalculateLookAtPos(out bool maxAngleExceeded, bool forceEvents = false)
        {
            var lookAtPos = match == LookAtMatch.Direction
                ? target.position + target.forward * lookAtDirectionDistance
                : target.position;

            maxAngleExceeded =
                Vector3.Angle(
                    match == LookAtMatch.Direction || !referenceAngle
                        ? target.forward
                        : (lookAtPos - transform.position).normalized, referenceAngle.forward) > maxAngle;

            if (_wasMaxAngleExceeded != maxAngleExceeded || forceEvents)
            {
                _wasMaxAngleExceeded = maxAngleExceeded;
                if (maxAngleExceeded) onMaxAngleExceeded?.Invoke();
                else if (forceEvents) onWithinMaxAngle?.Invoke();
            }
            
            return lookAtPos;
        }
    }
}