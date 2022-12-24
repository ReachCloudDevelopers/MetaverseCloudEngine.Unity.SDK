using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public class MetaverseSdkInstaller : AssetPostprocessor
    {
        private const string InitialUpdateCheckFlag = "MVCE_InitialUpdateCheck";
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
        private static void Init()
        {
            CheckPackages();
        }

        private static void CheckPackages()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            return;
#endif

            var sdkPackageGuid = AssetDatabase.FindAssets("MVCESDK_", new[] { PackagePath }).FirstOrDefault();
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
                    RefreshEditor();
            }
        }

        private static void Install(string package)
        {
            MetaverseTmpInstaller.InstallTmpEssentials();
            AssetDatabase.ImportPackage(package, false);
        }

        public static void RefreshEditor()
        {
            SessionState.SetBool(InitialUpdateCheckFlag, false); // This will trigger the installer again.
            CompilationPipeline.RequestScriptCompilation();
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
        }

        private static bool Uninstall()
        {
            if (AssetDatabase.IsValidFolder(SdkPath))
            {
                if (!EditorUtility.DisplayDialog(
                    DialogTitle,
                    "A Metaverse Cloud Engine SDK update is available. Would you like to update now?",
                    "Yes",
                    "No"))
                    return false;

                var dir = new DirectoryInfo(SdkPath);
                if (dir.Exists)
                {
                    dir.Delete(true);
                }
            }

            ScriptingDefines.Remove(new[] { ScriptingDefines.DefaultSymbols });
            return true;
        }

        private static void EditorFrameDelay(Action action, int frames = 1)
        {
            void OnFinish()
            {
                EditorApplication.delayCall -= OnFinish;

                if (frames == 0)
                {
                    action?.Invoke();
                    return;
                }

                EditorFrameDelay(action, --frames);
            }

            EditorApplication.delayCall += OnFinish;
        }
    }
}