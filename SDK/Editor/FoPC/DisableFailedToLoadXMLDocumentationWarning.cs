using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class DisableFailedToLoadXMLDocumentationWarning
    {
        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "XmlDocumentation.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.unity.visualscripting") && x.Replace("\\", "/").EndsWith("XmlDocumentation.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            if (text.Contains("Debug.LogWarning(\"Failed to load XML documentation:\\n\" + ex);"))
            {
                var newText = text.Replace("Debug.LogWarning(\"Failed to load XML documentation:\\n\" + ex);", "// ignored");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Disabled Failed To Load XML Documentation Warning");
            }
        }
    }
}