using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public static class DisableOvrTelemtaryPopup
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var files = System.IO.Directory.GetFiles(
                "Library/PackageCache", 
                "OVRTelemetryPopup.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var path = files.FirstOrDefault(x => 
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.core") && 
                x.Replace("\\", "/").EndsWith("OVRTelemetryPopup.cs"));
            if (string.IsNullOrEmpty(path)) return;
            if (!System.IO.File.Exists(path)) return;
            var text = System.IO.File.ReadAllText(path);
            const string badCode = "var consent = EditorUtility.DisplayDialog(";
            if (!text.Contains(badCode)) return;
            text = text.Replace(badCode, "var consent = false;}}/*EditorUtility.DisplayDialog(");
            System.IO.File.WriteAllText(path, text);
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Disabled OVR Telemetry Popup");
        }
    }
}