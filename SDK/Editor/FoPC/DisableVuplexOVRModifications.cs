using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class DisableVuplexOVRModifications : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Assets";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "AndroidBuildScript.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").Contains("Vuplex/WebView/Android/Editor/") && x.Replace("\\", "/").EndsWith("AndroidBuildScript.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            var modified = ModifyEnableClearTextTrafficIfNeeded(ref text);
            modified |= ModifyScriptDefines(ref text);
            if (!modified) return;
            System.IO.File.WriteAllText(file, text);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Fixed Vuplex AndroidBuildScript.cs");
        }

        private static bool ModifyScriptDefines(ref string text)
        {
            const string badCode = "#if VUPLEX_OCULUS_PROJECT_CONFIG";
            const string fixedCode = "#if false";
            if (!text.Contains(badCode)) return false;
            text = text.Replace(badCode, fixedCode);
            return true;
        }

        private static bool ModifyEnableClearTextTrafficIfNeeded(ref string text)
        {
            const string badCode = "#if VUPLEX_ANDROID_DISABLE_CLEARTEXT_TRAFFIC";
            const string fixedCode = "#if true";
            if (!text.Contains(badCode)) return false;
            text = text.Replace(badCode, fixedCode);
            return true;
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode(); 
    }
}