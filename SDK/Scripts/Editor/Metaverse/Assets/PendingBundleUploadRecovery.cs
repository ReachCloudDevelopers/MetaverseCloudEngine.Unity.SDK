using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetaverseCloudEngine.Common.Enumerations;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    /// <summary>
    /// Recovers from domain/assembly reloads during bundle uploads.
    /// If an upload finishes while Unity is reloading, the normal completion log/dialog never runs.
    /// This watcher verifies completion against the server and logs a recovered success.
    /// </summary>
    [InitializeOnLoad]
    internal static class PendingBundleUploadRecovery
    {
        private const string PendingUploadSessionStateKey = "MetaverseCloudEngine_Unity_PendingBundleUpload";

        [Serializable]
        private sealed class PendingBundleUploadState
        {
            [Serializable]
            public sealed class PendingBuild
            {
                public int Platforms;
                public string OutputPath;
            }

            public int Version = 1;
            public string AssetType;
            public string AssetServerId;
            public string AssetName;
            public string BundlePath;
            public string AssetUpsertFormJson;
            public bool SuppressDialog;
            public PendingBuild[] Builds;
            public long TotalBytes;
            public string StartedUtc;
        }

        private static bool _monitoring;
        private static bool _checking;
        private static double _nextCheckAt;
        private static int _checkCount;

        static PendingBundleUploadRecovery()
        {
            EditorApplication.delayCall += StartIfPending;
        }

        private static void StartIfPending()
        {
            if (string.IsNullOrWhiteSpace(SessionState.GetString(PendingUploadSessionStateKey, null)))
                return;

            if (_monitoring)
                return;

            _monitoring = true;
            _nextCheckAt = EditorApplication.timeSinceStartup + 1.0d;
            _checkCount = 0;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (!_monitoring)
                return;

            if (EditorApplication.timeSinceStartup < _nextCheckAt)
                return;

            if (_checking)
                return;

            _nextCheckAt = EditorApplication.timeSinceStartup + 5.0d;
            _checkCount++;

            // Don’t hammer forever.
            if (_checkCount > 120)
            {
                StopMonitoring();
                return;
            }

            var json = SessionState.GetString(PendingUploadSessionStateKey, null);
            if (string.IsNullOrWhiteSpace(json))
            {
                StopMonitoring();
                return;
            }

            PendingBundleUploadState state;
            try
            {
                state = JsonConvert.DeserializeObject<PendingBundleUploadState>(json);
            }
            catch
            {
                // Corrupt state; clear so we don't loop.
                SessionState.EraseString(PendingUploadSessionStateKey);
                StopMonitoring();
                return;
            }

            if (state?.Builds == null || state.Builds.Length == 0)
                return;

            if (!Guid.TryParse(state.AssetServerId, out var assetId))
                return;

            if (MetaverseProgram.ApiClient?.Account?.IsLoggedIn != true)
                return;

            _checking = true;
            VerifyAndReportAsync(state, assetId);
        }

        private static async void VerifyAndReportAsync(PendingBundleUploadState state, Guid assetId)
        {
            try
            {
                var controller = ResolveController(state);
                if (controller == null)
                    return;

                dynamic response = await controller.FindAsync(assetId);
                if (response == null)
                    return;

                if (!(bool)response.Succeeded)
                    return;

                dynamic dto = await response.GetResultAsync();
                if (dto == null)
                    return;

                var requiredPlatforms = state.Builds
                    .SelectMany(b => ExpandPlatformFlags((Platform)b.Platforms))
                    .Distinct()
                    .ToArray();

                var uploadedPlatforms = new HashSet<Platform>();
                try
                {
                    IEnumerable platformsEnumerable = dto.Platforms as IEnumerable;
                    if (platformsEnumerable != null)
                    {
                        foreach (var p in platformsEnumerable)
                        {
                            try
                            {
                                dynamic platformDoc = p;
                                uploadedPlatforms.Add((Platform)platformDoc.Platform);
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }

                var missing = requiredPlatforms.Where(rp => !uploadedPlatforms.Contains(rp)).ToArray();
                if (missing.Length != 0)
                    return;

                // Success recovered.
                var assetName = state.AssetName;
                try { assetName ??= (string)dto.Name; } catch { /* ignored */ }

                MetaverseProgram.Logger.Log($"<b><color=green>Successfully</color></b> uploaded bundles for '{assetName}'. (Recovered after assembly reload)");

                // Show a small toast even for batch/suppressed uploads.
                ShowEditorNotification($"Upload complete: {assetName}");

                SessionState.EraseString(PendingUploadSessionStateKey);
                StopMonitoring();
            }
            catch (Exception e)
            {
                // Avoid breaking editor update loop.
                MetaverseProgram.Logger.Log($"Pending upload recovery check failed: {e.Message}");
            }
            finally
            {
                _checking = false;
            }
        }

        private static IEnumerable<Platform> ExpandPlatformFlags(Platform platforms)
        {
            foreach (Platform p in Enum.GetValues(typeof(Platform)))
            {
                if ((int)p == 0)
                    continue;
                if (platforms.HasFlag(p))
                    yield return p;
            }
        }

        private static dynamic ResolveController(PendingBundleUploadState state)
        {
            // Heuristic mapping based on asset type string.
            var type = state?.AssetType ?? string.Empty;
            if (type.Contains("MetaSpaces.MetaSpace", StringComparison.Ordinal) || type.Contains("MetaSpace", StringComparison.Ordinal))
                return MetaverseProgram.ApiClient?.MetaSpaces;
            if (type.Contains("LandPlot", StringComparison.Ordinal))
                return MetaverseProgram.ApiClient?.Land;
            if (type.Contains("MetaPrefab", StringComparison.Ordinal) || type.Contains("Prefab", StringComparison.Ordinal))
                return MetaverseProgram.ApiClient?.Prefabs;

            return null;
        }

        private static void ShowEditorNotification(string message, double seconds = 4d)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            EditorApplication.delayCall += () =>
            {
                try
                {
                    var window = EditorWindow.focusedWindow ?? SceneView.lastActiveSceneView;
                    if (window == null)
                        return;

                    window.ShowNotification(new GUIContent(message));

                    var start = EditorApplication.timeSinceStartup;
                    void Tick()
                    {
                        if (EditorApplication.timeSinceStartup - start < seconds)
                            return;
                        EditorApplication.update -= Tick;
                        try { window.RemoveNotification(); } catch { /* ignored */ }
                    }

                    EditorApplication.update += Tick;
                }
                catch
                {
                    // ignored
                }
            };
        }

        private static void StopMonitoring()
        {
            if (!_monitoring)
                return;

            _monitoring = false;
            _checking = false;
            EditorApplication.update -= Update;
        }
    }
}
