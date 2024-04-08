#if !CLOUD_BUILD_PLATFORM
using MetaverseCloudEngine.Unity.Installer.Editor;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseRequiredPackageInstaller
    {
        private const string InitialUpdateCheckFlag = "MVCE_InitialUpdateCheck";

        private static readonly string[] PackagesToInstall =
        {
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "https://github.com/Unity-Technologies/AssetBundles-Browser.git",
            "https://github.com/ReachCloudDevelopers/GLTFUtility.git",
#if !METAVERSE_CLOUD_ENGINE_INTERNAL
            "https://github.com/ReachCloudDevelopers/MetaverseCloudEngine.Unity.SDK.git",
#endif
        };

        private static AddAndRemoveRequest _packageRequest;

        [InitializeOnLoadMethod]
        private static void InstallPackages()
        {
            if (EditorApplication.isCompiling)
                return;

            if (!SessionState.GetBool(InitialUpdateCheckFlag, false))
            {
#if !METAVERSE_CLOUD_ENGINE_INTERNAL
                while (!TryUpdatePackages())
                {
                    ShowProgressBar();
                    System.Threading.Thread.Sleep(500);
                }

                HideProgressBar();
#endif
                SessionState.SetBool(InitialUpdateCheckFlag, true);
            }
        }

        private static bool TryUpdatePackages()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _packageRequest = null;
                return true;
            }

            _packageRequest ??= Client.AddAndRemove(packagesToAdd: PackagesToInstall);
            if (_packageRequest.Status != StatusCode.InProgress)
            {
                if (_packageRequest.Status == StatusCode.Success)
                    OnPackagesInstalled();
                else OnPackagesFailed();
                _packageRequest = null;
                return true;
            }

            return false;
        }

        private static void OnPackagesInstalled()
        {
            ScriptingDefines.Add(new[] {ScriptingDefines.DefaultSymbols});
            Client.Resolve();
            EditorUtility.ClearProgressBar();
        }

        private static void OnPackagesFailed()
        {
            Debug.LogError("Failed to install packages: " + _packageRequest.Error.message);
            EditorUtility.ClearProgressBar();
        }

        private static void ShowProgressBar() => EditorUtility.DisplayProgressBar("Metaverse Cloud Engine Dependencies", "Ensuring Package Dependencies...", 1);

        private static void HideProgressBar() => EditorUtility.ClearProgressBar();
    }
}
#endif