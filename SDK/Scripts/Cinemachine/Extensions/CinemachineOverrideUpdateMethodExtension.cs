using Cinemachine;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Cinemachine.Extensions
{
    public class CinemachineOverrideUpdateMethodExtension : CinemachineExtension
    {
        [Tooltip("The update method to use for the brain.")]
        [SerializeField] private CinemachineBrain.UpdateMethod updateMethod = CinemachineBrain.UpdateMethod.LateUpdate;

        public CinemachineBrain.UpdateMethod UpdateMethod
        {
            get => updateMethod;
            set => updateMethod = value;
        }
        
        public int UpdateMethodInt
        {
            get => (int) updateMethod;
            set => updateMethod = (CinemachineBrain.UpdateMethod) value;
        }
        
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize)
                return;
            
            var brain = CinemachineCore.Instance.FindPotentialTargetBrain(vcam);
            if (brain == null)
                return;
            
            brain.m_UpdateMethod = updateMethod;
        }
    }
}