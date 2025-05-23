using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class RemoveRequiredAttributeFromLocationModule : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "LocationModule.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").Contains("/com.google.ar.core.arfoundation.extensions") && x.Replace("\\", "/").EndsWith("LocationModule.cs"));
            if (file == null) return;
            var changed = false;
            var text = System.IO.File.ReadAllText(file);
            // usesFeature.Attributes.Required.Set(true);
            {
                const string badCode = "Attributes.Required.Set(true)";
                const string fixedCode = "Attributes.Required.Set(false)";
                if (text.Contains(badCode))
                {
                    var newText = text.Replace(badCode, fixedCode);
                    System.IO.File.WriteAllText(file, newText);
                    changed = true;
                }
            }
            // android:name=""android.hardware.location.gps"" android:required=""true""/>
            {
                const string badCode = "android:required=\"\"true\"\"/>";
                const string fixedCode = "android:required=\"false\"/>";
                if (text.Contains(badCode))
                {
                    var newText = text.Replace(badCode, fixedCode);
                    System.IO.File.WriteAllText(file, newText);
                    changed = true;
                }
            }
            // manifest.AddUsesFeature("android.hardware.location.gps", true);
            {
                const string badCode = ", true);";
                const string fixedCode = ", false);";
                if (text.Contains(badCode))
                {
                    var newText = text.Replace(badCode, fixedCode);
                    System.IO.File.WriteAllText(file, newText);
                    changed = true;
                }
            }
            if (changed)
            {
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed LocationModule.cs");
            }
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}