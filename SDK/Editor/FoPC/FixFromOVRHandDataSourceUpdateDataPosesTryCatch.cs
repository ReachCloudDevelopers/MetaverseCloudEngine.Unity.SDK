#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Wraps UpdateDataPoses(poseData) in FromOVRHandDataSource.cs with try/catch
    /// to prevent exceptions in Meta's SDK from crashing the app.
    /// </summary>
    internal static class FixFromOVRHandDataSourceUpdateDataPosesTryCatch
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "FromOVRHandDataSource.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            var file = files.FirstOrDefault(x =>
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.interaction.ovr@") &&
                x.Replace("\\", "/").EndsWith("Runtime/Scripts/Input/FromOVRHandDataSource.cs"));
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            var text = File.ReadAllText(file);
            const string target = "UpdateDataPoses(poseData);";
            const string replacement = "try { UpdateDataPoses(/*PATCHED*/poseData); } catch { }";

            // If already patched with our replacement, do nothing.
            if (text.Contains(replacement)) return;

            if (!text.Contains(target)) return;

            var newText = text.Replace(target, replacement);
            if (newText == text) return;

            File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Wrapped UpdateDataPoses(poseData) in try-catch in FromOVRHandDataSource.cs to prevent exceptions from crashing the app.");
        }
    }
}
#endif

