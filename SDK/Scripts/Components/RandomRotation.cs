using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component to randomize the rotation of a transform.
    /// </summary>
    [HideMonoScript]
    public class RandomRotation : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The randomization mode.
        /// </summary>
        public enum RandomizationMode
        {
            /// <summary>
            /// Randomize the rotation on a single axis.
            /// </summary>
            Simple,
            /// <summary>
            /// Randomize the rotation on each axis.
            /// </summary>
            PerAxis,
        }

        [InfoBox("Target is optional.")]
        [SerializeField] private Transform target;
        [SerializeField] private Space targetApplySpace = Space.World;

        [Space]
        [SerializeField] private bool randomizeOnStart = true;

        [Space]
        [SerializeField] private RandomizationMode mode = RandomizationMode.Simple;
        [HideIf(nameof(IsSimpleMode))][SerializeField] private Vector2 xAngleRange = new(-180, 180);
        [HideIf(nameof(IsSimpleMode))][SerializeField] private Vector2 yAngleRange = new(-180, 180);
        [HideIf(nameof(IsSimpleMode))][SerializeField] private Vector2 zAngleRange = new(-180, 180);
        [ShowIf(nameof(IsSimpleMode))][SerializeField, Min(0)] private float angleRange = 180f;
        [ShowIf(nameof(IsSimpleMode))][SerializeField] private bool randomizeX = true;
        [ShowIf(nameof(IsSimpleMode))][SerializeField] private bool randomizeY = true;
        [ShowIf(nameof(IsSimpleMode))][SerializeField] private bool randomizeZ = true;

        [Space]
        [SerializeField, Min(0)] private float multiplier = 1f;

        [Space]
        [SerializeField] private UnityEvent<Quaternion> onRotation;
        [SerializeField] private UnityEvent<Vector3> onEulerAngles;

        private bool _hasStarted;

        public bool IsSimpleMode => mode == RandomizationMode.Simple;
        public float Multiplier { get => multiplier; set => multiplier = value; }
        public bool SimpleModeRandomizeX { get => randomizeX; set => randomizeX = value; }
        public bool SimpleModeRandomizeY { get => randomizeY; set => randomizeY = value; }
        public bool SimpleModeRandomizeZ { get => randomizeZ; set => randomizeZ = value; }
        public float SimpleModeAngleRange { get => angleRange; set => angleRange = value; }
        public Vector2 PerAxisZAngleRange { get => zAngleRange; set => zAngleRange = value; }
        public Vector2 PerAxisYAngleRange { get => yAngleRange; set => yAngleRange = value; }
        public Vector2 PerAxisXAngleRange { get => xAngleRange; set => xAngleRange = value; }

        private void Start()
        {
            _hasStarted = true;

            if (randomizeOnStart)
                Randomize();
        }

        public void Randomize()
        {
            if (!_hasStarted && randomizeOnStart)
                return;

            if (!isActiveAndEnabled)
                return;

            Quaternion rotation = GetRandomRotation();
            if (target)
            {
                switch (targetApplySpace)
                {
                    case Space.World:
                        target.rotation = rotation;
                        break;
                    case Space.Self:
                        target.rotation *= rotation;
                        break;
                }
            }

            onRotation?.Invoke(rotation);
            onEulerAngles?.Invoke(rotation.eulerAngles);
        }

        private Quaternion GetRandomRotation()
        {
            if (IsSimpleMode)
            {
                float xAngle = Random.Range(-angleRange, angleRange) * (randomizeX ? 1 : 0) * multiplier;
                float yAngle = Random.Range(-angleRange, angleRange) * (randomizeY ? 1 : 0) * multiplier;
                float zAngle = Random.Range(-angleRange, angleRange) * (randomizeZ ? 1 : 0) * multiplier;
                return Quaternion.Euler(xAngle, yAngle, zAngle);
            }
            else
            {
                float xAngle = Random.Range(xAngleRange.x, xAngleRange.y) * multiplier;
                float yAngle = Random.Range(yAngleRange.x, yAngleRange.y) * multiplier;
                float zAngle = Random.Range(zAngleRange.x, zAngleRange.y) * multiplier;
                return Quaternion.Euler(xAngle, yAngle, zAngle);
            }
        }
    }
}
