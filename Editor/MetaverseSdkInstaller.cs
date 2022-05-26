using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public static class MetaverseSdkInstaller
    {
        private const string BasePath = "Assets/MetaverseCloudEngine";
        private const string SdkPath = BasePath + "/SDK";
        private const string VersionFilePath = BasePath + "/MVCE_Version.txt";

        [InitializeOnLoadMethod]
        private static void Init()
        {
            var sdkPackageGuid = AssetDatabase.FindAssets("MVCESDK_").FirstOrDefault();
            var asset = AssetDatabase.GUIDToAssetPath(sdkPackageGuid);
            var name = Path.GetFileNameWithoutExtension(asset);
            if (string.IsNullOrEmpty(name))
                return;
            
            var version = name.Split("_")[1];
            var packageVer = File.Exists(VersionFilePath) ? File.ReadAllText(VersionFilePath) : null;
            if (version != packageVer)
            {
                if (Uninstall())
                    AssetDatabase.ImportPackage(asset, false);

                var versionDir = Path.GetDirectoryName(VersionFilePath);
                if (!Directory.Exists(versionDir))
                    Directory.CreateDirectory(versionDir);
                
                File.WriteAllText(VersionFilePath, version);   
                AssetDatabase.Refresh();
            }
        }

        private static bool Uninstall()
        {
            if (!EditorUtility.DisplayDialog(
                "Update Metaverse SDK", "DATA LOSS WARNING: You are about to uninstall the " +
                $"Metaverse Cloud Engine SDK. This will delete everything underneath '{SdkPath}'. " +
                "All modifications to these files will be lost as a result. Have you made a backup?", 
                "Yes. I've made a backup, continue.", 
                "Cancel"))
                return false;
            
            if (Directory.Exists(SdkPath))
                Directory.Delete(SdkPath, true);
            
            AssetDatabase.Refresh();
            ScriptingDefines.Remove(new[] {ScriptingDefines.DefaultSymbols});
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }
    }
}