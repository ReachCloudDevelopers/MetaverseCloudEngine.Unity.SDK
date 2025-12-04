#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Patches XRBaseInteractable.cs to handle null interactorObject in OnSelectExiting.
    /// Replaces: var removed = m_InteractorsSelecting.Remove(args.interactorObject);
    /// With:     var removed = m_InteractorsSelecting/*PATCHED*/.Remove(args.interactorObject) || args.interactorObject == null;
    /// </summary>
    internal static class FixXRBaseInteractableSelectExitRemove
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "XRBaseInteractable.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;

            var file = files.FirstOrDefault(x =>
                x.Replace("\\", "/").Contains("com.unity.xr.interaction.toolkit") &&
                x.Replace("\\", "/").EndsWith("Runtime/Interaction/Interactables/XRBaseInteractable.cs"));
            
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;

            var text = File.ReadAllText(file);
            const string target = "var removed = m_InteractorsSelecting.Remove(args.interactorObject);";
            const string replacement = "var removed = m_InteractorsSelecting/*PATCHED*/.Remove(args.interactorObject) || args.interactorObject == null;";

            // If already patched with our replacement, do nothing.
            if (text.Contains(replacement)) return;

            if (!text.Contains(target)) return;

            var newText = text.Replace(target, replacement);
            if (newText == text) return;

            File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Patched XRBaseInteractable.cs to handle null interactorObject in OnSelectExiting.");
        }
    }
}
#endif
