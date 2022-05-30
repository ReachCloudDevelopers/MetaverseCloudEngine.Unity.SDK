//#if !METAVERSE_CLOUD_ENGINE_INTERNAL

using System;
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
        private const string PackagePath = "Packages/com.reachcloud.metaverse-cloud-sdk";

        private static DateTime _nextUpdateCheck;

        [InitializeOnLoadMethod]
        private static void Init() => EditorApplication.update += OnEditorUpdate;

        private static void OnEditorUpdate()
        {
            try
            {
                if (DateTime.UtcNow > _nextUpdateCheck)
                {
                    var sdkPackageGuid = AssetDatabase.FindAssets("MVCESDK_", new[] {PackagePath}).FirstOrDefault();
                    var asset = AssetDatabase.GUIDToAssetPath(sdkPackageGuid);
                    if (string.IsNullOrEmpty(asset))
                        return;

                    var name = Path.GetFileNameWithoutExtension(asset);
                    if (string.IsNullOrEmpty(name))
                        return;

                    var version = name.Split("_")[1];
                    var packageVer = GetVersion();
                    if (version != packageVer)
                    {
                        if (Uninstall())
                        {
                            AssetDatabase.ImportPackage(asset, false);
                            CompilationPipeline.RequestScriptCompilation();
                        }

                        SetVersion(version);
                    }
                }
            }
            finally
            {
                _nextUpdateCheck = DateTime.UtcNow.AddSeconds(15);
            }
        }

        private static string GetVersion()
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
                    "Update Metaverse SDK",
                    "DATA LOSS WARNING: You are about to uninstall the " +
                    $"Metaverse Cloud Engine SDK. This will delete everything underneath '{SdkPath}'. " +
                    "All modifications to these files will be lost as a result. Have you made a backup?",
                    "Yes. I've made a backup, continue.",
                    "Skip"))
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