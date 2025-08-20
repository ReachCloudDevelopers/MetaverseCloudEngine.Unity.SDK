using System;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.XR;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR;
#if (UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID) && MV_UNITY_AR_FOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component used to perform actions based on a specific platform.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.PreInitialization)]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Platform/Platform API")]
    [HideMonoScript]
    [DeclareFoldoutGroup("AR / VR")]
    [DeclareFoldoutGroup("Behavior")]
    public class PlatformAPI : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The event callbacks that tell the app when a platform is active or inactive.
        /// </summary>
        [Serializable]
        public class PlatformEvents
        {
            [Tooltip(
                "An event that is raised when the current platform is considered supported by this PlatformHelper instance.")]
            public UnityEvent onSupported;

            [Tooltip(
                "An event that is raised when the current platform is considered unsupported by this PlatformHelper instance.")]
            public UnityEvent onUnsupported;
        }

        /// <summary>
        /// The action that should occur on this object when the platform is unsupported.
        /// </summary>
        public enum UnsupportedBehaviorType
        {
            /// <summary>
            /// Do nothing.
            /// </summary>
            Nothing,

            /// <summary>
            /// Deactivate this object.
            /// </summary>
            Deactivate,

            /// <summary>
            /// Destroy this object completely.
            /// </summary>
            Destroy,
        }

        /// <summary>
        /// The support for XR platforms.
        /// </summary>
        public enum XRSupportType
        {
            /// <summary>
            /// XR is not supported.
            /// </summary>
            NotSupported,

            /// <summary>
            /// Do not care whether the device supports XR or not.
            /// </summary>
            Supported,

            /// <summary>
            /// XR is required.
            /// </summary>
            Required
        }

        /// <summary>
        /// The type of version comparison to perform.
        /// </summary>
        [Flags]
        public enum VersionComparison
        {
            /// <summary>
            /// Versions are equal.
            /// </summary>
            Equal = 1 << 0,
            /// <summary>
            /// The current version is greater than the specified version.
            /// </summary>
            GreaterThan = 1 << 1,
            /// <summary>
            /// The current version is less than the specified version.
            /// </summary>
            LessThan = 1 << 2,
        }

        [InfoBox("Use this component to define behavior that occurs only on specific platforms.")]
        [SerializeField]
        [Tooltip(
            "The platforms that are considered supported by this PlatformHelper instance. If the current platform is included in this value, the IsCurrentPlatformSupported property will return true.")]
        private Platform platform = (Platform)~0;
        
        [SerializeField]
        [Tooltip("The application version to compare against the current application version.")]
        private string applicationVersion = "";
        [SerializeField]
        [Tooltip("The type of comparison to perform between the current application version and the specified application version.")]
        private VersionComparison versionComparison;

        [SerializeField]
        [Tooltip(
            "A flag indicating whether mobile platforms accessed through WebGL should be considered supported. If this flag is true and the application is running on WebGL, the Android and iOS platforms will be considered supported if the current platform is a mobile platform accessed through WebGL.")]
        [HideInInspector]
        private bool includeMobileWebPlatforms = true;

        [SerializeField]
        [Tooltip(
            "If true, the platform check will be performed after the Start method is called.")]
        private bool afterStart;

        [FormerlySerializedAs("waitForMetaSpace")]
        [Tooltip("If true, will wait until the metaspace is initialized before performing the platform check.")]
        [SerializeField]
        private bool waitForMetaSpaceInitialize;

        [Group("AR / VR")]
        [SerializeField]
        [Tooltip(
            "The level of VR support required or allowed by this PlatformHelper instance. If this value is Required, the IsCurrentPlatformSupported property will return false if VR is not supported or not currently active. If this value is NotSupported, the IsCurrentPlatformSupported property will return false if VR is currently active. If this value is Supported, VR support has no effect on the IsCurrentPlatformSupported property.")]
        private XRSupportType vrSupport = XRSupportType.Supported;

        [Group("AR / VR")]
        [SerializeField]
        [Tooltip(
            "The level of AR support required or allowed by this PlatformHelper instance. If this value is Required, the IsCurrentPlatformSupported property will return false if AR is not supported or not currently active. If this value is NotSupported, the IsCurrentPlatformSupported property will return false if AR is currently active. If this value is Supported, AR support has no effect on the IsCurrentPlatformSupported property.")]
        private XRSupportType arSupport = XRSupportType.Supported;

        [Group("Behavior")]
        [SerializeField]
        [Tooltip(
            "A flag indicating whether the IsCurrentPlatformSupported property should return the opposite of its normal value. If this flag is true, the IsCurrentPlatformSupported property will return the opposite of its normal value.")]
        private bool invert;

        [Group("Behavior")]
        [SerializeField]
        [Tooltip(
            "The behavior to apply when the current platform is not supported. If this value is Nothing, no action will be taken. If this value is Deactivate, the game object that this component is attached to will be deactivated. If this value is Destroy, the game object that this component is attached to will be destroyed.")]
        private UnsupportedBehaviorType unsupportedBehavior;

        [Space]
        [SerializeField]
        [Tooltip("A set of events that are raised based on the IsCurrentPlatformSupported property.")]
        private PlatformEvents events = new();

        private bool _supported;
        private bool _awakeCalled;
        private bool _enabledStateChanged;
        private bool _firstTime = true;
        private bool? _isCurrentPlatformSupported;
#if (UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID) && MV_UNITY_AR_FOUNDATION
        private ARSessionState? _lastARSessionState;
#endif

        /// <summary>
        /// The supported platforms.
        /// </summary>
        public Platform Platform
        {
            get => platform;
            set => platform = value;
        }

        /// <summary>
        /// Whether to invert the behavior of the <see cref="IsCurrentPlatformSupported"/> check.
        /// </summary>
        public bool Invert
        {
            get => invert;
            set => invert = value;
        }

        /// <summary>
        /// The behavior that occurs on this object when the platform is not supported.
        /// </summary>
        public UnsupportedBehaviorType UnsupportedBehavior
        {
            get => unsupportedBehavior;
            set => unsupportedBehavior = value;
        }

        /// <summary>
        /// Events that are invoked when the platform is either supported or unsupported.
        /// </summary>
        public PlatformEvents Events
        {
            get => events;
            set => events = value;
        }

        /// <summary>
        /// Whether the current platform is supported.
        /// </summary>
        public bool IsCurrentPlatformSupported
        {
            get
            {
                _isCurrentPlatformSupported ??= CalculateIsCurrentPlatformSupported(
                    platform,
                    vrSupport,
                    arSupport,
                    includeMobileWebPlatforms,
                    applicationVersion,
                    versionComparison);
                return _isCurrentPlatformSupported.Value;
            }
        }

        private void Awake()
        {
            if (!afterStart)
                Init();
            else
                MetaverseDispatcher.AtEndOfFrame(Init);

#if (UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID) && MV_UNITY_AR_FOUNDATION
            ARSession.stateChanged += OnARSessionStateChanged;
#endif

            XRInputTrackingAPI.HmdConnected += OnDeviceConnected;
            XRInputTrackingAPI.HmdDisconnected += OnDeviceDisconnected;
        }

        private void Init()
        {
            if (waitForMetaSpaceInitialize && MetaSpace.Instance)
                MetaSpace.OnReady(Check);
            else
                Check();
        }

        private void Check()
        {
            _awakeCalled = true;
            PerformCheck();
        }

#if (UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID) && MV_UNITY_AR_FOUNDATION
        private void OnARSessionStateChanged(ARSessionStateChangedEventArgs e)
        {
            if (!this) return;
            UniTask.Void(async c =>
            {
                await UniTask.DelayFrame(1, cancellationToken: c);
                if (!this) return;
                if (destroyCancellationToken.IsCancellationRequested) return;
                if (_lastARSessionState == e.state) return;
                _lastARSessionState = e.state;
                ClearPlatformSupportCachedValue();
                PerformCheck();
            }, destroyCancellationToken);
        }
#endif

        private void OnEnable() => PerformCheck();

        private void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            XRInputTrackingAPI.HmdConnected -= OnDeviceConnected;
            XRInputTrackingAPI.HmdDisconnected -= OnDeviceDisconnected;
#if (UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID) && MV_UNITY_AR_FOUNDATION
            ARSession.stateChanged -= OnARSessionStateChanged;
#endif
        }

        private void OnDeviceConnected(InputDevice device)
        {
            UniTask.Void(async c =>
            {
                if (!this) return;
                await UniTask.DelayFrame(1, cancellationToken: c);
                if (destroyCancellationToken.IsCancellationRequested) return;
                ClearPlatformSupportCachedValue();
                PerformCheck();
            }, destroyCancellationToken);
        }

        private void OnDeviceDisconnected(InputDevice device)
        {
            if (MetaverseProgram.IsQuitting) return;
            UniTask.Void(async c =>
            {
                if (!this) return;
                await UniTask.DelayFrame(1, cancellationToken: c);
                if (destroyCancellationToken.IsCancellationRequested) return;
                ClearPlatformSupportCachedValue();
                PerformCheck();
            }, destroyCancellationToken);
        }

        private static bool IsVRDeviceConsideredActive()
        {
            if (!MVUtils.IsVRCompatible())
                return false;
            return XRSettings.isDeviceActive || Application.isMobilePlatform;
        }

        public void PerformCheck()
        {
            if (!_awakeCalled) return;
            if (!this) return;
            if (!enabled) return;

            var valid = !invert ? IsCurrentPlatformSupported : !IsCurrentPlatformSupported;
            if (!valid)
            {
                if (!_supported && !_firstTime)
                    return;

                if (unsupportedBehavior != UnsupportedBehaviorType.Destroy)
                {
                    UniTask.Void(async c =>
                    {
                        await UniTask.DelayFrame(1, cancellationToken: c);
                        if (!this) return;
                        if (destroyCancellationToken.IsCancellationRequested) return;
                        if (_supported) return;
                        if (!enabled) return;
                        events?.onUnsupported?.Invoke();
                    }, destroyCancellationToken);
                }

                switch (unsupportedBehavior)
                {
                    case UnsupportedBehaviorType.Deactivate:
                        gameObject.SetActive(false);
                        break;
                    case UnsupportedBehaviorType.Destroy:
                        Destroy(gameObject);
                        break;
                    case UnsupportedBehaviorType.Nothing:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _supported = false;
                _firstTime = false;
            }
            else
            {
                if (_supported) return;
                if (unsupportedBehavior == UnsupportedBehaviorType.Deactivate)
                    gameObject.SetActive(true);

                UniTask.Void(async c =>
                {
                    await UniTask.DelayFrame(1, cancellationToken: c);
                    if (!this) return;
                    if (destroyCancellationToken.IsCancellationRequested) return;
                    if (!_supported) return;
                    if (!enabled) return;
                    events?.onSupported?.Invoke();
                }, destroyCancellationToken);

                _supported = true;
            }
        }

        private void ClearPlatformSupportCachedValue()
        {
            _isCurrentPlatformSupported = null;
        }

        public static bool CalculateIsCurrentPlatformSupported(
            Platform platform,
            XRSupportType vrSupport,
            XRSupportType arSupport,
            bool includeMobileWebPlatforms,
            string version,
            VersionComparison versionComparison)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL || MV_SDK_DEV
            if (!string.IsNullOrEmpty(version) && versionComparison != 0)
            {
                var currentVersion = Application.version;
                if (Version.TryParse(currentVersion, out var currentVer) &&
                    Version.TryParse(version, out var requiredVer))
                {
                    var isEqual = currentVer == requiredVer;
                    var isGreaterThan = currentVer > requiredVer;
                    var isLessThan = currentVer < requiredVer;
                    
                    if (isEqual && !versionComparison.HasFlag(VersionComparison.Equal))
                        return false;
                    if (isGreaterThan && !versionComparison.HasFlag(VersionComparison.GreaterThan))
                        return false;
                    if (isLessThan && !versionComparison.HasFlag(VersionComparison.LessThan))
                        return false;
                }
            }
#endif
            
            if (MVUtils.IsVRCompatible())
            {
                if (!IsVRDeviceConsideredActive() && vrSupport == XRSupportType.Required)
                    return false;
                if (IsVRDeviceConsideredActive() && vrSupport == XRSupportType.NotSupported)
                    return false;
            }
            else if (vrSupport == XRSupportType.Required)
                return false;

            if (MVUtils.IsARCompatible())
            {
                switch (XRSettings.isDeviceActive)
                {
                    case false when arSupport == XRSupportType.Required:
                    case true when arSupport == XRSupportType.NotSupported:
                        return false;
                }
            }
            else if (arSupport == XRSupportType.Required)
                return false;

            if (platform.HasFlag(MetaverseProgram.GetCurrentPlatform()))
                return true;

            if (includeMobileWebPlatforms && Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (MetaverseProgram.IsAndroidWebPlatform())
                    return platform.HasFlag(Platform.Android);
                if (MetaverseProgram.IsIOSWebPlatform())
                    return platform.HasFlag(Platform.iOS);
            }

            return false;
        }

        public void OnSimulatorDeviceChanged()
        {
            ClearPlatformSupportCachedValue();
            PerformCheck();
        }

        #region Device Simulator Support

#if UNITY_EDITOR
        internal static class DeviceSimulatorPlatformChangeTracker
        {
            private static RuntimePlatform? _lastPlatform;

            [UnityEditor.InitializeOnEnterPlayMode]
            internal static void Init()
            {
                UnityEditor.EditorApplication.update += OnEditorUpdate;
            }

            private static void OnEditorUpdate()
            {
                if (_lastPlatform == UnityEngine.Device.Application.platform) return;
                foreach (var p in MVUtils.FindObjectsOfTypeNonPrefabPooled<PlatformAPI>(true))
                {
                    if (p._awakeCalled)
                        p.OnSimulatorDeviceChanged();
                }

                _lastPlatform = UnityEngine.Device.Application.platform;
            }
        }
#endif

        #endregion
    }
}
