using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    public class FixNullReferenceErrorInLinkerCreator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "LinkerCreator.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.unity.visualscripting") && x.Replace("\\", "/").EndsWith("LinkerCreator.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            if (text.Contains("foreach (var unit in subgraph.nest.graph.units)") && !text.Contains("if (subgraph?.nest?.graph?.units != null)"))
            {
                var newText = text.Replace("foreach (var unit in subgraph.nest.graph.units)", 
                    "if (subgraph?.nest?.graph?.units != null) foreach (var unit in subgraph.nest.graph.units)");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            PatchCode();
        }
    }
}