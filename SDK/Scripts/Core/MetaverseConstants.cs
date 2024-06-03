using System;
using UnityEngine;
#if !UNITY_IOS
#if MV_OCULUS_PLUGIN
using Unity.XR.Oculus;
#endif
#endif

namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    /// Constants used by the Metaverse Cloud Engine SDK.
    /// </summary>
    public static class MetaverseConstants
    {
        /// <summary>
        /// The name of the SDK.
        /// </summary>
        public const string ProductName = "REACH Explorer";
        /// <summary>
        /// The path for deprecated SDK components.
        /// </summary>
        public const string DeprecatedComponent
#if UNITY_2022_2_OR_NEWER
            = ProductName + "/Deprecated/DEPRECATED COMPONENT";
#else
            = "";
#endif

        /// <summary>
        /// The resource paths for SDK assets.
        /// </summary>
        public static class Resources
        {
            /// <summary>
            /// The base path for SDK assets.
            /// </summary>
            public const string ResourcesBasePath = "Metaverse SDK/";
            
            /// <summary>
            /// The path for the default player prefab.
            /// </summary>
            public const string DefaultPlayer = ResourcesBasePath + "Default Player";
            
            /// <summary>
            /// The path for the default Meta Space prefab.
            /// </summary>
            public const string MetaSpace = ResourcesBasePath + "Meta Space";
            
            /// <summary>
            /// The path for the default Seat prefab.
            /// </summary>
            public const string Seat = ResourcesBasePath + "Seat";
        }

        /// <summary>
        /// Constants for unity menu item paths.
        /// </summary>
        public static class MenuItems
        {
            /// <summary>
            /// The root path for SDK menu items.
            /// </summary>
            public const string MenuRootPath = ProductName + "/";
            
            /// <summary>
            /// The root path for internal application menu items.
            /// </summary>
            public const string InternalMenuRootPath = MenuRootPath + "Internal/";
            
            /// <summary>
            /// The root path for SDK windows.
            /// </summary>
            public const string WindowsMenuRootPath = MenuRootPath + "Windows/";
            
            /// <summary>
            /// The root path for tools menu items.
            /// </summary>
            public const string ToolsMenuRootPath = MenuRootPath + "Tools/";
            
            /// <summary>
            /// The root path for GameObject menu items.
            /// </summary>
            public const string GameObjectMenuRootPath = "GameObject/" + ProductName + "/";
        }

        /// <summary>
        /// URLs used by the SDK.
        /// </summary>
        public static class Urls
        {
            /// <summary>
            /// The base URI Scheme for IPFS URIs.
            /// </summary>
            public const string IpfsUri = "ipfs://";
            
            /// <summary>
            /// The default IPFS Gateway to use when resolving IPFS URIs.
            /// </summary>
            public const string IpfsGateway = "https://ipfs.io/ipfs/";
            
            /// <summary>
            /// The base URI Scheme for native deep links (non-web). 
            /// </summary>
            public static string DeepLink = "reachdl://";
            
            /// <summary>
            /// The base URI Scheme for the launcher application.
            /// </summary>
            public static string LauncherDeepLink = "reachlauncherdl://";
            
            /// <summary>
            /// The domain name of the web application host.
            /// </summary>
            public static string WebGLHost = "app.reachcloud.org";
            
            /// <summary>
            /// The assumed web address for the Metaverse Cloud Engine web application.
            /// </summary>
            public static string WebGL => "https://" + WebGLHost + "/";

            /// <summary>
            /// The assumed web address for the Metaverse Cloud Engine AI resources endpoint.
            /// </summary>
            public const string ResourcesUrl = "https://resources.reachcloud.org";

            /// <summary>
            /// The assumed web address for the Metaverse Cloud Engine web application dashboard.
            /// </summary>
            public static string DashboardUrl = "https://dashboard.reachcloud.org/"; 

            /// <summary>
            /// The URI for the Metaverse Cloud Engine API.
            /// </summary>
            public static string ApiEndpoint = "https://api.reachcloud.org/api";
        }

        /// <summary>
        /// Default XR related constants.
        /// </summary>
        public static class XR
        {
            /// <summary>
            /// The Default XR resolution scale to use based on the current device.
            /// </summary>
            public static float DefaultXRResolutionScale => GetXRResolutionScaleBasedOnDevice();

            /// <summary>
            /// The Default XR resolution scale to use in a world based on the current device.
            /// </summary>
            public static float DefaultInWorldResolutionMultiplier => GetInWorldResolutionMultiplierBasedOnDevice();

            /// <summary>
            /// The Default XR anti-aliasing level to use.
            /// </summary>
            public const int DefaultAntiAliasing = 4;

            private static float GetInWorldResolutionMultiplierBasedOnDevice()
            {
                const float defaultXRResolutionScaleMul = 1f;
                var scale = defaultXRResolutionScaleMul;

#if !UNITY_IOS
                if (MVUtils.IsOculusPlatform())
                {
#if MV_OCULUS_PLUGIN
                    scale = Utils.GetSystemHeadsetType() switch
                    {
                        SystemHeadset.Oculus_Quest => 0.5f,
                        SystemHeadset.Oculus_Quest_2 => 0.5f,
                        _ => defaultXRResolutionScaleMul,
                    };
#else
                    scale = 0.5f;
#endif
                }
#endif

                return scale;
            }

            private static float GetXRResolutionScaleBasedOnDevice()
            {
                const float defaultXRResolutionScale = 1.25f;
                var scale = defaultXRResolutionScale;
#if !UNITY_IOS
                if (MVUtils.IsOculusPlatform())
                {
#if MV_OCULUS_PLUGIN
                    scale = Utils.GetSystemHeadsetType() switch
                    {
                        SystemHeadset.Oculus_Quest => 1.5f,
                        SystemHeadset.Oculus_Quest_2 => 1.5f,
                        _ => defaultXRResolutionScale,
                    };
#endif
                }
#endif
                return scale;
            }
        }

        /// <summary>
        /// Default identifiers used by the application during runtime.
        /// </summary>
        public static class Identifiers
        {
            /// <summary>
            /// The default organization ID to use when none is specified.
            /// </summary>
            public static Guid? DefaultOrganizationId = null;
            
            /// <summary>
            /// The default Meta Space ID to use when none is specified.
            /// </summary>
            public static Guid? DefaultMetaSpaceId = null;
        }

        /// <summary>
        /// Default physics-specific constants.
        /// </summary>
        public static class Physics
        {
            /// <summary>
            /// The default Fixed Update rate.
            /// </summary>
            public const float DefaultFixedDeltaTime =
#if UNITY_ANDROID && MV_OCULUS_PLUGIN 
                1f / 120f;
#else 
                1f / 72f;
#endif

            /// <summary>
            /// The default gravity to use.
            /// </summary>
            public static readonly Vector3 DefaultGravity = new(0, -9.81f, 0);
        }

        public class Sizes
        {
            public const int InstanceUidLength = 16;
        }
    }
}
