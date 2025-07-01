using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [HideMonoScript]
    public partial class MetaverseBodyEstimationAPI : TriInspectorMonoBehaviour
    {
        [Header("Events")]
        public UnityEvent onBodyEstimationStarted;
        public UnityEvent onBodyEstimationEnded;
        public UnityEvent<Texture> onInputTextureChanged;
        public UnityEvent onInputTextureNull;

        [Header("Begin Body Estimation Options")]
        public PoseEstimationSpace poseSpace = PoseEstimationSpace.World;

        public void BeginBodyEstimation()
        {
            BeginBodyEstimationInternal();
        }
        
        public void EndBodyEstimation()
        {
            EndBodyEstimationInternal();
        }
        
        public void SetPoseEstimationSpace(int space)
        {
            poseSpace = (PoseEstimationSpace) space;
            BeginBodyEstimationInternal();
        }

        partial void BeginBodyEstimationInternal();
        
        partial void EndBodyEstimationInternal();
    }
}