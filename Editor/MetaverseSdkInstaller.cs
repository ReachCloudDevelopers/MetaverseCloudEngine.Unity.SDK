using System;
using System.IO;
using System.Linq;
using UnityEditor;

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
                ReInstall(asset, version);
            }
        }

        /// <summary>
        /// Re-install the SDK.
        /// </summary>
        [MenuItem("Assets/Metaverse/Re-Install Metaverse SDK")]
        public static void ReInstallSDK()
        {
            var sdkPackageGuid = AssetDatabase.FindAssets("MVCESDK_", new[] { PackagePath }).FirstOrDefault();
            var asset = AssetDatabase.GUIDToAssetPath(sdkPackageGuid);
            if (string.IsNullOrEmpty(asset))
                return;
            if (!EditorUtility.DisplayDialog(
                "Install SDK", "You are about to install the SDK again. Are you sure you want to do that? This won't restart Unity.", "Yes", "Cancel"))
                return;
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            return;
#endif
            Install(asset);
        }

        /// <summary>
        /// Uninstalls the SDK.
        /// </summary>
        [MenuItem("Assets/Metaverse/Uninstall Metaverse SDK")]
        public static void UninstallSDK()
        {
            if (!EditorUtility.DisplayDialog(
                DialogTitle,
                "Are you sure you want to uninstall the SDK?",
                "Yes",
                "No"))
                return;

#if METAVERSE_CLOUD_ENGINE_INTERNAL
            return;
#endif
            Uninstall(false);
        }

        private static bool Uninstall(bool isUpdating = true)
        {
            if (AssetDatabase.IsValidFolder(SdkPath))
            {
                if (isUpdating && !EditorUtility.DisplayDialog(
                    DialogTitle,
                    "A Metaverse Cloud Engine SDK update is available. Would you like to update now?",
                    "Yes",
                    "No"))
                    return false;

                if (!AssetDatabase.DeleteAsset(SdkPath))
                {
                    EditorUtility.DisplayDialog("Delete SDK failed", "Failed to delete the SDK folder.", "Ok");
                    return false;
                }
            }

            ScriptingDefines.Remove(new[] { ScriptingDefines.DefaultSymbols });
            return true;
        }

        private static void ReInstall(string asset, string version)
        {
            void Update(bool restartEditor)
            {
                SetVersion(version);

                if (restartEditor)
                    RefreshEditor();
            }

            if (Uninstall())
            {
                void InstallDelayed()
                {
                    EditorApplication.update -= InstallDelayed;
                    Install(asset);
                }
                EditorApplication.update += InstallDelayed;
                return;
            }

            Update(false);
        }

        private static void Install(string package, bool interactive = false)
        {
            MetaverseTmpInstaller.InstallTmpEssentials();
            AssetDatabase.ImportPackage(package, interactive);
        }

        public static void RefreshEditor()
        {
            static void RestartProj() { EditorApplication.update -= RestartProj; EditorApplication.OpenProject(Environment.CurrentDirectory); }
            EditorApplication.update += RestartProj;
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