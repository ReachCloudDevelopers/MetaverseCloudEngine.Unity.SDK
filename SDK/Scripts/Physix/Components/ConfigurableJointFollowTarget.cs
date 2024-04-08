using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix
{
    [RequireComponent(typeof(ConfigurableJoint))]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Follow Target (Configurable Joint)")]
    [HideMonoScript]
    public class ConfigurableJointFollowTarget : TriInspectorMonoBehaviour
    {
        [SerializeField] private bool followRotation = true;
        [ShowIf(nameof(followRotation))]
        [SerializeField] private Transform rotationTarget;
        [ShowIf(nameof(followRotation))]
        [SerializeField] private bool useConnectedBodyLocalSpaceRotation;
        [ShowIf(nameof(followPosition))]
        [SerializeField] private bool followPosition = true;
        [SerializeField] private Transform positionTarget;
        [ShowIf(nameof(followPosition))]
        [SerializeField] private bool followAnchor;

        private ConfigurableJoint _configurableJoint;
        private Quaternion _initialLocalRotation;
        private Quaternion _initialWorldRotation;

        public bool FollowPosition { get => followPosition; set => followPosition = value; }
        public bool FollowRotation { get => followRotation; set => followRotation = value; }
        public bool FollowAnchor { get => followAnchor; set => followAnchor = value; }
        public bool UseConnectedBodyLocalSpaceRotation { get => useConnectedBodyLocalSpaceRotation; set => useConnectedBodyLocalSpaceRotation = value; }
        public Transform RotationTarget { get => rotationTarget; set => rotationTarget = value; }
        public Transform PositionTarget { get => positionTarget; set => positionTarget = value; }
        public Transform Target { set { PositionTarget = value; RotationTarget = value; } }

        private void Awake()
        {
            _configurableJoint = GetComponent<ConfigurableJoint>();
        }

        private void Start()
        {
            _initialLocalRotation = GetLocalRotation(_configurableJoint.transform);
            _initialWorldRotation = _configurableJoint.transform.rotation;
        }

        private void FixedUpdate()
        {
            if (followRotation)
            {
                if (_configurableJoint.configuredInWorldSpace)
                    _configurableJoint.SetTargetRotation(rotationTarget.rotation, _initialWorldRotation);
                else
                    _configurableJoint.SetTargetRotationLocal(GetLocalRotation(rotationTarget), _initialLocalRotation);
            }

            if (followPosition)
            {
                if (!followAnchor)
                    _configurableJoint.SetTargetPositionLocal(rotationTarget.localPosition);
                else
                    _configurableJoint.connectedAnchor = _configurableJoint.connectedBody ? _configurableJoint.connectedBody.transform.InverseTransformPoint(rotationTarget.position) : rotationTarget.position;
            }
        }

        private Quaternion GetLocalRotation(Transform target)
        {
            return _configurableJoint.connectedBody && useConnectedBodyLocalSpaceRotation 
                ? _configurableJoint.connectedBody.transform.InverseTransformRotation(target.rotation) 
                : target.localRotation;
        }
    }
}
