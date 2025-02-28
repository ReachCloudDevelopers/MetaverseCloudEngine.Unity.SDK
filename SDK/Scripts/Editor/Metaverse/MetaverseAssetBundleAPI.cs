using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Components;
using Unity.EditorCoroutines.Editor;
using UnityEditor.Build.Player;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseAssetBundleAPI
    {
        public class BundleBuild
        {
            public Platform Platforms { get; set; }
            public string OutputPath { get; set; }
        }

        public const string MetaverseBuildDirectory = "Builds/MetaverseAssetBundles";

        private static IEnumerator BuildAssetBundle(
            string bundleId,
            string[] dependencies,
            bool forceSaveScene,
            Platform platforms,
            Action<IEnumerable<BundleBuild>> completed,
            IDictionary<Platform, BundlePlatformOptions> platformOptions = null,
            Action preProcessBuild = null,
            Action postProcessBuild = null,
            bool includeIOSFix = true)
        {
            var lockedAssemblies = true;
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.ReleaseCachedFileHandles();

            preProcessBuild?.Invoke();
            try
            {
                // Ensure we start out with the default build target.
                const BuildTarget defaultBuildTarget =
#if UNITY_EDITOR_WIN
                        BuildTarget.StandaloneWindows64
#elif UNITY_EDITOR_OSX
                        BuildTarget.StandaloneOSX
#endif
                    ;
                if (EditorUserBuildSettings.activeBuildTarget != defaultBuildTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(defaultBuildTarget), defaultBuildTarget);

                // Make sure all dirty assets are saved and cleaned up.
                AssetDatabase.SaveAssets();
                MetaPrefabLoadingAPI.ClearPool(false);
                MetaverseProjectConfigurator.ConfigureXRLoaders(true);
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    if (!forceSaveScene)
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else
                        throw new OperationCanceledException("You must save the open scene before building.");
                }

                // Create the build output directory.
                if (!Directory.Exists(MetaverseBuildDirectory))
                    Directory.CreateDirectory(MetaverseBuildDirectory);

                // Initialize build parameters.
                var successfulBuilds = new List<BundleBuild>();
                var targetPlatforms = ((Platform[])Enum.GetValues(typeof(Platform)))
                    .Where(x => platforms.HasFlag(x))
                    .ToList();
                if (includeIOSFix && targetPlatforms.Contains(Platform.iOS))
                {
                    targetPlatforms.Remove(Platform.iOS);
                    targetPlatforms.Insert(0, Platform.iOS);
                }

                // Let's start processing each platform.
                var alreadyDonePlatforms = new List<int>();
                foreach (var platform in targetPlatforms.Where(
                             platform => !alreadyDonePlatforms.Contains((int)platform)))
                {
                    alreadyDonePlatforms.Add((int)platform);

                    BuildTarget buildTarget;
                    if (platform != Platform.AndroidVR)
                    {
                        if (!Enum.TryParse(platform.ToString(), out buildTarget))
                            continue;
                    }
                    else
                    {
                        buildTarget = BuildTarget.Android;
                    }

                    var group = BuildPipeline.GetBuildTargetGroup(buildTarget);
                    if (!BuildPipeline.IsBuildTargetSupported(group, buildTarget))
                    {
                        Debug.LogError(
                            $"Build target {platform} is not supported by your Unity Editor configuration. Please install the necessary development kits.");
                        continue;
                    }
                    
                    // Switch current build target before pre-processing assets.
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);
                        
#if UNITY_2023_1_OR_NEWER
                    //UnityEditor.QNX.Settings.architecture = EmbeddedArchitecture.Arm64;
#else
                    EditorUserBuildSettings.selectedQnxArchitecture = QNXArchitecture.Arm64;
#endif
                    
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);

                    var targetBundleId = bundleId + "_" + platform;
                    var validAssetNames = new List<string>();

                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        CollectAssetNamesFromAssetBundleDependencies(dependencies, targetBundleId, validAssetNames);
                        if (validAssetNames.Count == 0)
                            throw new BuildFailedException("There were no valid assets to build.");
                        ApplyPlatformOptions(platformOptions, platform, validAssetNames, group);
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }

                    // Create sub-output directory.
                    var outputFolder = Path.Combine(MetaverseBuildDirectory, targetBundleId) + "_Data";
                    if (!Directory.Exists(outputFolder))
                        Directory.CreateDirectory(outputFolder);

                    // Configure editor and settings.
#if MV_ARFOUNDATION
                    if (buildTarget != BuildTarget.WebGL)
                        UnityEditor.XR.ARSubsystems.ARBuildProcessor.PreprocessBuild(buildTarget);
#endif
                    MetaPrefab.PreProcessBuild();
                    StartDisabled.PreProcessBuild();
                    ApplyGraphicsApiForCurrentPlatform(buildTarget, platform);
                    if (buildTarget != BuildTarget.WebGL && buildTarget != BuildTarget.Android)
                        PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.Mono2x);
                    else PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    
                    try
                    {
                        MetaverseProgram.IsBuildingAssetBundle = true;
                        try
                        {
                            if (lockedAssemblies)
                            {
                                EditorApplication.UnlockReloadAssemblies();
                                lockedAssemblies = false;
                            }

                            var result = ContentPipeline.BuildAssetBundles(
                                new BundleBuildParameters(buildTarget, group, outputFolder)
                                {
                                    BundleCompression = UnityEngine.BuildCompression.LZ4,
                                    ContiguousBundles = true,
                                    ContentBuildFlags = ContentBuildFlags.StripUnityVersion,
                                    Group = group,
                                    Target = buildTarget,
                                    ScriptOptions = ScriptCompilationOptions.None,
                                    UseCache = true,
                                },
                                new BundleBuildContent
                                (
                                    new[]
                                    {
                                        new AssetBundleBuild
                                        {
                                            assetBundleName = targetBundleId,
                                            assetNames = validAssetNames.ToArray(),
                                        }
                                    }
                                ), out var results);

                            if (!lockedAssemblies)
                            {
                                EditorApplication.LockReloadAssemblies();
                                lockedAssemblies = true;
                            }
                            
                            if (result < 0)
                            {
                                MetaverseProgram.Logger.Log("<b><color=red>Build Result</color></b>: " + result);
                                throw new OperationCanceledException();
                            }

                            successfulBuilds.Add(new BundleBuild
                            {
                                OutputPath = results.BundleInfos.Select(x => x.Value.FileName).First(),
                                Platforms = platform
                            });
                        }
                        finally
                        {
                            MetaverseProgram.IsBuildingAssetBundle = false;
                        }
                    }
                    catch (BuildFailedException e)
                    {
                        MetaverseProgram.Logger.Log(
                            $"<b><color=red>Failed</color></b> to build platform {platform}. Check the console for build errors: " +
                            e.Message);
                    }
                    catch (OperationCanceledException)
                    {
                        MetaverseProgram.Logger.Log(
                            $"<b><color=red>Failed</color></b> to build platform {platform}: Build was cancelled. Check the console for build errors.");
                        successfulBuilds.Clear();
                        break;
                    }

                    AssetDatabase.ReleaseCachedFileHandles();
                    AssetDatabase.Refresh();
                    yield return null;
                }

                completed?.Invoke(successfulBuilds);
            }
            finally
            {
                if (lockedAssemblies)
                {
                    EditorApplication.UnlockReloadAssemblies();
                }
                
                const BuildTarget defaultTarget =
#if UNITY_EDITOR_WIN
                        BuildTarget.StandaloneWindows64
#elif UNITY_EDITOR_OSX
                        BuildTarget.StandaloneOSX
#endif
                    ;
                
                if (EditorUserBuildSettings.activeBuildTarget != defaultTarget &&
                    !Application.isBatchMode && EditorUtility.DisplayDialog("Reset Build Target",
                    "Would you like to reset the build target to the default platform to " + defaultTarget + "?", 
                    "Yes (Recommended)", "No, stay on " + EditorUserBuildSettings.activeBuildTarget))
                {
                    var defaultGroup = BuildPipeline.GetBuildTargetGroup(defaultTarget);
                    EditorUserBuildSettings.SwitchActiveBuildTarget(defaultGroup, defaultTarget);
                    EditorUserBuildSettings.selectedBuildTargetGroup = defaultGroup;
                    if (defaultGroup == BuildTargetGroup.Standalone)
                        EditorUserBuildSettings.selectedStandaloneTarget = defaultTarget;
                }
                
                postProcessBuild?.Invoke();
            }
        }

        private static void ApplyGraphicsApiForCurrentPlatform(BuildTarget buildTarget, Platform platform)
        {
            switch (buildTarget)
            {
                case BuildTarget.Android:
                    PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
                    PlayerSettings.SetGraphicsAPIs(buildTarget,
                        platform == Platform.AndroidVR
                            ? new[] { GraphicsDeviceType.Vulkan }
                            : new[] { GraphicsDeviceType.OpenGLES3 });
                    break;
                case BuildTarget.iOS:
                    PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
                    PlayerSettings.SetGraphicsAPIs(buildTarget, new[]
                    {
                        GraphicsDeviceType.Metal,
                    });
                    break;
                case BuildTarget.WebGL:
                    PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, true);
                    break;
                case BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64:
                    PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
                    PlayerSettings.SetGraphicsAPIs(buildTarget,
                        new[]
                        {
                            GraphicsDeviceType.Direct3D11,
                            GraphicsDeviceType.Direct3D12,
                        });
                    break;
                case BuildTarget.StandaloneLinux64:
                    PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
                    PlayerSettings.SetGraphicsAPIs(buildTarget,
                        new[]
                        {
                            GraphicsDeviceType.OpenGLCore,
                            GraphicsDeviceType.Vulkan,
                        });
                    break;
                case BuildTarget.StandaloneOSX:
                    PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
                    PlayerSettings.SetGraphicsAPIs(buildTarget,
                        new[]
                        {
                            GraphicsDeviceType.Metal,
                            GraphicsDeviceType.OpenGLCore,
                        });
                    break;
            }
        }

        private static void CollectAssetNamesFromAssetBundleDependencies(string[] dependencies, string targetBundleId,
            List<string> validAssetNames)
        {
            foreach (var assetName in dependencies)
            {
                var importer = AssetImporter.GetAtPath(assetName);
                if (!importer)
                    continue;

                if (importer.assetBundleName != targetBundleId)
                {
                    importer.SetAssetBundleNameAndVariant(targetBundleId, string.Empty);
                    validAssetNames.Add(assetName);
                    importer.SaveAndReimport();
                }
            }
        }

        private static void ApplyPlatformOptions(IDictionary<Platform, BundlePlatformOptions> platformOptions,
            Platform platform, List<string> validAssetNames,
            BuildTargetGroup group)
        {
            if (platformOptions == null || !platformOptions.TryGetValue(platform, out var options))
                return;

            var deps = AssetDatabase.GetDependencies(validAssetNames.ToArray(),
                true); // Get only the necessary dependencies.

            foreach (var path in deps)
            {
                var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter)
                {
                    var changed = false;
                    var currentSettings = textureImporter.GetPlatformTextureSettings(group.ToString());
                    var overriden = options.overrideDefaults &&
                                    textureImporter.textureType is
                                        TextureImporterType.NormalMap or
                                        TextureImporterType.Default or
                                        TextureImporterType.Sprite;

                    if (currentSettings.overridden != overriden)
                    {
                        currentSettings.overridden = overriden;
                        changed = true;
                    }

                    if (overriden)
                    {
                        if (currentSettings.maxTextureSize != (int)options.maxTextureResolution)
                        {
                            currentSettings.maxTextureSize = (int)options.maxTextureResolution;
                            changed = true;
                        }

                        var useCrunchedCompression = 
                            options.compressTextures && options.compressorQuality < 100 && !textureImporter.isReadable;
                        if (currentSettings.crunchedCompression != useCrunchedCompression)
                        {
                            currentSettings.crunchedCompression = useCrunchedCompression;
                            changed = true;
                        }

                        var compressorQuality = useCrunchedCompression ? options.compressorQuality : 100;
                        if (currentSettings.compressionQuality != compressorQuality)
                        {
                            currentSettings.compressionQuality = compressorQuality;
                            changed = true;
                        }

                        if (currentSettings.androidETC2FallbackOverride !=
                            AndroidETC2FallbackOverride.UseBuildSettings)
                        {
                            currentSettings.androidETC2FallbackOverride =
                                AndroidETC2FallbackOverride.UseBuildSettings;
                            changed = true;
                        }

                        if (textureImporter.textureType == TextureImporterType.NormalMap)
                        {
                            var crunchedFormat =
                                group switch
                                {
                                    BuildTargetGroup.Android or BuildTargetGroup.iOS => textureImporter.DoesSourceTextureHaveAlpha() || textureImporter.alphaSource ==
                                        TextureImporterAlphaSource.FromGrayScale
                                            ? TextureImporterFormat.ETC2_RGBA8Crunched
                                            : TextureImporterFormat.ETC_RGB4Crunched,
                                    BuildTargetGroup.WebGL => TextureImporterFormat.ETC2_RGBA8Crunched,
                                    _ => TextureImporterFormat.DXT5Crunched
                                };
                            var nonCrunchedFormat =
                                group switch
                                {
                                    _ => textureImporter.DoesSourceTextureHaveAlpha() || textureImporter.alphaSource ==
                                        TextureImporterAlphaSource.FromGrayScale
                                            ? TextureImporterFormat.RGBA32
                                            : TextureImporterFormat.RGB24,
                                };

                            var format = useCrunchedCompression ? crunchedFormat : nonCrunchedFormat;
                            if (currentSettings.format != format)
                            {
                                currentSettings.format = format;
                                changed = true;
                            }
                        }
                        else
                        {
                            var crunchedFormat =
                                group is BuildTargetGroup.Android or BuildTargetGroup.iOS
                                    ? textureImporter.DoesSourceTextureHaveAlpha() || textureImporter.alphaSource ==
                                    TextureImporterAlphaSource.FromGrayScale
                                        ? TextureImporterFormat.ETC2_RGBA8Crunched
                                        : TextureImporterFormat.ETC2_RGB4
                                    : textureImporter.DoesSourceTextureHaveAlpha() || textureImporter.alphaSource ==
                                    TextureImporterAlphaSource.FromGrayScale
                                        ? TextureImporterFormat.DXT5Crunched
                                        : TextureImporterFormat.DXT1Crunched;
                            var nonCrunchedFormat =
                                textureImporter.DoesSourceTextureHaveAlpha() || textureImporter.alphaSource ==
                                TextureImporterAlphaSource.FromGrayScale
                                    ? TextureImporterFormat.RGBA32
                                    : TextureImporterFormat.RGB24;

                            var format = useCrunchedCompression ? crunchedFormat : nonCrunchedFormat;
                            if (currentSettings.format != format)
                            {
                                currentSettings.format = format;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        textureImporter.SetPlatformTextureSettings(currentSettings);
                        textureImporter.SaveAndReimport();
                    }

                    continue;
                }

                var modelImporter = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!modelImporter) continue;
                if (!options.overrideDefaults) continue;
                if (modelImporter.meshCompression == options.meshCompression) continue;
                modelImporter.meshCompression = options.meshCompression;
                modelImporter.SaveAndReimport();
            }
        }

        public static void BuildPrefab(
            this GameObject prefab,
            Platform platforms,
            Action<IEnumerable<BundleBuild>> completed,
            Action onPreProcessBuild = null,
            IDictionary<Platform, BundlePlatformOptions> platformOptions = null,
            Action<object> failed = null)
        {
            if (!prefab)
            {
                failed?.Invoke("Please save the prefab to a valid path within the project.");
                return;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out var guid, out long _))
            {
                failed?.Invoke("The specified asset is not valid.");
                return;
            }

            var processors =
                MVUtils.CreateClassInstancesOfType<IMetaversePrefabBuildProcessor>()
                    .OrderByDescending(x => x.callbackOrder)
                    .ToArray();

            foreach (var proc in processors)
                proc.OnPreProcessBuild(prefab);

            var buildRoutine = BuildAssetBundle(
                guid,
                new[] { AssetDatabase.GetAssetPath(prefab) },
                forceSaveScene: false,
                platforms,
                bundles =>
                {
                    var bundleBuilds = bundles as BundleBuild[] ?? bundles.ToArray();
                    if (!bundleBuilds.Any())
                        failed?.Invoke("Check console for errors.");
                    else
                        completed?.Invoke(bundleBuilds);
                },
                platformOptions: platformOptions,
                preProcessBuild: () => { onPreProcessBuild?.Invoke(); },
                postProcessBuild: () =>
                {
                    foreach (var proc in processors)
                        proc.OnPostProcessBuild(prefab);

                }, includeIOSFix: true);

            EditorCoroutineUtility.StartCoroutineOwnerless(buildRoutine);
        }

        public static void BuildStreamedScene(
            this Scene scene,
            Platform platforms,
            Action<IEnumerable<BundleBuild>> completed,
            IDictionary<Platform, BundlePlatformOptions> platformOptions = null,
            Action<object> failed = null)
        {
            var scenePath = scene.path;
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset == null)
            {
                failed?.Invoke("Please save the current scene to a valid path within the project.");
                return;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sceneAsset, out var guid, out long _))
            {
                failed?.Invoke("The specified asset is not valid.");
                return;
            }

            var processors =
                MVUtils.CreateClassInstancesOfType<IMetaverseSceneBuildProcessor>()
                    .OrderByDescending(x => x.callbackOrder)
                    .ToArray();

            foreach (var proc in processors)
                proc.OnPreProcessBuild(scene);

            if (!EditorSceneManager.SaveScenes(new[] { scene }))
            {
                failed?.Invoke("Failed to save scenes after pre-processing build.");
                return;
            }

            var scenePaths = new[] { scenePath }
                .Concat(EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path))
                .Distinct()
                .ToArray();

            var buildRoutine = BuildAssetBundle(
                guid,
                scenePaths,
                forceSaveScene: true,
                platforms,
                bundles =>
                {
                    var bundleBuilds = bundles as BundleBuild[] ?? bundles.ToArray();
                    if (!bundleBuilds.Any())
                        failed?.Invoke("Check console for errors.");
                    else
                        completed?.Invoke(bundleBuilds);
                },
                platformOptions: platformOptions,
                postProcessBuild: () =>
                {
                    scene = SceneManager.GetSceneByPath(scenePath);
                    foreach (var proc in processors)
                        proc.OnPostProcessBuild(scene);
                    
                }, includeIOSFix: false);

            EditorCoroutineUtility.StartCoroutineOwnerless(buildRoutine);
        }
    }
}