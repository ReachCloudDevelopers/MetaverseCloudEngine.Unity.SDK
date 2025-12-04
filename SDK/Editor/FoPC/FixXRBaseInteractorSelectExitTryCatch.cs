#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Wraps interactionManager.SelectExit(this, m_ManualInteractionInteractable) in XRBaseInteractor.cs with try/catch
    /// to prevent exceptions.
    /// </summary>
    internal static class FixXRBaseInteractorSelectExitTryCatch
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "XRBaseInteractor.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            var file = files.FirstOrDefault(x =>
                x.Replace("\\", "/").Contains("com.unity.xr.interaction.toolkit") &&
                x.Replace("\\", "/").EndsWith("Runtime/Interaction/Interactors/XRBaseInteractor.cs"));
            
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            var text = File.ReadAllText(file);
            const string target = "interactionManager.SelectExit(this, m_ManualInteractionInteractable);";
            const string replacement = "try { /*PATCHED*/ interactionManager.SelectExit(this, m_ManualInteractionInteractable); } catch { /* ignored */ }";

            // If already patched with our replacement, do nothing.
            if (text.Contains(replacement)) return;

            if (!text.Contains(target)) return;

            var newText = text.Replace(target, replacement);
            if (newText == text) return;

            File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Wrapped interactionManager.SelectExit(this, m_ManualInteractionInteractable) in try-catch in XRBaseInteractor.cs.");
        }
    }
}
#endif
