#if !CLOUD_BUILD_PLATFORM && UNITY_EDITOR
using System;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MetaverseCloudEngine.Unity.Installer
{
    public class MetaverseRequiredPackageInstaller : AssetPostprocessor
    {
        private const string InitialUpdateCheckFlag = "MVCE_InitialUpdateCheck";

        private static readonly string[] PackagesToInstall =
        {
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "https://github.com/Unity-Technologies/AssetBundles-Browser.git",
            "https://github.com/ReachCloudDevelopers/GLTFUtility.git",
        };

        private static AddAndRemoveRequest _packageRequest;
        
        [MenuItem("Assets/Metaverse Cloud Engine/Install Required Packages", false, int.MaxValue)]
        public static void ForceInstallPackages()
        {
            SessionState.SetBool(InitialUpdateCheckFlag, false);
            InstallPackages();
        }

        [DidReloadScripts]
        [InitializeOnLoadMethod]
        private static void InstallPackages()
        {
            if (EditorApplication.isCompiling) return;
            if (SessionState.GetBool(InitialUpdateCheckFlag, 
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                true
#else
                false
#endif
                    )) return;
            SessionState.SetBool(InitialUpdateCheckFlag, true);
            try 
            {
                ShowProgressBar();

                var list = Client.List();
                while (!list.IsCompleted)
                    System.Threading.Thread.Sleep(500);
                
                if (list.Status == StatusCode.Failure)
                {
                    Debug.LogError("Failed to fetch package list: " + list.Error.message);
                    return;
                }
                
                var package = list.Result.FirstOrDefault(x => x.name.StartsWith("com.reachcloud.metaverse-cloud-sdk"));
                var latestCommitHash = GetLatestCommit(package);
                
                if (!string.IsNullOrEmpty(latestCommitHash) && !EditorUtility.DisplayDialog("Metaverse Cloud Engine SDK Update", 
                        "A new version of Metaverse Cloud Engine SDK is available. This may take a few minutes but rest assured we'll be done in no time.", 
                        "Update (Recommended)", "Skip"))
                    return;

                while (!TryUpdatePackages(latestCommitHash))
                    System.Threading.Thread.Sleep(500);

                if (!string.IsNullOrEmpty(latestCommitHash))
                    EditorApplication.OpenProject(Environment.CurrentDirectory);
            }
            finally 
            {
                HideProgressBar();
            }
        }

        private static string GetLatestCommit(PackageInfo package)
        {
            if (package is not { git: not null })
                return string.Empty;
            
            var currentVersion = package.git.hash;
            var httpClient = new WebClient();
            httpClient.Headers.Add("Accept", "application/vnd.github+json");
            httpClient.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            httpClient.Headers.Add("User-Agent", "MetaverseCloudEngine.Unity.SDK");
            var response = httpClient.DownloadString("https://api.github.com/repos/ReachCloudDevelopers/MetaverseCloudEngine.Unity.SDK/commits?per_page=1");
            var match = System.Text.RegularExpressions.Regex.Match(response, "\"sha\"\\s*:\\s*\"(.+?)\"");
            if (!match.Success)
                return string.Empty;
            
            var latestCommitHash = match.Groups[1].Value;
            if (currentVersion == latestCommitHash)
                return string.Empty;

            Debug.Log($"Updating Metaverse Cloud Engine SDK: {currentVersion} -> {latestCommitHash}");
            return latestCommitHash;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (AssetDatabase.FindAssets("", new[] { "Packages/com.reachcloud.metaverse-cloud-sdk" }).Length > 0)
            {
                ScriptingDefines.AddDefaultSymbols();
                return;
            }
            
            if (!ScriptingDefines.AreDefaultSymbolsDefined) 
                return;
            
            SessionState.EraseBool(InitialUpdateCheckFlag);
            ScriptingDefines.OnMainPackageUninstalled();
            ScriptingDefines.RemoveDefaultSymbols(
                EditorUtility.DisplayDialog("Uninstall Metaverse Cloud Engine SDK", 
                    "We're sorry to see you go! Nevertheless thank you for using the SDK. Would you like to keep the integrations enabled in case you decide to install the SDK again?", 
                    "Yes (Recommended)", "No") == false);
            
            EditorApplication.OpenProject(Environment.CurrentDirectory);
        }

        [UsedImplicitly]
        private static bool TryUpdatePackages(string commitHash)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _packageRequest = null;
                return true;
            }

            var packagesToAdd = PackagesToInstall.ToArray();
            if (!string.IsNullOrEmpty(commitHash))
            {
                packagesToAdd = packagesToAdd.Concat(!string.IsNullOrEmpty(commitHash) ? new[] {
                    $"https://github.com/ReachCloudDevelopers/MetaverseCloudEngine.Unity.SDK.git#{commitHash}"
                } : Array.Empty<string>())
                .ToArray();
            }

            if (packagesToAdd.Length == 0)
            {
                OnPackagesInstalled();
                _packageRequest = null;
                return true;
            }

            _packageRequest ??= Client.AddAndRemove(packagesToAdd: packagesToAdd);

            if (EditorUtility.DisplayCancelableProgressBar(
                    "Metaverse Cloud Engine SDK Update",
                    "Updating the SDK to the latest version...",
                    (_packageRequest.Result?.Count() ?? 0f) / packagesToAdd.Length))
            {
                OnPackagesFailed();
                _packageRequest = null;
                return true;
            }

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
            ScriptingDefines.AddDefaultSymbols();
            Client.Resolve();
            EditorUtility.ClearProgressBar();
        }

        private static void OnPackagesFailed()
        {
            EditorUtility.ClearProgressBar();
            if (_packageRequest.Error is null) return;
            EditorUtility.DisplayDialog("Metaverse Cloud Engine SDK Installation Failed", $"Failed to install Metaverse Cloud Engine SDK packages: {_packageRequest.Error.message}", "OK");
            throw new Exception("Failed to install packages: " + _packageRequest.Error.message);
        }

        [UsedImplicitly]
        private static void ShowProgressBar() => EditorUtility.DisplayProgressBar("Metaverse Cloud Engine Dependencies", "Ensuring Package Dependencies...", 1);

        [UsedImplicitly]
        private static void HideProgressBar() => EditorUtility.ClearProgressBar();
    }
}
#endif
