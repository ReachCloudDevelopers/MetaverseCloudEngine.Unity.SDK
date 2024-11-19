using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class FixOVRPluginPoseVariableNameInvalid : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "OVRPlugin.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").Contains("/com.meta.xr.sdk.core") && x.Replace("\\", "/").EndsWith("OVRPlugin.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            const string badCode = "pose = default(Posef);";
            const string fixedCode = "result = default(Posef);";
            if (!text.Contains(badCode)) return;
            var newText = text.Replace(badCode, fixedCode);
            System.IO.File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Fixed OVRPlugin.cs");
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}
