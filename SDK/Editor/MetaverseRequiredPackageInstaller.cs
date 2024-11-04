#if !CLOUD_BUILD_PLATFORM
using System.Linq;
using MetaverseCloudEngine.Unity.Installer.Editor;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    public class MetaverseRequiredPackageInstaller : AssetPostprocessor
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

        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (deletedAssets.Length <= 0 ||
                !deletedAssets.Any(x => x.Contains("Packages/com.reachcloud.metaverse-cloud-sdk"))) 
                return;
            ScriptingDefines.Remove(new[] {ScriptingDefines.DefaultSymbols});
        }

        [UsedImplicitly]
        private static bool TryUpdatePackages()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _packageRequest = null;
                return true;
            }

            _packageRequest ??= Client.AddAndRemove(packagesToAdd: PackagesToInstall);
            switch (_packageRequest.Status)
            {
                case StatusCode.InProgress:
                    return false;
                case StatusCode.Success:
                    OnPackagesInstalled();
                    break;
                default:
                    OnPackagesFailed();
                    break;
            }

            _packageRequest = null;
            return true;

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

        [UsedImplicitly]
        private static void ShowProgressBar() => EditorUtility.DisplayProgressBar("Metaverse Cloud Engine Dependencies", "Ensuring Package Dependencies...", 1);

        [UsedImplicitly]
        private static void HideProgressBar() => EditorUtility.ClearProgressBar();
    }
}
#endif