using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class DisableVuplexArgumentExceptionOnWebGLInitialization : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Assets";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "CanvasWebViewPrefab.WebGL.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").Contains("Vuplex/WebView/WebGL/") && x.Replace("\\", "/").EndsWith("CanvasWebViewPrefab.WebGL.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            const string badCode = "throw new InvalidOperationException(\"2D WebView for WebGL only supports Native 2D Mode, which requires that the Canvas's render mode be set to \\\"Screen Space - Overlay\\\" or \\\"Screen Space - Camera\\\", but its render mode is instead currently set to \\\"World Space\\\". Please change the Canvas's render mode to \\\"Screen Space - Overlay\\\" or \\\"Screen Space - Camera\\\".\");";
            const string fixedCode = "UnityEngine.Debug.LogError(\"2D WebView for WebGL only supports Native 2D Mode, which requires that the Canvas's render mode be set to \\\"Screen Space - Overlay\\\" or \\\"Screen Space - Camera\\\", but its render mode is instead currently set to \\\"World Space\\\". Please change the Canvas's render mode to \\\"Screen Space - Overlay\\\" or \\\"Screen Space - Camera\\\".\");";
            if (!text.Contains(badCode) || text.Contains(fixedCode)) return;
            var newText = text.Replace(badCode, fixedCode);
            System.IO.File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Fixed Vuplex CanvasWebViewPrefab.WebGL.cs");
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}