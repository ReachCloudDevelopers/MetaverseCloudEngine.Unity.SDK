using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class LookAt : TriInspectorMonoBehaviour
    {
        public enum LookAtMatch
        {
            Direction,
            Position
        }

        [Tooltip("The source object looking at the target.")]
        [LabelText("Source (Optional)")]
        [SerializeField] private Transform source;
        [Required]
        [Tooltip("The target to look at.")]
        [SerializeField] private Transform target;
        [Tooltip("The way we want to match the target.")]
        public LookAtMatch match;
        [Tooltip("Whether the look-at should happen every frame.")]
        public bool everyFrame = true;
        [HideIf(nameof(everyFrame))]
        public bool lookAtOnStart;

        [ShowIf(nameof(everyFrame))]
        [Header("Smoothing")]
        [Tooltip("The speed at which this transform and the target match.")]
        [Min(0)]
        public float lerpSpeed;

        [Header("Angle Check")]
        [Tooltip("(Optional) The angle reference to use when checking if the max angle is exceeded.")]
        public Transform referenceAngle;
        [Tooltip("The maximum angle that the look at can match, otherwise once exceeded this transform will face the reference angle.")]
        [Min(0)]
        public float maxAngle = 85;
        public UnityEvent onExceededMaxAngle;
        public UnityEvent onWithinMaxAngle;

        private bool _hasStarted;
        private bool _exceededMaxAngle;

        public Transform Source { get => source; set => source = value; }
        public Transform Target { get => target; set => target = value; }

        private void Start()
        {
            if (!source)
                source = transform;

            _hasStarted = true;
            
            if (target)
                CalculateLookRotation(true);
            
            if (lookAtOnStart)
                Look();
        }

        private void Update()
        {
            if (everyFrame)
                Look();
        }

        public void Look()
        {
            try
            {
                if (!_hasStarted && lookAtOnStart)
                    return;

                if (!isActiveAndEnabled)
                    return;

                if (!source)
                    return;

                source.rotation = everyFrame && lerpSpeed > 0
                    ? Quaternion.Lerp(source.rotation, CalculateLookRotation(), Time.deltaTime * lerpSpeed)
                    : CalculateLookRotation();
            }
            catch (NullReferenceException)
            {
                source = null;
            }
            catch (MissingReferenceException)
            {
                source = null;
            }
        }

        private Quaternion CalculateLookRotation(bool forceEvents = false)
        {
            var dir = match switch
            {
                LookAtMatch.Direction => target.forward,
                LookAtMatch.Position => (target.position - source.position).normalized,
                _ => Vector3.zero
            };

            var up = source.parent ? source.parent.up : Vector3.up;
            if (referenceAngle)
            {
                var withinMaxAngle = Vector3.Angle(dir, referenceAngle.forward) < maxAngle;
                if (_exceededMaxAngle != withinMaxAngle || forceEvents)
                {
                    if (withinMaxAngle) onWithinMaxAngle?.Invoke();
                    else onExceededMaxAngle?.Invoke();
                    _exceededMaxAngle = !withinMaxAngle;
                }
                return Quaternion.LookRotation(
                    withinMaxAngle ? dir : referenceAngle.forward, up);
            }
            
            return Quaternion.LookRotation(dir, up);
        }
    }
}