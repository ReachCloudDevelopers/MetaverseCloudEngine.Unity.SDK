using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    public class FixCesiumRuntimeAssemblyDefinitionForWebGL
    {
        [InitializeOnLoadMethod]
        private static void PatchASMDef()
        {
            var path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "CesiumRuntime.asmdef", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.cesium.unity") && x.Replace("\\", "/").EndsWith("CesiumRuntime.asmdef"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            if (text.Contains("\"excludePlatforms\": []"))
            {
                var newText = text.Replace("\"excludePlatforms\": []", "\"excludePlatforms\": [\"WebGL\"]");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed CesiumRuntime.asmdef");
            }
        }
    }
}