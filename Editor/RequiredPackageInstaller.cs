using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Installer.Editor
{
    public static class RequiredPackageInstaller
    {
        private static readonly string[] PackagesToInstall = {
            "com.unity.burst@1",
            "com.unity.cinemachine@2",
            "com.unity.inputsystem@1",
            "com.unity.mathematics@1",
            "com.unity.nuget.newtonsoft-json@3",
            "com.unity.render-pipelines.universal@12",
            "com.unity.xr.management@4",
            "com.unity.xr.interaction.toolkit@2",
            "com.unity.visualscripting@1",
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
        };

        private static bool _requestPackages;
        private static AddAndRemoveRequest _packageRequest;

        [InitializeOnLoadMethod]
        private static void InstallPackages()
        {
            if (_requestPackages)
                return;
        
            _requestPackages = true;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= OnEditorUpdate;
                _packageRequest = null;
                _requestPackages = false;
                return;
            }
        
            EditorUtility.DisplayProgressBar("Importing Dependencies", "Importing Packages...", 1);
        
            if (_requestPackages)
            {
                _packageRequest = Client.AddAndRemove(packagesToAdd: PackagesToInstall);
                _requestPackages = false;
            }

            if (_packageRequest == null)
                return;

            if (_packageRequest.Status != StatusCode.InProgress)
            {
                if (_packageRequest.Status == StatusCode.Success)
                    OnPackagesInstalled();
                else
                    OnPackagesFailed();

                _packageRequest = null;
                EditorApplication.update -= OnEditorUpdate;
            }
        }

        private static void OnPackagesInstalled()
        {
            ScriptingDefines.Add(new[] { ScriptingDefines.DefaultSymbols });

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                var pipelineAsset = (RenderPipelineAsset[])Resources.FindObjectsOfTypeAll(typeof(RenderPipelineAsset));
                var urpPipelineAsset = pipelineAsset.FirstOrDefault(x => x.GetType().Name.Equals("UniversalRenderPipelineAsset"));
                if (urpPipelineAsset != null) GraphicsSettings.defaultRenderPipeline = urpPipelineAsset;
            }
        
            Client.Resolve();
            EditorUtility.ClearProgressBar();
        }

        private static void OnPackagesFailed()
        {
            ScriptingDefines.Remove(new [] { ScriptingDefines.DefaultSymbols });
            Debug.LogError("Failed to install packages: " + _packageRequest.Error.message);
            EditorUtility.ClearProgressBar();
        }
    }
}

