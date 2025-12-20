using UnityEditor;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    // Fixes SpeechToTextAgent.cs compilation errors on WebGL.
    internal static class FixSpeechToTextAgentWebGL
    {
        [InitializeOnLoadMethod]
        public static void PatchCode()
        {
            var files = Directory.GetFiles("Library/PackageCache", "SpeechToTextAgent.cs", SearchOption.AllDirectories);
            if (files.Length == 0) return;
            
            var path = files.FirstOrDefault(x => 
                x.Replace("\\", "/").Contains("com.meta.xr.sdk.core") && 
                x.Replace("\\", "/").EndsWith("SpeechToTextAgent.cs"));
            
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            var text = File.ReadAllText(path);
            var originalText = text;

            // Fix Microphone calls for WebGL
            // Line 148-149
            var brokenStart = "_mic = Microphone.Start(deviceName, true, 10, sampleRate);\n            while (Microphone.GetPosition(deviceName) <= 0) yield return null;";
            var fixedStart = "#if !UNITY_WEBGL\n            _mic = Microphone.Start(deviceName, true, 10, sampleRate)/*PATCHED*/;\n            while (Microphone.GetPosition(deviceName) <= 0) yield return null;\n#else\n            _mic = null;\n            yield break;\n#endif";

            if (text.Contains(brokenStart)) {
                text = text.Replace(brokenStart, fixedStart);
            } else if (text.Contains(brokenStart.Replace("\n", "\r\n"))) {
                text = text.Replace(brokenStart.Replace("\n", "\r\n"), fixedStart.Replace("\n", "\r\n"));
            }

            // Line 160
            var brokenPos = "var pos = Microphone.GetPosition(deviceName);";
            var fixedPos = "#if !UNITY_WEBGL\n                var pos = Microphone.GetPosition(deviceName)/*PATCHED*/;\n#else\n                var pos = 0;\n#endif";

            if (text.Contains(brokenPos)) {
                text = text.Replace(brokenPos, fixedPos);
            } else if (text.Contains(brokenPos.Replace("\n", "\r\n"))) {
                text = text.Replace(brokenPos.Replace("\n", "\r\n"), fixedPos.Replace("\n", "\r\n"));
            }

            // Line 273
            var brokenEnd = "Microphone.End(deviceName);";
            var fixedEnd = "#if !UNITY_WEBGL\n            Microphone.End(deviceName)/*PATCHED*/;\n#endif";

            if (text.Contains(brokenEnd)) {
                text = text.Replace(brokenEnd, fixedEnd);
            } else if (text.Contains(brokenEnd.Replace("\n", "\r\n"))) {
                text = text.Replace(brokenEnd.Replace("\n", "\r\n"), fixedEnd.Replace("\n", "\r\n"));
            }

            if (text != originalText) {
                File.WriteAllText(path, text);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                Debug.Log("Fixed SpeechToTextAgent.cs compilation errors for WebGL");
            }
        }
    }
}
