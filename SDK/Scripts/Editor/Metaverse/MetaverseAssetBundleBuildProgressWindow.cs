using System;
using System.Collections.Generic;
using System.IO;
using MetaverseCloudEngine.Common.Enumerations;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    internal class MetaverseAssetBundleBuildProgressWindow : EditorWindow
    {
        private const string WindowTitle = "Metaverse Bundle Build Progress";

        private static MetaverseAssetBundleBuildProgressWindow _instance;
        private static readonly ProgressState State = new();

        private sealed class ProgressState
        {
            public string BundleGuid;
            public string BundleDisplayName;
            public Platform CurrentPlatform;
            public int CurrentPlatformIndex;
            public int TotalPlatforms;
            public int CompletedPlatforms;
            public string StatusMessage = "Preparing build...";
            public string DetailMessage = string.Empty;
            public bool IsComplete;
            public bool HasError;
            public bool HasActiveBuild;
        }

        internal static void Begin(string bundleGuid, IReadOnlyList<Platform> orderedPlatforms)
        {
            if (orderedPlatforms == null || orderedPlatforms.Count == 0)
            {
                CloseWindow();
                return;
            }

            State.BundleGuid = bundleGuid;
            State.BundleDisplayName = BuildFriendlyName(bundleGuid);
            State.TotalPlatforms = orderedPlatforms.Count;
            State.CompletedPlatforms = 0;
            State.CurrentPlatformIndex = 0;
            State.StatusMessage = "Preparing build...";
            State.DetailMessage = string.Empty;
            State.IsComplete = false;
            State.HasError = false;
            State.HasActiveBuild = true;
            GetOrCreateWindow().Repaint();
        }

        internal static void ReportPlatformStarted(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms)
        {
            if (totalPlatforms <= 0)
                return;

            State.TotalPlatforms = totalPlatforms;
            State.CurrentPlatform = platform;
            State.CurrentPlatformIndex = platformIndex;
            State.CompletedPlatforms = Mathf.Clamp(completedPlatforms, 0, totalPlatforms);
            State.StatusMessage = $"Building {platform} ({platformIndex}/{totalPlatforms})";
            State.DetailMessage = string.Empty;
            State.IsComplete = false;
            State.HasError = false;
            State.HasActiveBuild = true;
            GetOrCreateWindow().Repaint();
        }

        internal static void ReportPlatformCompleted(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms)
        {
            if (totalPlatforms <= 0)
                return;

            State.TotalPlatforms = totalPlatforms;
            State.CurrentPlatform = platform;
            State.CurrentPlatformIndex = platformIndex;
            State.CompletedPlatforms = Mathf.Clamp(completedPlatforms, 0, totalPlatforms);
            State.StatusMessage = $"Finished {platform} ({platformIndex}/{totalPlatforms})";
            State.DetailMessage = string.Empty;
            State.IsComplete = false;
            State.HasError = false;
            State.HasActiveBuild = true;
            GetOrCreateWindow().Repaint();
        }

        internal static void ReportPlatformSkipped(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms, string reason)
        {
            if (totalPlatforms <= 0)
                return;

            State.TotalPlatforms = totalPlatforms;
            State.CurrentPlatform = platform;
            State.CurrentPlatformIndex = platformIndex;
            State.CompletedPlatforms = Mathf.Clamp(completedPlatforms, 0, totalPlatforms);
            State.StatusMessage = $"Skipping {platform} ({platformIndex}/{totalPlatforms})";
            State.DetailMessage = string.IsNullOrEmpty(reason) ? "Platform skipped." : reason;
            State.IsComplete = false;
            State.HasError = false;
            State.HasActiveBuild = true;
            GetOrCreateWindow().Repaint();
        }

        internal static void ReportPlatformFailed(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms, string reason, bool buildStopping)
        {
            if (totalPlatforms <= 0)
                return;

            State.TotalPlatforms = totalPlatforms;
            State.CurrentPlatform = platform;
            State.CurrentPlatformIndex = platformIndex;
            State.CompletedPlatforms = Mathf.Clamp(completedPlatforms, 0, totalPlatforms);
            State.StatusMessage = $"Failed {platform} ({platformIndex}/{totalPlatforms})";
            State.DetailMessage = string.IsNullOrEmpty(reason) ? "Check the console for details." : reason;
            State.IsComplete = buildStopping;
            State.HasError = true;
            State.HasActiveBuild = !buildStopping;
            GetOrCreateWindow().Repaint();
        }

        internal static void ReportBuildFinished(string message, int completedPlatforms, int totalPlatforms, bool failed)
        {
            if (totalPlatforms <= 0)
                return;

            State.TotalPlatforms = totalPlatforms;
            State.CompletedPlatforms = Mathf.Clamp(completedPlatforms, 0, totalPlatforms);
            State.StatusMessage = string.IsNullOrEmpty(message)
                ? failed ? "Asset bundle build failed." : "Asset bundle build complete."
                : message;
            State.DetailMessage = string.Empty;
            State.IsComplete = true;
            State.HasError = failed;
            State.HasActiveBuild = false;
            GetOrCreateWindow().Repaint();
        }

        internal static void CloseWindow()
        {
            if (_instance)
            {
                _instance.Close();
                _instance = null;
            }

            State.HasActiveBuild = false;
        }

        private static string BuildFriendlyName(string bundleGuid)
        {
            if (string.IsNullOrEmpty(bundleGuid))
                return "Unknown Bundle";

            try
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(bundleGuid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var fileName = Path.GetFileNameWithoutExtension(assetPath);
                    if (!string.IsNullOrEmpty(fileName))
                        return fileName;
                }
            }
            catch (Exception)
            {
                // ignored - fall back to guid
            }

            return bundleGuid;
        }

        private static MetaverseAssetBundleBuildProgressWindow GetOrCreateWindow()
        {
            if (!_instance)
            {
                _instance = GetWindow<MetaverseAssetBundleBuildProgressWindow>();
                _instance.titleContent = new GUIContent(WindowTitle);
                _instance.minSize = new Vector2(380f, 150f);
            }

            return _instance;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(380f, 150f);
            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnGUI()
        {
            GUILayout.Label("Asset Bundle Build", EditorStyles.boldLabel);

            if (!State.HasActiveBuild && !State.IsComplete)
            {
                EditorGUILayout.HelpBox("No active bundle builds.", MessageType.Info);
                if (GUILayout.Button("Close"))
                    CloseWindow();
                return;
            }

            if (!string.IsNullOrEmpty(State.BundleDisplayName))
                EditorGUILayout.LabelField("Asset", State.BundleDisplayName);

            if (!string.IsNullOrEmpty(State.BundleGuid))
                EditorGUILayout.LabelField("GUID", State.BundleGuid);

            if (State.TotalPlatforms > 0)
            {
                var currentLabel = State.CurrentPlatformIndex > 0
                    ? $"{State.CurrentPlatform} ({State.CurrentPlatformIndex}/{State.TotalPlatforms})"
                    : $"Waiting... (0/{State.TotalPlatforms})";
                EditorGUILayout.LabelField("Current Platform", currentLabel);

                var progressRect = GUILayoutUtility.GetRect(18, 18, "TextField");
                var normalized = State.TotalPlatforms == 0
                    ? 0f
                    : Mathf.Clamp01((float)State.CompletedPlatforms / State.TotalPlatforms);
                EditorGUI.ProgressBar(progressRect, normalized,
                    $"{State.CompletedPlatforms}/{State.TotalPlatforms} complete");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(State.StatusMessage ?? "Preparing build...", EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrEmpty(State.DetailMessage))
            {
                var messageType = State.HasError ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(State.DetailMessage, messageType);
            }

            using (new EditorGUI.DisabledScope(!State.IsComplete))
            {
                if (GUILayout.Button("Close") && State.IsComplete)
                    CloseWindow();
            }
        }
    }
}
