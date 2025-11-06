#if !CLOUD_BUILD_PLATFORM && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;
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
        private const string GitHubRawBase = "https://raw.githubusercontent.com/ReachCloudDevelopers/MetaverseCloudEngine.Unity.SDK";
        private const string PackageJsonRelativePath = "Packages/MetaverseCloudEngine.Unity.SDK/package.json";

        private static readonly string[] PackagesToInstall =
        {
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "https://github.com/Unity-Technologies/AssetBundles-Browser.git",
            "https://github.com/ReachCloudDevelopers/GLTFUtility.git",
        };

        private static readonly Queue<string> PendingPackages = new();
        private static int _packagesTotal;
        private static int _packagesProcessed;
        private static AddRequest _packageRequest;
        private static string _currentPackage;
        private static double _currentPackageStartTime;
        
        [MenuItem("Assets/Metaverse Cloud Engine/Force Update", false, int.MaxValue)]
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
                
                if (!string.IsNullOrEmpty(latestCommitHash))
                {
                    if (TryBuildPackageUpdateInfo(package, latestCommitHash, out var updateInfo))
                    {
                        Debug.Log($"Successfully built package update info. Changelog length: {updateInfo?.FullChangelog?.Length ?? 0}");
                        if (!MetaverseSdkUpdateWindow.ShowModal(updateInfo))
                            return;
                    }
                    else
                    {
                        Debug.LogWarning("Failed to build package update info, using fallback dialog.");
                        const string fallbackMessage = "A newer Metaverse Cloud Engine SDK build is available. Install it now to ensure all required packages stay in sync?";
                        if (!EditorUtility.DisplayDialog("Metaverse Cloud Engine SDK Update", fallbackMessage, "Install Update", "Skip"))
                            return;
                    }
                }

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
            using var httpClient = CreateGitHubWebClient();
            var response = httpClient.DownloadString("https://api.github.com/repos/ReachCloudDevelopers/MetaverseCloudEngine.Unity.SDK/commits?per_page=1");
            var match = System.Text.RegularExpressions.Regex.Match(response, "\"sha\"\\s*:\\s*\"(.+?)\"");
            if (!match.Success)
                return string.Empty;
            
            var latestCommitHash = match.Groups[1].Value;
            if (currentVersion == latestCommitHash)
                return string.Empty;

            Debug.Log($"Metaverse Cloud Engine SDK update available: {currentVersion} -> {latestCommitHash}");
            return latestCommitHash;
        }

        private static WebClient CreateGitHubWebClient()
        {
            var httpClient = new WebClient();
            httpClient.Headers.Add("Accept", "application/vnd.github+json");
            httpClient.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            httpClient.Headers.Add("User-Agent", "MetaverseCloudEngine.Unity.SDK");
            return httpClient;
        }

        private static bool TryBuildPackageUpdateInfo(PackageInfo package, string commitHash, out MetaverseSdkUpdateInfo info)
        {
            info = null;
            try
            {
                using var httpClient = CreateGitHubWebClient();
                var packageJsonUrl = $"{GitHubRawBase}/{commitHash}/{PackageJsonRelativePath}";
                Debug.Log($"Fetching package.json from: {packageJsonUrl}");
                var json = httpClient.DownloadString(packageJsonUrl);
                var jObject = JObject.Parse(json);

                var version = jObject["version"]?.Value<string>() ?? commitHash;
                var description = jObject["description"]?.Value<string>() ?? string.Empty;
                Debug.Log($"Retrieved description length: {description.Length}");
                var normalizedChangelog = NormalizeChangelog(description);
                Debug.Log($"Normalized changelog length: {normalizedChangelog.Length}");

                info = new MetaverseSdkUpdateInfo
                {
                    CurrentVersion = package?.version ?? "Unknown",
                    AvailableVersion = version,
                    CommitHash = commitHash,
                    FullChangelog = normalizedChangelog,
                    LatestEntry = ExtractLatestChangelog(normalizedChangelog, version)
                };

                Debug.Log($"Latest entry length: {info.LatestEntry?.Length ?? 0}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Metaverse Cloud Engine: unable to load update metadata. {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}");
                info = null;
                return false;
            }
        }

        private static string NormalizeChangelog(string changelog)
        {
            if (string.IsNullOrEmpty(changelog))
                return string.Empty;

            var normalized = changelog.Replace("\r\n", "\n");
            normalized = normalized.Replace("\\n", "\n");
            return normalized.Trim();
        }

        private static string ExtractLatestChangelog(string changelog, string version)
        {
            if (string.IsNullOrEmpty(changelog))
                return string.Empty;

            var header = $"## {version}";
            var startIndex = changelog.IndexOf(header, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
                startIndex = changelog.IndexOf("## ", StringComparison.OrdinalIgnoreCase);

            if (startIndex < 0)
                return changelog.Trim();

            var nextIndex = changelog.IndexOf("\n## ", startIndex + 3, StringComparison.OrdinalIgnoreCase);
            if (nextIndex < 0)
                nextIndex = changelog.Length;

            var section = changelog.Substring(startIndex, nextIndex - startIndex);
            return section.Trim();
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

            if (!PendingPackages.Any() && _packageRequest is null && _packagesProcessed == 0)
            {
                EnqueuePackages(commitHash);
                if (_packagesTotal == 0)
                {
                    OnPackagesInstalled();
                    ResetPackageUpdateState();
                    return true;
                }
            }

            if (_packageRequest is null && PendingPackages.Count > 0)
            {
                var nextPackage = PendingPackages.Peek();
                _currentPackage = nextPackage;
                _currentPackageStartTime = EditorApplication.timeSinceStartup;
                _packageRequest = Client.Add(nextPackage);
            }

            if (_packageRequest is null)
                return false;

            if (EditorUtility.DisplayCancelableProgressBar(
                    "Metaverse Cloud Engine Packages",
                    GetProgressMessage(),
                    GetProgressValue()))
            {
                var canceledRequest = _packageRequest;
                ResetPackageUpdateState();
                OnPackagesCancelled(canceledRequest);
                return true;
            }

            switch (_packageRequest.Status)
            {
                case StatusCode.InProgress:
                    return false;
                case StatusCode.Success:
                    var completedPackage = _currentPackage;
                    PendingPackages.Dequeue();
                    _packagesProcessed++;
                    _packageRequest = null;
                    _currentPackage = null;
                    LogPackageCompleted(completedPackage);
                    break;
                default:
                    var failedRequest = _packageRequest;
                    ResetPackageUpdateState();
                    OnPackagesFailed(failedRequest);
                    return true;
            }

            if (!PendingPackages.Any())
            {
                OnPackagesInstalled();
                ResetPackageUpdateState();
                return true;
            }

            return false;

        }

        private static void OnPackagesInstalled()
        {
            ScriptingDefines.AddDefaultSymbols();
            Client.Resolve();
            EditorUtility.ClearProgressBar();
            Debug.Log("Metaverse Cloud Engine: package check complete.");
        }

        private static void OnPackagesFailed(Request request)
        {
            EditorUtility.ClearProgressBar();
            if (request?.Error is null) return;
            EditorUtility.DisplayDialog("Metaverse Cloud Engine SDK Installation Failed", $"Failed to install Metaverse Cloud Engine SDK packages: {request.Error.message}", "OK");
            throw new Exception("Failed to install packages: " + request.Error.message);
        }

        private static void OnPackagesCancelled(Request _)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogWarning("Metaverse Cloud Engine: package check cancelled by user.");
        }

        private static void EnqueuePackages(string commitHash)
        {
            ResetPackageUpdateState();

            foreach (var package in PackagesToInstall)
                PendingPackages.Enqueue(package);

            if (!string.IsNullOrEmpty(commitHash))
                PendingPackages.Enqueue($"https://github.com/ReachCloudDevelopers/MetaverseCloudEngine.Unity.SDK.git#{commitHash}");

            _packagesTotal = PendingPackages.Count;
        }

        private static void ResetPackageUpdateState()
        {
            PendingPackages.Clear();
            _packageRequest = null;
            _packagesProcessed = 0;
            _packagesTotal = 0;
            _currentPackage = null;
            _currentPackageStartTime = 0;
        }

        private static string GetProgressMessage()
        {
            if (_packagesTotal == 0)
                return "Ensuring package dependencies...";

            var currentIndex = Math.Min(_packagesProcessed + 1, _packagesTotal);
            var currentName = string.IsNullOrEmpty(_currentPackage) ? "Finalizing" : GetFriendlyPackageName(_currentPackage);

            return $"Ensuring package {currentIndex}/{_packagesTotal}: {currentName}";
        }

        private static float GetProgressValue()
        {
            if (_packagesTotal == 0)
                return 1f;

            var baseProgress = _packagesProcessed / (float)_packagesTotal;

            if (_packageRequest is null || string.IsNullOrEmpty(_currentPackage))
                return baseProgress;

            var elapsed = EditorApplication.timeSinceStartup - _currentPackageStartTime;
            var simulated = Math.Min(0.9f, (float)(elapsed / 10f));

            return Mathf.Clamp01(baseProgress + simulated / _packagesTotal);
        }

        private static string GetFriendlyPackageName(string package)
        {
            if (string.IsNullOrEmpty(package))
                return "Unknown";

            var withoutHash = package.Split('#').FirstOrDefault() ?? package;
            var withoutQuery = withoutHash.Split('?').FirstOrDefault() ?? withoutHash;
            withoutQuery = withoutQuery.TrimEnd('/');
            var parts = withoutQuery.Split('/');
            var last = parts.LastOrDefault() ?? withoutQuery;
            return last.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? last.Substring(0, last.Length - 4)
                : last;
        }

        private static void LogPackageCompleted(string package)
        {
            if (string.IsNullOrEmpty(package))
                return;

            var friendlyName = GetFriendlyPackageName(package);
            Debug.Log($"Metaverse Cloud Engine: ensured package '{friendlyName}'.");
        }

        [UsedImplicitly]
        private static void ShowProgressBar() => EditorUtility.DisplayProgressBar("Metaverse Cloud Engine Packages", "Preparing package checks...", 0f);

        [UsedImplicitly]
        private static void HideProgressBar() => EditorUtility.ClearProgressBar();
    }
}
#endif
