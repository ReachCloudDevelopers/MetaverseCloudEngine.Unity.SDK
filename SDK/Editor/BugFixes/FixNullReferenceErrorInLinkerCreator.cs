using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    public class FixNullReferenceErrorInLinkerCreator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "LinkerCreator.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith($"{basePath}/com.unity.visualscripting") && x.Replace("\\", "/").EndsWith("LinkerCreator.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            const string badCode = "foreach (var unit in subgraph.nest.graph.units)";
            const string fixedCode = "if (subgraph?.nest?.graph?.units != null) foreach (var unit in subgraph.nest.graph.units)";
            if (!text.Contains(badCode) || text.Contains(fixedCode)) return;
            var newText = text.Replace(badCode, fixedCode);
            System.IO.File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Fixed LinkerCreator.cs");
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}