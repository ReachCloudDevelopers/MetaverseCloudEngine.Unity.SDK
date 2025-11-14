using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity
{
    [HideMonoScript]
    public class ClampTransformMagnitude : TriInspectorMonoBehaviour
    {
        [Min(0)]
        [SerializeField] private float maxMagnitude = 1f;
        [Min(0)]
        [SerializeField] private float clampAfterSeconds = 0;
        [SerializeField] private bool saveLocalOriginAtStart;
        [HideIf(nameof(saveLocalOriginAtStart))]
        [LabelText("Origin (Optional)")]
        [SerializeField] private Transform origin;
        [SerializeField] private bool clampInUpdate = true;
        [SerializeField] private bool clampInLateUpdate;
        [SerializeField] private bool clampInFixedUpdate;

        [Header("Events")]
        [SerializeField] private UnityEvent onClamped = new();
        [SerializeField] private UnityEvent onUnclamped = new();

        private bool? _clamped;
        private Transform _customLocalOrigin;
        private float _clampTime;
        private bool _clampTimerRunning;

        private bool Clamped
        {
            get => _clamped ??= false;
            set
            {
                var changed = _clamped != value;
                
                if (value == true)
                {
                    // Start cooldown timer when transitioning to clamped state
                    if (changed)
                    {
                        if (!_clampTimerRunning)
                        {
                            _clampTimerRunning = true;
                            _clampTime = Time.time + clampAfterSeconds;
                        }
                    }

                    // Only apply clamp if cooldown has elapsed (or no cooldown set)
                    var cooldownElapsed = clampAfterSeconds <= 0 || Time.time >= _clampTime;
                    if (changed && cooldownElapsed)
                    {
                        _clamped = value;
                        _clampTimerRunning = false;
                        onClamped.Invoke();
                    }
                }
                else if (changed)
                {
                    // Unclamping - no cooldown, reset timer and trigger event immediately
                    _clampTimerRunning = false;
                    _clamped = value;
                    onUnclamped.Invoke();
                }
            }
        }

        private void Start()
        {
            if (saveLocalOriginAtStart)
            {
                _customLocalOrigin = new GameObject($"Custom Origin -> {name}").transform;
                _customLocalOrigin.parent = transform.parent;
                _customLocalOrigin.transform.position = transform.position;
                _customLocalOrigin.transform.rotation = transform.rotation;
                origin = _customLocalOrigin;
            }
        }

        private void OnTransformParentChanged()
        {
            if (_customLocalOrigin)
            {
                _customLocalOrigin.parent = transform.parent;
            }
        }

        private void OnDestroy()
        {
            if (_customLocalOrigin) Destroy(_customLocalOrigin.gameObject);
        }

        private void Update()
        {
            if (clampInUpdate)
                Clamp();
        }
        
        private void LateUpdate()
        {
            if (clampInLateUpdate)
                Clamp();
        }
        
        private void FixedUpdate()
        {
            if (clampInFixedUpdate)
                Clamp();
        }

        /// <summary>
        /// Clamps the position of the transform to the max magnitude.
        /// </summary>
        public void Clamp()
        {
            if (!origin)
            {
                if (transform.position.magnitude > maxMagnitude)
                {
                    transform.position = transform.position.normalized * maxMagnitude;
                    Clamped = true;
                }
                else Clamped = false;
            }
            else
            {
                if ((transform.position - origin.position).magnitude > maxMagnitude)
                {
                    transform.position = origin.position + (transform.position - origin.position).normalized * maxMagnitude;
                    Clamped = true;
                }
                else Clamped = false;
            }
        }
    }
}
