using Cinemachine;

namespace MetaverseCloudEngine.Unity.Cinemachine.Extensions
{
    /// <summary>
    /// This extension prevents a cinemachine virtual camera
    /// from modifying the near and far clipping planes of the brain camera.
    /// </summary>
    public class CinemachineIgnoreClippingPlanesExtension : CinemachineExtension
    {
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize)
                return;
            
            var brain = CinemachineCore.Instance.FindPotentialTargetBrain(vcam);
            if (brain == null)
                return;
            
            var cam = brain.OutputCamera;
            if (cam == null)
                return;
            
            state.Lens.NearClipPlane = cam.nearClipPlane;
            state.Lens.FarClipPlane = cam.farClipPlane;
        }
    }
}