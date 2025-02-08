using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class FixArOcclusionManagerNotCheckingForNullReference
    {
        [InitializeOnLoadMethod]
        private static void Patch()
        {
            const string path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "AROcclusionManager.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.unity.xr.arfoundation") && x.Replace("\\", "/").EndsWith("AROcclusionManager.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            if (text.Contains("m_SwapchainStrategy.Dispose();"))
            {
                var newText = text.Replace("m_SwapchainStrategy.Dispose();", "m_SwapchainStrategy?.Dispose();");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed AROcclusionManager.cs");
            }
        }
    }
}