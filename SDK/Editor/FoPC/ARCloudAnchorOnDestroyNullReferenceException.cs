using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class ARCloudAnchorOnDestroyNullReferenceException : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Library/PackageCache";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "ARCloudAnchor.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").Contains("/Runtime/Scripts/") && x.Replace("\\", "/").EndsWith("ARCloudAnchor.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            const string badCode = "if (ARCoreExtensions._instance.currentARCoreSessionHandle";
            const string fixedCode = "if (ARCoreExtensions._instance != null && ARCoreExtensions._instance.currentARCoreSessionHandle";
            if (!text.Contains(badCode)) return;
            var newText = text.Replace(badCode, fixedCode);
            System.IO.File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Fixed ARCloudAnchor.cs");
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}