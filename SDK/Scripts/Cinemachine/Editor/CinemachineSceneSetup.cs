#if UNITY_EDITOR

using Cinemachine;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces.Abstract;
using MetaverseCloudEngine.Unity.Cinemachine.Editor.Components;
using UnityEngine;

#if MV_UNITY_AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

namespace MetaverseCloudEngine.Unity.Cinemachine.Editor
{
    public class CinemachineSceneSetup : IMetaSpaceSceneSetup
    {
        public bool SetupMainCamera(Camera camera)
        {
            if (camera.GetComponent<MetaSpaceCameraCinemachineWarningIgnore>())
                return false;

#if MV_UNITY_AR_FOUNDATION
#pragma warning disable CS0618
            if (camera.GetComponent<ARCameraManager>() || camera.GetComponentInParent<ARSessionOrigin>())
#pragma warning restore CS0618
                return false;
#endif

            var cinemachineBrain = camera.GetComponent<CinemachineBrain>();
            if (!cinemachineBrain &&
                UnityEditor.EditorUtility.DisplayDialog(
                    "Cinemachine Support",
                    "The metaverse cloud engine uses Cinemachine for the Built-In Player. Would you like to add a CinemachineBrain to your main camera?", "Yes", "No"))
            {
                var cm = camera.gameObject.AddComponent<CinemachineBrain>();
                cm.m_UpdateMethod = CinemachineBrain.UpdateMethod.SmartUpdate;
                cm.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
                camera.nearClipPlane = 0.05f;
            }

            camera.gameObject.AddComponent<MetaSpaceCameraCinemachineWarningIgnore>();
            return true;
        }
    }
}

#endif