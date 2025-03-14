using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class FixCesiumRuntimeAssemblyDefinitionForWebGL
    {
        [InitializeOnLoadMethod]
        private static void PatchASMDef()
        {
            const string path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "CesiumRuntime.asmdef", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.cesium.unity") && x.Replace("\\", "/").EndsWith("CesiumRuntime.asmdef"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            if (text.Contains("\"excludePlatforms\": []") && !text.Contains("\"includePlatforms\": [\r\n"))
            {
                var newText = text.Replace("\"excludePlatforms\": []", "\"excludePlatforms\": [\"WebGL\",\"LinuxStandalone64\"]");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed CesiumRuntime.asmdef");
            }
        }
    }
}