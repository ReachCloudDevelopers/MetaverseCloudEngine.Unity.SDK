using System;
using System.Collections.Generic;
using System.IO;
using MetaverseCloudEngine.Common.Enumerations;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors
{
    /// <summary>
    /// Helper that mirrors bundle build activity into Unity's native background Progress system.
    /// </summary>
    internal static class MetaverseAssetBundleBuildProgress
    {
        private static int _progressId = -1;
        private static int _totalPlatforms;
        private static string _bundleGuid;
        private static string _bundleDisplayName;

        internal static void Begin(string bundleGuid, IReadOnlyList<Platform> orderedPlatforms)
        {
            Finish();

            if (orderedPlatforms == null || orderedPlatforms.Count == 0)
                return;

            _bundleGuid = bundleGuid;
            _bundleDisplayName = BuildFriendlyName(bundleGuid);
            _totalPlatforms = orderedPlatforms.Count;

            var title = string.IsNullOrEmpty(_bundleDisplayName)
                ? "Building Asset Bundle"
                : $"Building {_bundleDisplayName}";

            var description = _totalPlatforms == 1
                ? "Preparing 1 platform..."
                : $"Preparing {_totalPlatforms} platforms...";

            _progressId = Progress.Start(title, description, Progress.Options.Sticky);
            Progress.Report(_progressId, 0, _totalPlatforms, description);
        }

        internal static void ReportPlatformStarted(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms)
        {
            if (!IsActive(totalPlatforms))
                return;

            var description = $"Building {platform} ({platformIndex}/{totalPlatforms})";
            Progress.Report(_progressId, Mathf.Max(completedPlatforms, 0), totalPlatforms, description);
        }

        internal static void ReportPlatformCompleted(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms)
        {
            if (!IsActive(totalPlatforms))
                return;

            var description = $"Finished {platform} ({platformIndex}/{totalPlatforms})";
            Progress.Report(_progressId, Mathf.Clamp(completedPlatforms, 0, totalPlatforms), totalPlatforms, description);
        }

        internal static void ReportPlatformSkipped(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms, string reason)
        {
            if (!IsActive(totalPlatforms))
                return;

            var detail = string.IsNullOrEmpty(reason) ? "Platform skipped." : reason;
            var description = $"Skipping {platform}: {detail}";
            Progress.Report(_progressId, Mathf.Clamp(completedPlatforms, 0, totalPlatforms), totalPlatforms, description);
        }

        internal static void ReportPlatformFailed(Platform platform, int platformIndex, int totalPlatforms, int completedPlatforms, string reason, bool buildStopping)
        {
            if (!IsActive(totalPlatforms))
                return;

            var detail = string.IsNullOrEmpty(reason) ? "Check the console for details." : reason;
            var description = $"Failed {platform} ({platformIndex}/{totalPlatforms}): {detail}";
            Progress.Report(_progressId, Mathf.Clamp(completedPlatforms, 0, totalPlatforms), totalPlatforms, description);

            if (buildStopping)
                Finish(Progress.Status.Failed, description);
        }

        internal static void ReportBuildFinished(string message, int completedPlatforms, int totalPlatforms, bool failed)
        {
            if (!IsActive(totalPlatforms))
                return;

            var status = failed ? Progress.Status.Failed : Progress.Status.Succeeded;
            var clampedCompleted = Mathf.Clamp(completedPlatforms, 0, totalPlatforms);
            var description = string.IsNullOrEmpty(message)
                ? failed ? "Asset bundle build failed." : "Asset bundle build complete."
                : message;
            Progress.Report(_progressId, clampedCompleted, totalPlatforms, description);
            Finish(status, description);
        }

        private static bool IsActive(int totalPlatforms)
        {
            return _progressId >= 0 && totalPlatforms > 0;
        }

        private static void Finish(Progress.Status status = Progress.Status.Succeeded, string description = null)
        {
            if (_progressId < 0)
                return;

            if (!string.IsNullOrEmpty(description))
                Progress.SetDescription(_progressId, description);

            Progress.Finish(_progressId, status);
            _progressId = -1;
            _bundleGuid = null;
            _bundleDisplayName = null;
            _totalPlatforms = 0;
        }

        private static string BuildFriendlyName(string bundleGuid)
        {
            if (string.IsNullOrEmpty(bundleGuid))
                return null;

            try
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(bundleGuid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var filename = Path.GetFileNameWithoutExtension(assetPath);
                    if (!string.IsNullOrEmpty(filename))
                        return filename;
                }
            }
            catch (Exception)
            {
                // ignore lookup failures, fallback to GUID
            }

            return bundleGuid;
        }
    }
}
