//#if !METAVERSE_CLOUD_ENGINE_INTERNAL

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public class MetaverseSdkInstaller : AssetPostprocessor
    {
        private const string BasePath = "Assets/MetaverseCloudEngine";
        private const string SdkPath = BasePath + "/SDK";
        private const string VersionFilePath = BasePath + "/MVCE_Version.txt";
        private const string PackagePath = "Packages/com.reachcloud.metaverse-cloud-sdk";
        private const string DialogTitle = "Update Metaverse SDK";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var inPackages = 
                importedAssets.Any(path => path.StartsWith("Packages/")) ||
                deletedAssets.Any(path => path.StartsWith("Packages/")) ||
                movedAssets.Any(path => path.StartsWith("Packages/")) ||
                movedFromAssetPaths.Any(path => path.StartsWith("Packages/"));
 
            if (inPackages)
            {
                CheckPackages();
            }
        }
        
        [InitializeOnLoadMethod]
        private static void Init() => CheckPackages();

        private static void CheckPackages()
        {
            var sdkPackageGuid = AssetDatabase.FindAssets("MVCESDK_", new[] {PackagePath}).FirstOrDefault();
            var asset = AssetDatabase.GUIDToAssetPath(sdkPackageGuid);
            if (string.IsNullOrEmpty(asset))
                return;

            var name = Path.GetFileNameWithoutExtension(asset);
            if (string.IsNullOrEmpty(name))
                return;

            var version = name.Split("_")[1];
            var packageVer = ReadVersion();
            if (version != packageVer)
            {
                var installed = false;
                if (Uninstall())
                {
                    installed = true;
                    Install(asset);
                }

                SetVersion(version);

                if (installed)
                    TryRestart();
            }
        }

        private static void Install(string package)
        {
            AssetDatabase.ImportPackage(package, false);
            CompilationPipeline.RequestScriptCompilation();
        }

        private static void TryRestart()
        {
            if (EditorUtility.DisplayDialog(DialogTitle, "The Metaverse Cloud SDK needs to restart Unity.", "Restart Now", "Restart Later"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }

        private static string ReadVersion()
        {
            var packageVer = File.Exists(VersionFilePath) ? File.ReadAllText(VersionFilePath) : null;
            return packageVer;
        }

        private static void SetVersion(string version)
        {
            var versionDir = Path.GetDirectoryName(VersionFilePath);
            if (!Directory.Exists(versionDir))
                Directory.CreateDirectory(versionDir);

            File.WriteAllText(VersionFilePath, version);
            AssetDatabase.Refresh();
        }

        private static bool Uninstall()
        {
            if (AssetDatabase.IsValidFolder(SdkPath))
            {
                if (!EditorUtility.DisplayDialog(
                    DialogTitle,
                    "DATA LOSS WARNING: You are about to uninstall the " +
                    $"Metaverse Cloud Engine SDK. This will delete everything underneath '{SdkPath}'. " +
                    "All modifications to these files will be lost as a result. Have you made a backup?",
                    "Yes. I've made a backup, continue.",
                    "Don't Update"))
                    return false;

                AssetDatabase.DeleteAsset(SdkPath);
                AssetDatabase.Refresh();
            }

            ScriptingDefines.Remove(new[] {ScriptingDefines.DefaultSymbols});
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }
    }
}

//#endif