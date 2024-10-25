using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Editors.BugFixes
{
    public class FixGeospatialPlacesAPINullReferenceError : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string path = "./Library/PackageCache";
            if (!System.IO.Directory.Exists(path)) return;
            var files = System.IO.Directory.GetFiles(path, "PlacesApi.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").StartsWith("./Library/PackageCache/com.google.ar.core.arfoundation.extensions") && x.Replace("\\", "/").EndsWith("LinkerCreator.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            if (text.Contains("public Place[] places = null;") && !text.Contains("public Place[] places = new Place[0];"))
            {
                var newText = text.Replace("public Place[] places = null;", "public Place[] places = new Place[0];");
                System.IO.File.WriteAllText(file, newText);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed PlacesApi.cs null reference error.");
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            PatchCode();
        }
    }
}