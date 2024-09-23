using UnityEditor;
using System.IO;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    // Fixes the OVRProjectSetupRenderingTasks.cs file, and replaces line 573 with the corrected code.
    public static class MetaOVRProjectSetupRenderingTasksFix
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var path = "Library/PackageCache/com.meta.xr.sdk.core@68.0.2/Editor/OVRProjectSetup/Tasks/Implementations/OVRProjectSetupRenderingTasks.cs";
            if (!File.Exists(path)) return;
            var text = File.ReadAllText(path);
            var brokenLine = "?.Any(cameraData => cameraData.cameraStack?.Any() ?? false) ?? false;";
            if (text.Contains(brokenLine)) {
                var newText = text.Replace(
                    brokenLine,
                    "?.Any(cameraData => cameraData != null && cameraData.scriptableRenderer != null && (cameraData.cameraStack?.Any() ?? false)) ?? false;");
                File.WriteAllText(path, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            }
        }
    }
}