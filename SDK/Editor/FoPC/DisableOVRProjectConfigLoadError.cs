using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Suppresses the noisy "OVRProjectConfig exists but could not be loaded" error
    /// emitted by OVRProjectConfig.GetOrCreateProjectConfig so it does not spam the console.
    /// </summary>
    public static class DisableOVRProjectConfigLoadError
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var files = System.IO.Directory.GetFiles(
                "Library/PackageCache",
                "OVRProjectConfig.cs",
                System.IO.SearchOption.AllDirectories);
            if (files.Length == 0)
                return;

            var path = files.FirstOrDefault(x =>
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.meta.xr.sdk.core") &&
                x.Replace("\\", "/").EndsWith("OVRProjectConfig.cs"));

            if (string.IsNullOrEmpty(path))
                return;
            if (!System.IO.File.Exists(path))
                return;

            var text = System.IO.File.ReadAllText(path);

            const string badCode = "Debug.LogError(\"OVRProjectConfig exists but could not be loaded. Config values may not be available until restart.\");";
            const string fixedCode = "//Debug./*FoPC*/LogError(\"OVRProjectConfig exists but could not be loaded. Config values may not be available until restart.\");";

            // Idempotency: if we've already applied the patch, do nothing.
            if (text.Contains(fixedCode))
                return;

            if (!text.Contains(badCode))
                return;

            var newText = text.Replace(badCode, fixedCode);
            if (newText == text)
                return;

            System.IO.File.WriteAllText(path, newText);
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Disabled OVRProjectConfig load error log.");
        }
    }
}

