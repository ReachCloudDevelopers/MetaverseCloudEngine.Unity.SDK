using UnityEditor;
using System.IO;
using System.Linq;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    // Fixes the OVRProjectSetupRenderingTasks.cs file, and replaces line 573 with the corrected code.
    public static class FixMetaSDKCoreAccessingCameraStackWithoutUrpEnabled
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            //var path = "Library/PackageCache/com.meta.xr.sdk.core@68.0.2/Editor/OVRProjectSetup/Tasks/Implementations/OVRProjectSetupRenderingTasks.cs";
            var files = Directory.GetFiles("Library/PackageCache", "OVRProjectSetupRenderingTasks.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var path = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.core@") && x.Replace("\\", "/").EndsWith("/Editor/OVRProjectSetup/Tasks/Implementations/OVRProjectSetupRenderingTasks.cs"));
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