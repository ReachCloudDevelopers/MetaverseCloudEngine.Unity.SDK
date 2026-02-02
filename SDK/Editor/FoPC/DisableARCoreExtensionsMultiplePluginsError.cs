#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    /// <summary>
    /// FoPC: Suppresses the noisy ARCore Extensions error emitted when multiple
    /// External Dependency Manager resolver plugins are detected.
    /// </summary>
    internal static class DisableARCoreExtensionsMultiplePluginsError
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!Directory.Exists(basePath))
                return;

            var files = Directory.GetFiles(basePath, "ExternalDependencyResolverHelper.cs", SearchOption.AllDirectories);
            if (files.Length == 0)
                return;

            var file = files.FirstOrDefault(x =>
                x.Replace("\\", "/").StartsWith("Library/PackageCache/com.google.ar.core.arfoundation.extensions@") &&
                x.Replace("\\", "/").EndsWith("/Editor/Scripts/Internal/ExternalDependencyResolverHelper.cs"));

            if (string.IsNullOrEmpty(file) || !File.Exists(file))
                return;

            var text = File.ReadAllText(file);

            const string idempotencyMarker = "/*FoPC*/LogErrorFormat(\"ARCoreExtensions: \"";
            if (text.Contains(idempotencyMarker))
                return;

            const string target =
                "                        Debug.LogErrorFormat(\"ARCoreExtensions: \" +\n" +
                "                            \"There are multiple {0} plugins detected. \" +\n" +
                "                            \"One is {1}, another is {2}. Please remove one of them.\",\n" +
                "                            resolverName, resolverPath, path);";

            const string replacement =
                "                        //Debug./*FoPC*/LogErrorFormat(\"ARCoreExtensions: \" +\n" +
                "                        //    \"There are multiple {0} plugins detected. \" +\n" +
                "                        //    \"One is {1}, another is {2}. Please remove one of them.\",\n" +
                "                        //    resolverName, resolverPath, path);";

            if (!text.Contains(target))
                return;

            var newText = text.Replace(target, replacement);
            if (newText == text)
                return;

            File.WriteAllText(file, newText);
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log("[FoPC] Disabled ARCoreExtensions multiple plugins detected error log.");
        }
    }
}
#endif
