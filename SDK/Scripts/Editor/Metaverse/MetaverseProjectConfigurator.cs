using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;

using UnityEngine;
using UnityEngine.Rendering;

#if MV_AR_CORE
using UnityEngine.XR.ARCore;
#endif
#if !UNITY_IOS
#if MV_OCULUS_PLUGIN
using Unity.XR.Oculus;
#endif
#if MV_OPENXR
using UnityEngine.XR.OpenXR;
#endif
#endif

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseProjectConfigurator
    {
        [InitializeOnLoadMethod]
        public static void Init()
        {
            if (!CanConfigureProject())
                return;

            static void ConfigureProjectNextEditorUpdate()
            {
                EditorApplication.update -= ConfigureProjectNextEditorUpdate;

#if !METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                CheckColorSpace();

                EnsureGraphicsApis();

                CleanupPreloadedAssets();

                ConfigureXRLoaders();

                EnsureForwardRenderingPath();
#endif
            }

            EditorApplication.update += ConfigureProjectNextEditorUpdate;
        }

        public static void ConfigureXRLoaders(bool force = false)
        {
#if MV_XR_MANAGEMENT && (MV_OCULUS_PLUGIN || MV_OPENXR || MV_AR_CORE)
            const string configureXrSessionFlag = "MVCE_ConfigureXR_Session";
            var configureXrProjectFlag = $"MVCE_ConfigureXR_Project_{PlayerSettings.productGUID}";

            if ((!SessionState.GetBool(configureXrSessionFlag, false) && EditorPrefs.GetBool(configureXrProjectFlag, true)) || force)
            {
#if !UNITY_IOS
                if (!MetaverseEditorUtils.IsXrLoaderConfigured(BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader") &&
                    !MetaverseEditorUtils.IsXrLoaderConfigured(BuildTargetGroup.Android, "Unity.XR.Oculus.OculusLoader") &&
                    !MetaverseEditorUtils.IsXrLoaderConfigured(BuildTargetGroup.Android, "UnityEngine.XR.OpenXR.OpenXRLoader") &&
                    !MetaverseEditorUtils.IsXrLoaderConfigured(BuildTargetGroup.Standalone, "UnityEngine.XR.OpenXR.OpenXRLoader") &&
                    !force)
                {
                    if (!EditorUtility.DisplayDialog("Enable XR/AR", "Would you like to configure XR/AR for this project?", "Yes (Recommended)", "No"))
                    {
                        EditorPrefs.SetBool(configureXrProjectFlag, false);
                        return;
                    }
                }

                MetaverseEditorUtils.ConfigureXRLoaders(BuildTargetGroup.Android, new[] 
                    {
#if MV_OCULUS_PLUGIN
                        typeof(OculusLoader).FullName, 
#else
#if MV_OPENXR
                        typeof(OpenXRLoader).FullName,
#endif
#endif
#if MV_AR_CORE
                        typeof(ARCoreLoader).FullName
#endif
                    }, 
                    xrsdk: MetaverseCloudEngine.Unity.XR.XRSDK.None,
                    changeInitOnStartup: false);

                MetaverseEditorUtils.ConfigureXRLoaders(BuildTargetGroup.Standalone, new[] 
                    {
                        typeof(OpenXRLoader).FullName,
                    },
                    xrsdk: MetaverseCloudEngine.Unity.XR.XRSDK.None,
                    changeInitOnStartup: false);
#endif
                SessionState.SetBool(configureXrSessionFlag, true);
            }
#endif
        }

        [MenuItem(MetaverseConstants.ProductName + "/Project/Allow Project Configuration")]
        private static void CanConfigureProject_MenuItem() => CanConfigureProject(true);

        private static bool CanConfigureProject(bool force = false)
        {
            var configureProjectFlag = $"MVCE_ConfigureProject_{PlayerSettings.productGUID}";

            if ((!EditorPrefs.HasKey(configureProjectFlag) || force) && 
                (!Application.isBatchMode && EditorUtility.DisplayDialog("Configure Project", MetaverseConstants.ProductName + " would like to configure your project to ensure a smooth experience with the SDK. Would you like to allow changes to be made to your project settings?", "Yes (Recommended)", "No")))
            {
                EditorPrefs.SetBool(configureProjectFlag, false);
                return false;
            }

            if (EditorPrefs.GetBool(configureProjectFlag, false))
                return false;

            EditorPrefs.SetBool(configureProjectFlag, true);
            return true;
        }

        private static void EnsureGraphicsApis()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);

            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.OpenGLCore, GraphicsDeviceType.Vulkan });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { GraphicsDeviceType.Metal });
        }

        private static void EnsureForwardRenderingPath()
        {
            var groups = MetaverseEditorUtils.GetSupportedBuildTargetGroups();
            foreach (var gr in groups)
            {
                var tier1 = EditorGraphicsSettings.GetTierSettings(gr, GraphicsTier.Tier1);
                var tier2 = EditorGraphicsSettings.GetTierSettings(gr, GraphicsTier.Tier2);
                var tier3 = EditorGraphicsSettings.GetTierSettings(gr, GraphicsTier.Tier3);
                tier1.renderingPath = RenderingPath.Forward;
                tier2.renderingPath = RenderingPath.Forward;
                tier3.renderingPath = RenderingPath.Forward;
                EditorGraphicsSettings.SetTierSettings(gr, GraphicsTier.Tier1, tier1);
                EditorGraphicsSettings.SetTierSettings(gr, GraphicsTier.Tier2, tier2);
                EditorGraphicsSettings.SetTierSettings(gr, GraphicsTier.Tier3, tier3);
            }
        }

        private static void CheckColorSpace()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Uninitialized)
            {
                EditorApplication.update += CheckColorSpaceInEditorUpdate;
                return;
            }

            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                EditorApplication.update -= CheckColorSpaceInEditorUpdate;
                return;
            }

            EditorUtility.DisplayDialog(
                "Color Space",
                $"The '{PlayerSettings.colorSpace}' color space is not supported. " +
                "Only the 'Linear' color space is supported in the Metaverse Cloud SDK. " +
                "Your project will now be switched to the 'Linear' Color space. This may " +
                "take quite some time depending on the size of your project.",
                "Ok");

            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorApplication.update -= CheckColorSpaceInEditorUpdate;
        }

        private static void CheckColorSpaceInEditorUpdate()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Uninitialized)
                CheckColorSpace();
        }

        private static void CleanupPreloadedAssets()
        {
            var preloadedAssets = PlayerSettings.GetPreloadedAssets().Where(x => x).ToList();
            PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
        }
    }
}