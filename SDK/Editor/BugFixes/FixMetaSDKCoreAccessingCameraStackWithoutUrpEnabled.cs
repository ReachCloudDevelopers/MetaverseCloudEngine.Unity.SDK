using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    // Fixes the OVRProjectSetupRenderingTasks.cs file, and replaces line 573 with the corrected code.
    internal static class FixMetaSDKCoreAccessingCameraStackWithoutUrpEnabled
    {
        [InitializeOnLoadMethod]
        public static void PatchCode()
        {
            var files = Directory.GetFiles("Library/PackageCache", "OVRProjectSetupRenderingTasks.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var path = files.FirstOrDefault(x => 
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.core") && 
                x.Replace("\\", "/").EndsWith("OVRProjectSetupRenderingTasks.cs"));
            if (!File.Exists(path)) {
                return;
            }
            var text = File.ReadAllText(path);
            var brokenLine = "?.Any(cameraData => cameraData.cameraStack?.Any() ?? false) ?? false;";
            if (text.Contains(brokenLine)) {
                var newText = text.Replace(
                    brokenLine,
                    "?.Any(cameraData => cameraData != null && cameraData.scriptableRenderer != null && (cameraData.cameraStack?.Any() ?? false)) ?? false;");
                File.WriteAllText(path, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed OVRProjectSetupRenderingTasks.cs");
            }
        }
    }
}