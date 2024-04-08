using Cinemachine;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Cinemachine.Components
{
    public class CinemachineInheritAxisValues : CinemachineExtension
    {
        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
        }

        public override bool OnTransitionFromCamera(ICinemachineCamera fromCam, Vector3 worldUp, float deltaTime)
        {
            if (fromCam is not CinemachineFreeLook fromFreeLook)
                return base.OnTransitionFromCamera(fromCam, worldUp, deltaTime);

            CinemachineFreeLook thisFreeLook = VirtualCamera as CinemachineFreeLook;
            if (thisFreeLook == null)
                return base.OnTransitionFromCamera(fromCam, worldUp, deltaTime);

            AxisState axis = thisFreeLook.m_XAxis;
            axis.Value = fromFreeLook.m_XAxis.Value;
            thisFreeLook.m_XAxis = axis;

            axis = thisFreeLook.m_YAxis;
            axis.Value = fromFreeLook.m_YAxis.Value;
            thisFreeLook.m_YAxis = axis;

            return base.OnTransitionFromCamera(fromCam, worldUp, deltaTime);
        }
    }
}
