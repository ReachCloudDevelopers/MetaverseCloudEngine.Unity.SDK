#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Wraps calls to OnPreRenderCanvas() in TextMeshProUGUI.cs with try/catch
    /// to prevent NullReferenceException spam in the console.
    /// </summary>
    internal static class FixTextMeshProUGUIOnPreRenderCanvasTryCatch
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var packageRoots = Directory.GetDirectories(basePath, "com.unity.textmeshpro@*", SearchOption.TopDirectoryOnly);
            if (packageRoots.Length == 0) return;

            const string target = "OnPreRenderCanvas();";
            const string replacement = "try { OnPreRenderCanvas/*PATCHED*/(); } catch (System.NullReferenceException e) { /* ignored */ }";

            var anyPatched = false;

            foreach (var packageRoot in packageRoots)
            {
                var runtimeDir = Path.Combine(packageRoot, "Scripts", "Runtime");
                if (!Directory.Exists(runtimeDir)) continue;

                var candidateFiles = new[]
                {
                    Path.Combine(runtimeDir, "TextMeshProUGUI.cs"),
                    Path.Combine(runtimeDir, "TMPro_UGUI_Private.cs"),
                };

                foreach (var file in candidateFiles.Where(File.Exists))
                {
                    var text = File.ReadAllText(file);

                    // If already patched with our replacement, do nothing.
                    if (text.Contains(replacement)) continue;

                    if (!text.Contains(target)) continue;

                    var newText = text.Replace(target, replacement);
                    if (newText == text) continue;

                    File.WriteAllText(file, newText);
                    anyPatched = true;
                }
            }

            if (!anyPatched) return;

            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Wrapped OnPreRenderCanvas() calls in TextMeshPro UGUI runtime files with try/catch to prevent NRE spam.");
        }
    }
}
#endif
