#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Guard DataProvider.GetSkeletonPoseData() with null-conditional in
    /// FromOVRBodyDataSource.cs and FromOVRHandDataSource.cs to avoid NRE when
    /// the provider is null (e.g., component disabled).
    /// </summary>
    internal static class FixFromOVRDataSourceNullDataProvider
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var bodyFiles = Directory.GetFiles(basePath, "FromOVRBodyDataSource.cs", SearchOption.AllDirectories);
            var handFiles = Directory.GetFiles(basePath, "FromOVRHandDataSource.cs", SearchOption.AllDirectories);

            var candidates = bodyFiles.Concat(handFiles)
                .Where(x =>
                    x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.interaction.ovr@") &&
                    (x.Replace("\\", "/").EndsWith("Runtime/Scripts/Input/FromOVRBodyDataSource.cs") ||
                     x.Replace("\\", "/").EndsWith("Runtime/Scripts/Input/FromOVRHandDataSource.cs")))
                .ToArray();

            if (candidates.Length == 0) return;

            const string target = "var data = DataProvider.GetSkeletonPoseData();";
            const string replacement = "var data = DataProvider?/*PATCHED*/.GetSkeletonPoseData();";

            foreach (var file in candidates)
            {
                if (!File.Exists(file)) continue;

                var text = File.ReadAllText(file);

                // Idempotency: if null-conditional already present (with or without our marker), skip.
                if (text.Contains("DataProvider?/*PATCHED*/.GetSkeletonPoseData()") ||
                    text.Contains("DataProvider?.GetSkeletonPoseData()"))
                {
                    continue;
                }

                if (!text.Contains(target)) continue;

                var newText = text.Replace(target, replacement);
                if (newText == text) continue;

                File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log($"[FoPC] Replaced null-unsafe DataProvider.GetSkeletonPoseData() with null-conditional in {Path.GetFileName(file)}.");
            }
        }
    }
}
#endif

