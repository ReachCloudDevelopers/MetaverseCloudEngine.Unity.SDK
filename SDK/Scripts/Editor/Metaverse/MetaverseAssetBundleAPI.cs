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
using UnityEngine.Experimental.Rendering;

namespace MetaverseCloudEngine.Unity.Editors
{
    public static class MetaverseAssetBundleAPI
    {
        public class BundleBuild
        {
            public Platform Platforms { get; set; }
            public string OutputPath { get; set; }
        }

        private const string MetaverseBuildDirectory = "Builds/MetaverseAssetBundles";

        private static bool TryForceSaveOpenScenes()
        {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var openScene = EditorSceneManager.GetSceneAt(i);
                if (!openScene.IsValid() || !openScene.isDirty)
                    continue;

                if (string.IsNullOrEmpty(openScene.path))
                    return false;

                if (!EditorSceneManager.SaveScene(openScene))
                    return false;
            }

            return true;
        }

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
            
            var originalBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
            EditorUserBuildSettings.selectedStandaloneTarget = Application.platform == RuntimePlatform.OSXEditor
                ? BuildTarget.StandaloneOSX
                : BuildTarget.StandaloneWindows64;
            
#if UNITY_EDITOR_OSX
            if (Application.platform == RuntimePlatform.OSXEditor)
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
#endif

            preProcessBuild?.Invoke();
            try
            {
                // Make sure all dirty assets are saved and cleaned up.
                AssetDatabase.SaveAssets();
                MetaPrefabLoadingAPI.ClearPool(false);
                MetaverseProjectConfigurator.ConfigureXRLoaders(true);

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
                if (targetPlatforms.Contains(Platform.WebGL) && targetPlatforms[0] == Platform.WebGL)
                {
                    // Always compile webgl last.
                    targetPlatforms.Remove(Platform.WebGL);
                    targetPlatforms.Add(Platform.WebGL);
                }

                var progressOrder = new List<Platform>();
                var seenPlatformIds = new HashSet<int>();
                foreach (var targetPlatform in targetPlatforms)
                {
                    var platformKey = (int)targetPlatform;
                    if (seenPlatformIds.Add(platformKey))
                        progressOrder.Add(targetPlatform);
                }

                var totalProgressPlatforms = progressOrder.Count;
                if (totalProgressPlatforms > 0)
                    MetaverseAssetBundleBuildProgress.Begin(bundleId, progressOrder);

                var processedPlatforms = 0;
                var platformProgressIndex = 0;
                var buildTerminatedWithError = false;
                var buildWasCancelled = false;
                var hadPlatformFailures = false;

                // Let's start processing each platform.
                var alreadyDonePlatforms = new List<int>();
                foreach (var platform in targetPlatforms.Where(
                             platform => !alreadyDonePlatforms.Contains((int)platform)))
                {
                    alreadyDonePlatforms.Add((int)platform);
                    platformProgressIndex++;

                    BuildTarget buildTarget;
                    if (platform != Platform.AndroidVR)
                    {
                        if (!Enum.TryParse(platform.ToString(), out buildTarget))
                        {
                            if (totalProgressPlatforms > 0)
                            {
                                processedPlatforms++;
                                MetaverseAssetBundleBuildProgress.ReportPlatformSkipped(
                                    platform,
                                    platformProgressIndex,
                                    totalProgressPlatforms,
                                    processedPlatforms,
                                    "Unable to map platform to Unity BuildTarget.");
                            }
                            continue;
                        }
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
                        if (totalProgressPlatforms > 0)
                        {
                            processedPlatforms++;
                            MetaverseAssetBundleBuildProgress.ReportPlatformSkipped(
                                platform,
                                platformProgressIndex,
                                totalProgressPlatforms,
                                processedPlatforms,
                                "Build target support is missing in this Unity Editor.");
                        }
                        continue;
                    }
                    
#if UNITY_2023_1_OR_NEWER
                    //UnityEditor.QNX.Settings.architecture = EmbeddedArchitecture.Arm64;
#else
                    EditorUserBuildSettings.selectedQnxArchitecture = QNXArchitecture.Arm64;
#endif
                    
                    EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);

                    if (totalProgressPlatforms > 0)
                    {
                        MetaverseAssetBundleBuildProgress.ReportPlatformStarted(
                            platform,
                            platformProgressIndex,
                            totalProgressPlatforms,
                            processedPlatforms);
                    }

                    var targetBundleId = $"{bundleId}_{platform}";
                    var validAssetNames = new List<string>();

                    CollectAssetNamesFromAssetBundleDependencies(dependencies, targetBundleId, validAssetNames);
                    if (validAssetNames.Count == 0)
                        throw new BuildFailedException("There were no valid assets to build.");
                    ApplyPlatformOptions(platformOptions, platform, validAssetNames, group);

                    // Create sub-output directory.
                    var outputFolder = $"{Path.Combine(MetaverseBuildDirectory, targetBundleId)}_Data";
                    if (!Directory.Exists(outputFolder))
                        Directory.CreateDirectory(outputFolder);

                    // Configure editor and settings.
#if MV_ARFOUNDATION
                    if (buildTarget != BuildTarget.WebGL)
                        UnityEditor.XR.ARSubsystems.ARBuildProcessor.PreprocessBuild(buildTarget);
#endif
#if UNITY_EDITOR
                    MetaPrefab.PreProcessBuild();
                    StartDisabled.PreProcessBuild();
#endif
                    ApplyGraphicsApiForCurrentPlatform(buildTarget, platform);
                    if (buildTarget != BuildTarget.WebGL && buildTarget != BuildTarget.Android)
                        PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.Mono2x);
                    else PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    
                    try
                    {
#if UNITY_EDITOR
                        MetaverseProgram.IsBuildingAssetBundle = true;
#endif
                        try
                        {
                            if (MetaverseProgram.Initialized && !MetaverseProgram.ApiClient.Account.IsLoggedIn)
                            {
                                throw new InvalidOperationException("Account was logged out while trying to upload.");
                            }

                            if (!TryForceSaveOpenScenes())
                            {
                                if (!forceSaveScene)
                                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                                else
                                    throw new OperationCanceledException("Unable to auto-save open scenes before building. Please save them and try again.");
                            }

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
                                    UseCache = false, // Sadly Unity 2022.3.X causing this to freeze dramatically long periods of time, so it's best to set this to false.
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

                            if (totalProgressPlatforms > 0)
                            {
                                processedPlatforms++;
                                MetaverseAssetBundleBuildProgress.ReportPlatformCompleted(
                                    platform,
                                    platformProgressIndex,
                                    totalProgressPlatforms,
                                    processedPlatforms);
                            }
                        }
                        finally
                        {
#if UNITY_EDITOR
                            MetaverseProgram.IsBuildingAssetBundle = false;
#endif
                        }
                    }
                    catch (BuildFailedException e)
                    {
                        MetaverseProgram.Logger.Log(
                            $"<b><color=red>Failed</color></b> to build platform {platform}. Check the console for build errors: " +
                            e.Message);
                        if (totalProgressPlatforms > 0)
                        {
                            MetaverseAssetBundleBuildProgress.ReportPlatformFailed(
                                platform,
                                platformProgressIndex,
                                totalProgressPlatforms,
                                processedPlatforms,
                                e.Message,
                                false);
                        }
                        hadPlatformFailures = true;
                    }
                    catch (OperationCanceledException e)
                    {
                        MetaverseProgram.Logger.Log(
                            $"<b><color=red>Failed</color></b> to build platform {platform}: Build was cancelled. Check the console for build errors.");
                        if (totalProgressPlatforms > 0)
                        {
                            var reason = string.IsNullOrEmpty(e.Message) ? "Build cancelled." : e.Message;
                            MetaverseAssetBundleBuildProgress.ReportPlatformFailed(
                                platform,
                                platformProgressIndex,
                                totalProgressPlatforms,
                                processedPlatforms,
                                reason,
                                true);
                        }
                        successfulBuilds.Clear();
                        buildWasCancelled = true;
                        break;
                    }
                    catch (AccessViolationException e)
                    {
                        MetaverseProgram.Logger.Log(
                            $"<b><color=red>Failed</color></b> to build platform {platform}: Access violation. Check the console for build errors: " +
                            e.Message);
                        if (totalProgressPlatforms > 0)
                        {
                            MetaverseAssetBundleBuildProgress.ReportPlatformFailed(
                                platform,
                                platformProgressIndex,
                                totalProgressPlatforms,
                                processedPlatforms,
                                e.Message,
                                true);
                        }
                        buildTerminatedWithError = true;
                        successfulBuilds.Clear();
                        break;
                    }
                    catch (Exception e)
                    {
                        MetaverseProgram.Logger.Log(
                            $"<b><color=red>Failed</color></b> to build platform {platform}: An unexpected error occurred. Check the console for build errors: " +
                            e.Message);
                        if (totalProgressPlatforms > 0)
                        {
                            MetaverseAssetBundleBuildProgress.ReportPlatformFailed(
                                platform,
                                platformProgressIndex,
                                totalProgressPlatforms,
                                processedPlatforms,
                                e.Message,
                                true);
                        }
                        buildTerminatedWithError = true;
                        successfulBuilds.Clear();
                        break;
                    }

                    AssetDatabase.ReleaseCachedFileHandles();
                    AssetDatabase.Refresh();
                    yield return null;
                }

                if (totalProgressPlatforms > 0)
                {
                    var finalHadFailures = buildWasCancelled || buildTerminatedWithError || hadPlatformFailures;
                    string finishedMessage;
                    if (buildWasCancelled)
                        finishedMessage = $"Asset bundle build cancelled after {processedPlatforms}/{totalProgressPlatforms} platforms.";
                    else if (buildTerminatedWithError)
                        finishedMessage = $"Asset bundle build failed after {processedPlatforms}/{totalProgressPlatforms} platforms.";
                    else if (hadPlatformFailures)
                        finishedMessage = $"Asset bundle build completed with errors ({processedPlatforms}/{totalProgressPlatforms} platforms succeeded).";
                    else
                        finishedMessage = $"Asset bundle build complete ({processedPlatforms}/{totalProgressPlatforms} platforms).";

                    MetaverseAssetBundleBuildProgress.ReportBuildFinished(
                        finishedMessage,
                        processedPlatforms,
                        totalProgressPlatforms,
                        finalHadFailures);
                }

                // Switch back to the original build target before invoking callbacks/dialogs
                var defaultGroup = BuildPipeline.GetBuildTargetGroup(originalBuildTarget);
                EditorUserBuildSettings.SwitchActiveBuildTarget(defaultGroup, originalBuildTarget);
                EditorUserBuildSettings.selectedBuildTargetGroup = defaultGroup;
                if (defaultGroup == BuildTargetGroup.Standalone)
                    EditorUserBuildSettings.selectedStandaloneTarget = originalBuildTarget;

                completed?.Invoke(successfulBuilds);
            }
            finally
            {
                if (lockedAssemblies)
                    EditorApplication.UnlockReloadAssemblies();
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
                if (!assetName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (assetName.Contains("/Editor/"))
                    continue;

                var importer = AssetImporter.GetAtPath(assetName);
                if (!importer)
                    continue;

                if (importer.assetBundleName == targetBundleId)
                    continue;
                
                if ((importer.hideFlags & HideFlags.DontSave) != 0)
                    continue;

                importer.SetAssetBundleNameAndVariant(targetBundleId, string.Empty);
                validAssetNames.Add(assetName);
                if (AssetDatabase.WriteImportSettingsIfDirty(assetName))
                    AssetDatabase.ImportAsset(assetName, ImportAssetOptions.DontDownloadFromCacheServer);
            }
        }

        private static void ApplyPlatformOptions(IDictionary<Platform, BundlePlatformOptions> platformOptions,
            Platform platform, List<string> validAssetNames,
            BuildTargetGroup group)
        {
            if (platformOptions == null || !platformOptions.TryGetValue(platform, out var options))
                return;

            var deps = AssetDatabase.GetDependencies(validAssetNames.ToArray(), true);
            foreach (var path in deps)
            {
                var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter)
                {
                    var hasAlphaChannel = TextureHasAlpha(textureImporter, path);
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
                                    BuildTargetGroup.Android or BuildTargetGroup.iOS => hasAlphaChannel
                                        ? TextureImporterFormat.ETC2_RGBA8Crunched
                                        : TextureImporterFormat.ETC_RGB4Crunched,
                                    BuildTargetGroup.WebGL => TextureImporterFormat.ETC2_RGBA8Crunched,
                                    _ => TextureImporterFormat.DXT5Crunched
                                };
                            var nonCrunchedFormat =
                                group switch
                                {
                                    _ => hasAlphaChannel
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
                                    ? hasAlphaChannel
                                        ? TextureImporterFormat.ETC2_RGBA8Crunched
                                        : TextureImporterFormat.ETC2_RGB4
                                    : hasAlphaChannel
                                        ? TextureImporterFormat.DXT5Crunched
                                        : TextureImporterFormat.DXT1Crunched;
                            var nonCrunchedFormat =
                                hasAlphaChannel
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

        private static bool TextureHasAlpha(TextureImporter importer, string assetPath)
        {
            if (importer == null)
                return false;

            if (importer.alphaSource is TextureImporterAlphaSource.FromGrayScale or
                TextureImporterAlphaSource.FromInput)
            {
                return true;
            }

            if (importer.alphaIsTransparency)
                return true;

            var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            if (!texture)
                return false;

            try
            {
                if (texture.graphicsFormat != GraphicsFormat.None)
                    return GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
            }
            catch
            {
                // Some texture types may not expose graphics format; fall through.
            }

            if (texture is Texture2D texture2D)
            {
                try
                {
                    var graphicsFormat =
                        GraphicsFormatUtility.GetGraphicsFormat(texture2D.format, importer.sRGBTexture);
                    return GraphicsFormatUtility.HasAlphaChannel(graphicsFormat);
                }
                catch
                {
                    // ignore invalid conversion attempts
                }
            }

            return false;
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
