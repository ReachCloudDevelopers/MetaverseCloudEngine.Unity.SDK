#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Wraps PointableCanvasModule.RegisterPointableCanvas(this) in PointableCanvas.cs with try/catch
    /// to prevent NullReferenceExceptions from crashing on enable.
    /// </summary>
    internal static class FixPointableCanvasRegisterTryCatch
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "PointableCanvas.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            var file = files.FirstOrDefault(x =>
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.interaction@") &&
                x.Replace("\\", "/").EndsWith("Runtime/Scripts/Unity/PointableCanvas.cs"));
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            var text = File.ReadAllText(file);
            const string target = "PointableCanvasModule.RegisterPointableCanvas(this);";
            const string replacement = "try { PointableCanvasModule.RegisterPointableCanvas(/*PATCHED*/this); } catch { }";

            // If already patched with our replacement, do nothing.
            if (text.Contains(replacement)) return;

            if (!text.Contains(target)) return;

            var newText = text.Replace(target, replacement);
            if (newText == text) return;

            File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Wrapped PointableCanvasModule.RegisterPointableCanvas(this) in try-catch in PointableCanvas.cs to prevent NREs on enable.");
        }
    }
}
#endif

