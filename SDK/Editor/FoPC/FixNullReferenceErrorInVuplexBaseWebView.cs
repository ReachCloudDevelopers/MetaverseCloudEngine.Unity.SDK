using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class FixNullReferenceErrorInVuplexBaseWebView : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            const string basePath = "Assets";
            if (!System.IO.Directory.Exists(basePath)) return;
            var files = System.IO.Directory.GetFiles(basePath, "BaseWebView.cs", System.IO.SearchOption.AllDirectories);
            if (files.Length == 0) return;
            var file = files.FirstOrDefault(x => x.Replace("\\", "/").Contains("Vuplex/WebView/Core/") && x.Replace("\\", "/").EndsWith("BaseWebView.cs"));
            if (file == null) return;
            var text = System.IO.File.ReadAllText(file);
            const string badCode = "        Dictionary<EventHandler, EventHandler<LoadFailedEventArgs>> _legacyPageLoadFailedHandlerMap;";
            const string fixedCode = "        Dictionary<EventHandler, EventHandler<LoadFailedEventArgs>> _legacyPageLoadFailedHandlerMap = new Dictionary<EventHandler, EventHandler<LoadFailedEventArgs>>();";
            if (!text.Contains(badCode) || text.Contains(fixedCode)) return;
            var newText = text.Replace(badCode, fixedCode);
            System.IO.File.WriteAllText(file, newText);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            Debug.Log("Fixed Vuplex BaseWebView.cs");
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}