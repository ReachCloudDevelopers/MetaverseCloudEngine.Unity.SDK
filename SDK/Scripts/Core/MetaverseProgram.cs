/* Copyright (C) 2024 Reach Cloud - All Rights Reserved */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Scripting;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Services.Implementation;
using MetaverseCloudEngine.Unity.Web.Implementation;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[assembly: Preserve]
namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    /// This is the main entry point for the Metaverse app. It provides access to the
    /// Metaverse Cloud Engine Web API and other services provided by the app.
    /// </summary>
    public static class MetaverseProgram
    {
        private readonly static Queue<Action> InitializationCallbacks = new();

        static MetaverseProgram()
        {
            Prefs = new EncryptedPrefs();
            Logger = new UnityDebugLogger();
        }

        /// <summary>
        /// This will be true when the app is fully initialized. You can use 
        /// <see cref="OnInitialized(Action)"/> to ensure an action is performed
        /// after initialization occurs.
        /// </summary>
        public static bool Initialized { get; private set; }

        /// <summary>
        /// Gets a value indicating whether an app update is required.
        /// </summary>
        public static bool AppUpdateRequired => RuntimeServices?.UpdateRequired == true;

        /// <summary>
        /// These are services provided by the Metaverse app.
        /// </summary>
        public static MetaverseRuntimeServices RuntimeServices { get; private set; }

        /// <summary>
        /// This is class provides direct access to the Metaverse Cloud Engine Web API.
        /// </summary>
        public static MetaverseClient ApiClient { get; private set; }

        /// <summary>
        /// Use this for app configuration.
        /// </summary>
        public static IPrefs Prefs { get; private set; }

        /// <summary>
        /// Provides abstract logging functionality. This should be used
        /// as opposed to the Unity <see cref="Debug.Log(object)"/> method.
        /// </summary>
        public static IDebugLogger Logger { get; }

        /// <summary>
        /// Will be true if the Application is being quit.
        /// </summary>
        public static bool IsQuitting { get; internal set; }
        
#if UNITY_EDITOR
        private const string IsBuildingAssetBundleKey = "MetaverseAssetBundleAPI_ResetPlatform";
        private const string InternalIsIsPackagingSDKKey = "MetaverseAssetBundleAPI_IsPackagingSDK";

        public static bool IsBuildingAssetBundle {
            get => UnityEditor.SessionState.GetBool(IsBuildingAssetBundleKey, false);
            set => UnityEditor.SessionState.SetBool(IsBuildingAssetBundleKey, value);
        }
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
        public static bool InternalIsPackagingSDK {
            get => UnityEditor.SessionState.GetBool(InternalIsIsPackagingSDKKey, false);
            set => UnityEditor.SessionState.SetBool(InternalIsIsPackagingSDKKey, value);
        }
#endif
#endif

        /// <summary>
        /// Whether the application currently allows interaction with
        /// Crypto Currency and blockchain content.
        /// </summary>
        public static bool AllowsCrypto {
            get {
                if (MVUtils.IsOculusPlatform())
                    return false;
                if (AppUpdateRequired)
                    return false;
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                if (RuntimeServices.InternalOrganizationManager.SelectedOrganization != null &&
                    !RuntimeServices.InternalOrganizationManager.SelectedOrganization.SupportsCrypto)
                    return false;
#endif
                return Prefs.GetInt(nameof(AllowsCrypto), 1) == 1;
            }
            set {
                Prefs.SetInt(nameof(AllowsCrypto), value ? 1 : 0);
            }
        }

        /// <summary>
        /// This is the app's current version info. This is only available after <see cref="Initialized"/> is true.
        /// </summary>
        public static AppVersionDto Version { get; internal set; }

        /// <summary>
        /// The current device rank. This value may not be populated immediately.
        /// </summary>
        public static float DeviceRank { get; internal set; } = 1f;
        
        /// <summary>
        /// A value indicating whether the current application context is running within the Core App.
        /// This includes Unity Editor.
        /// </summary>
        public static bool IsCoreApp {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            get => true;
#else
            get => false;
#endif
        }

        /// <summary>
        /// When the application is launched, the link that was used to launch may contain
        /// launch arguments for the state of the application. 
        /// This property will be populated with that data.
        /// </summary>
        internal static IDictionary<string, string> LaunchArguments { get; set; }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            GraphicsSettings.defaultRenderPipeline = null; // Reset the default render pipeline to allow custom pipelines.
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void UnloadAllAssetBundles()
        {
            AssetBundle.UnloadAllAssetBundles(true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Main_Runtime()
        {
            Main();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void Main_Editor()
        {
            if (!Application.isPlaying && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.EditorApplication.delayCall += Installer.ScriptingDefines.OnMainPackageInstalled;
                Main();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void UnloadAssetsOnPlay() => AssetBundle.UnloadAllAssetBundles(true);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AppQuitDetector()
        {
            
            Application.wantsToQuit += () => IsQuitting = true;
            Application.quitting += () => IsQuitting = true;
        }

        private static void Main()
        {
            if (Initialized)
                return;
#if UNITY_EDITOR
            Application.logMessageReceivedThreaded += (condition, trace, type) =>
            {
                if (type is LogType.Exception or LogType.Error && condition.Contains("Exception"))
                    Logger.LogError(condition + "\n" + trace);
            };
#endif

            MVUtils.FreeUpMemory(() =>
            {
                ConfigureUnityApplication();

                Prefs ??= new EncryptedPrefs();
                
                try
                {
                    var webClient = MetaverseWebClient.CreateNew();
                    ApiClient = new MetaverseClient(
                        MetaverseConstants.Urls.ApiEndpoint,
                        client: webClient,
                        assetDownloader: webClient,
                        imageDownloader: webClient,
                        taskHandler: webClient)
                    {
                        DeviceIdProvider = new UnityDeviceIdProvider(),
                        Account =
                        {
                            UseCookieAuthentication = Application.platform == RuntimePlatform.WebGLPlayer,
                        }
                    };

                    DetectAssetVersion();

                    RuntimeServices = new MetaverseRuntimeServices(ApiClient, Prefs);
                    RuntimeServices.InitializeAsync().Then(OnInitSuccess, Crash);
                }
                catch (Exception e)
                {
                    Crash(e);
                }
            });
        }

        private static void DetectAssetVersion()
        {
            ApiClient.AssetVersion = AssetUploadVersionForPlatform(GetCurrentPlatform(false));
        }

        private static void OnInitSuccess()
        {
            Initialized = true;

            while (InitializationCallbacks.Count > 0)
            {
                try
                {
                    InitializationCallbacks.Dequeue()?.Invoke();
                }
                catch (Exception e)
                {
                    Logger?.LogError(e);
                }
            }
        }

        private static void Crash(object err)
        {
            Logger.LogError(err ?? "Unknown Error");
#if !UNITY_EDITOR
            UnityEngine.Diagnostics.Utils.NativeError(err?.ToString() ?? "Unknown Error");
            UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.FatalError);
#else
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static void ConfigureUnityApplication()
        {
            if (!Application.isPlaying)
                return;

            ConfigureApplicationQualitySettings();

            InputSystem.settings.SetInternalFeatureFlag("USE_OPTIMIZED_CONTROLS", true);
            InputSystem.settings.SetInternalFeatureFlag("USE_READ_VALUE_CACHING", true);   

            Texture.allowThreadedTextureCreation = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.runInBackground = true;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            SceneManagerAPI.overrideAPI = new SceneManagement.MetaverseSceneManagerAPI();

#if !UNITY_WEBGL || UNITY_EDITOR
            Caching.defaultCache.ClearCache((int)TimeSpan.FromHours(48).TotalSeconds);
#endif
        }

        /// <summary>
        /// Configures the application quality settings based on the current platform.
        /// </summary>
        public static void ConfigureApplicationQualitySettings()
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = false;

            if (Application.isMobilePlatform)
            {
                if (XRSettings.enabled)
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    MVUtils.SafelyAdjustXRResolutionScale(MetaverseConstants.XR.DefaultXRResolutionScale);
                    QualitySettings.antiAliasing = MetaverseConstants.XR.DefaultAntiAliasing;
                }
                else
                {
                    Application.targetFrameRate = 60;
                    QualitySettings.vSyncCount = 1;
                }
            }
            else
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = 60;
                }
                else
                {
                    QualitySettings.vSyncCount = 1;
                    Application.targetFrameRate = 120;
                }
            }
        }

        public static string AssetUploadVersionForPlatform(Platform platform)
        {
            // 2022.1.17 breaking changes:
            // - Switch from Unity 2021 to Unity 2022
            const string baseVersion = "2022.1.17";
            return baseVersion;
        }

        /// <summary>
        /// Use this method to safely perform functionality after the <see cref="MetaverseProgram"/> is
        /// initialized (i.e. <see cref="Initialized"/> is true). This will trigger the action immediately
        /// if the <see cref="MetaverseProgram"/> is already initialized.
        /// </summary>
        /// <param name="action">The action to perform post-initialization.</param>
        public static void OnInitialized(Action action)
        {
            if (action == null) return;
            if (Initialized) action();
            else InitializationCallbacks.Enqueue(action);
        }

#if !UNITY_EDITOR && UNITY_WEBGL && METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern bool IsAndroidWebGL();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern bool IsIOSWebGL();
#endif

        /// <summary>
        /// Returns a value indicating whether this is Android running Web GL.
        /// </summary>
        /// <returns></returns>
        public static bool IsAndroidWebPlatform()
        {
#if !UNITY_EDITOR && UNITY_WEBGL && METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            return IsAndroidWebGL();
#else
            return false;
#endif
        }

        /// <summary>
        /// Returns a value indicating whether this is iOS running Web GL.
        /// </summary>
        /// <returns></returns>
        public static bool IsIOSWebPlatform()
        {
#if !UNITY_EDITOR && UNITY_WEBGL && METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            return IsIOSWebGL();
#else
            return false;
#endif
        }

        /// <summary>
        /// Gets the current runtime platform supported by the core app.
        /// </summary>
        /// <param name="allowSimulation">Whether Unity's device simulation should count towards the current platform.</param>
        /// <returns></returns>
        public static Platform GetCurrentPlatform(bool allowSimulation = true)
        {
            var platform = allowSimulation ? UnityEngine.Device.Application.platform : Application.platform;
            return platform switch
            {
                RuntimePlatform.Android => GetAndroidPlatform(),
                RuntimePlatform.IPhonePlayer => GetIOSPlatform(),
                RuntimePlatform.OSXEditor => Platform.StandaloneOSX,
                RuntimePlatform.OSXPlayer => Platform.StandaloneOSX,
                RuntimePlatform.OSXServer => Platform.StandaloneOSX,
                RuntimePlatform.LinuxEditor => Platform.StandaloneLinux64,
                RuntimePlatform.LinuxPlayer => Platform.StandaloneLinux64,
                RuntimePlatform.LinuxServer => Platform.StandaloneLinux64,
                RuntimePlatform.WebGLPlayer => Platform.WebGL,
                _ => Platform.StandaloneWindows64
            };
        }

        private static Platform GetIOSPlatform()
        {
            return Platform.iOS;
        }

        private static Platform GetAndroidPlatform()
        {
            return MVUtils.IsVRCompatible() ? Platform.AndroidVR : Platform.Android;
        }
    }
}