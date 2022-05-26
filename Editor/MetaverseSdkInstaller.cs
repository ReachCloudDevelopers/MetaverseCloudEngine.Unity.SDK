using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public static class MetaverseSdkInstaller
    {
        private const string UninstallPath = "Assets/MetaverseCloudEngine/SDK";
        private const string MetaverseCloudEngineVersionKey = "MVCE_Version";

        [InitializeOnLoadMethod]
        private static void Init()
        {
            var sdkPackageGuid = AssetDatabase.FindAssets("MVCESDK_").FirstOrDefault();
            var asset = AssetDatabase.GUIDToAssetPath(sdkPackageGuid);
            var name = Path.GetFileNameWithoutExtension(asset);
            if (string.IsNullOrEmpty(name))
                return;
            
            var version = name.Split("_")[1];
            var packageVer = EditorPrefs.GetString(MetaverseCloudEngineVersionKey, null);
            if (version != packageVer && Uninstall())
            {
                AssetDatabase.ImportPackage(asset, false);
                EditorPrefs.SetString(MetaverseCloudEngineVersionKey, version);   
            }
        }

        private static bool Uninstall()
        {
            if (!EditorUtility.DisplayDialog(
                "Update Metaverse SDK", "DATA LOSS WARNING: You are about to uninstall the " +
                $"Metaverse Cloud Engine SDK. This will delete everything underneath '{UninstallPath}'. " +
                "All modifications to these files will be lost as a result. Have you made a backup?", 
                "Yes. I've made a backup, continue.", 
                "Cancel"))
                return false;
            
            Directory.Delete(UninstallPath, true);
            AssetDatabase.Refresh();
            ScriptingDefines.Remove(new[] {ScriptingDefines.DefaultSymbols});
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }
    }
}