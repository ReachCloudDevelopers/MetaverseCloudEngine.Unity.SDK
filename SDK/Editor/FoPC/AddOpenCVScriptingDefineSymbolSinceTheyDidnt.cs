using System.IO;
using System.Linq;
using MetaverseCloudEngine.Unity.Installer;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MetaverseCloudEngine.Unity.FixingOtherPeoplesCode
{
    public class AddOpenCVScriptingDefineSymbolSinceTheyDidnt : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        private static void PatchCode()
        {
            var openCvCoreFile = Directory
                .GetFiles("./", "Core.cs", SearchOption.AllDirectories)
                .FirstOrDefault(x => x.Replace("\\", "/").Contains("org/opencv/core/Core.cs"));
            if (openCvCoreFile == null)
            {
                if (ScriptingDefines.IsDefined("MV_OPENCV"))
                    ScriptingDefines.Remove(new [] { "MV_OPENCV" });
                return;
            }
            
            if (!ScriptingDefines.IsDefined("MV_OPENCV"))
                ScriptingDefines.Add(new [] { "MV_OPENCV" });
        }

        public void OnPreprocessBuild(BuildReport report) => PatchCode();
    }
}