using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    /// <summary>
    /// Provides the avatar system with extra information about the avatar, to help better facilitate IK
    /// and other avatar related features.
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    public class AvatarMetaData : TriInspectorMonoBehaviour
    {
        [Header("Head")]
        public Vector3 headForwardAxis = Vector3.forward;
        public Vector3 headUpAxis = Vector3.up;

        [Header("Left Hand")]
        public Vector3 leftHandWristToPalmAxis = Vector3.up;
        public Vector3 leftHandPalmDownAxis = Vector3.forward;
        
        [Header("Right Hand")]
        public Vector3 rightHandWristToPalmAxis = Vector3.up;
        public Vector3 rightHandPalmDownAxis = Vector3.forward;
        
        private static readonly Quaternion DefaultHeadRotationOffset = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        private static readonly Quaternion DefaultLeftHandRotationOffset = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        private static readonly Quaternion DefaultRightHandRotationOffset = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        
        public Quaternion GetHeadRotationOffset() => 
            Quaternion.Inverse(DefaultHeadRotationOffset) * Quaternion.LookRotation(headForwardAxis, headUpAxis);
        
        public Quaternion GetLeftHandRotationOffset() => 
            Quaternion.Inverse(DefaultLeftHandRotationOffset) * Quaternion.LookRotation(leftHandPalmDownAxis, leftHandWristToPalmAxis);

        public Quaternion GetRightHandRotationOffset() => 
            Quaternion.Inverse(DefaultRightHandRotationOffset) * Quaternion.LookRotation(rightHandPalmDownAxis, rightHandWristToPalmAxis);
    }
}