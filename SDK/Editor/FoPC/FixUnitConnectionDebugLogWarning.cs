#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Comments out Debug.LogWarning in UnitConnection.cs to prevent excessive warning spam
    /// when connections fail to load during Visual Scripting operations.
    /// </summary>
    internal static class FixUnitConnectionDebugLogWarning
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "UnitConnection.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            var file = files.FirstOrDefault(x =>
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.unity.visualscripting@") &&
                x.Replace("\\", "/").EndsWith("Runtime/VisualScripting.Flow/Connections/UnitConnection.cs"));
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            var text = File.ReadAllText(file);
            const string target = "Debug.LogWarning($\"Could not load connection between '{source.key}' of '{sourceUnit}' and '{destination.key}' of '{destinationUnit}'.\");";
            const string replacement = "//Debug./*PATCHED*/LogWarning($\"Could not load connection between '{source.key}' of '{sourceUnit}' and '{destination.key}' of '{destinationUnit}'.\");";

            // If already patched with our replacement, do nothing.
            if (text.Contains(replacement)) return;

            if (!text.Contains(target)) return;

            var newText = text.Replace(target, replacement);
            if (newText == text) return;

            File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Commented out Debug.LogWarning in UnitConnection.cs to prevent excessive warning spam.");
        }
    }
}
#endif

