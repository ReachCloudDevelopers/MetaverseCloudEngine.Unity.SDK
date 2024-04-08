﻿#if UNITY_POST_PROCESSING_STACK_V2
using System;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [HideMonoScript]
    [RequireComponent(typeof(PostProcessLayer))]
    [DefaultExecutionOrder(-int.MaxValue)]
    public class PlatformDependantPostProcessLayerSettings : TriInspectorMonoBehaviour
    {
        [Flags]
        public enum NativePlatform
        {
            UNITY_STANDALONE = 1,
            UNITY_ANDROID = 2,
            UNITY_IOS = 4,
            UNITY_WEBGL = 8,
            MOBILE_VR = 16,
        }
        
        [Serializable]
        public class PlatformSettings
        {
            public NativePlatform platforms = (NativePlatform)~0;
            public PlatformSettingsOptions options = new();
            
            public void Apply(PostProcessLayer layer, PostProcessVolume volume = null)
            {
                options.Apply(layer, volume);
            }
        }

        [Serializable]
        public class PlatformSettingsOptions
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private PostProcessLayer.Antialiasing antialiasing = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
            [SerializeField] private PostProcessProfile profile;

            public void Apply(PostProcessLayer layer, PostProcessVolume volume = null)
            {
                layer.enabled = enabled;
                layer.antialiasingMode = layer.enabled ? antialiasing : PostProcessLayer.Antialiasing.None;
                if (volume)
                    volume.sharedProfile = profile ? profile : null;
            }
        }

        [InfoBox("This component is only compatible with the Post Processing Stack v2. It also only applies at " +
                 "build time.")]
        public PlatformSettingsOptions defaultSettings;
        public PlatformSettings[] settings;
        public PostProcessVolume volume;

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        /// <summary>
        /// Apply the settings to the PostProcessLayer.
        /// </summary>
        public void Apply()
        {
            var layer = GetComponent<PostProcessLayer>();
            if (!layer)
                return;
            
            Resources.FindObjectsOfTypeAll<PostProcessResources>()
                .FirstOrDefault()
                .IfNotNull(layer.Init);

            layer.InitBundles();
            
            var currentPlatform = 
#if UNITY_STANDALONE
                NativePlatform.UNITY_STANDALONE
#elif UNITY_ANDROID
                NativePlatform.UNITY_ANDROID
#elif UNITY_IOS
                NativePlatform.UNITY_IOS
#elif UNITY_WEBGL
                NativePlatform.UNITY_WEBGL
#endif
                ;

            if (MVUtils.IsMobileVR())
                currentPlatform |= NativePlatform.MOBILE_VR;
            
            foreach (var setting in settings)
            {
                if (((int)setting.platforms & (int)currentPlatform) == 0)
                    continue;
                setting.Apply(layer, volume);
                return;
            }
            
            defaultSettings.Apply(layer, volume);
        }
    }
}

#endif