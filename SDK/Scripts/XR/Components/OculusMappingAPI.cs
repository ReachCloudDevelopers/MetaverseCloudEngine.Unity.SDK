using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class OculusMappingAPI : TriInspectorMonoBehaviour
    {
        public bool IsSupported
        {
            get
            {
#if MV_META_CORE
                return true;
#else 
                return false;
#endif
            }
        }

        [Tooltip("This event is invoked when the Oculus device is recentered.")]
        public UnityEvent onRecenter = new();
        
#if MV_META_CORE
        private readonly List<OVRSpatialAnchor.UnboundAnchor> _unboundAnchors = new();

        /// <summary>
        /// Checks if the Oculus device is currently tracking.
        /// </summary>
        public bool IsTracking => OVRManager.isHmdPresent && OVRManager.hasVrFocus;
        
        /// <summary>
        /// Checks if the Oculus boundary system is configured.
        /// </summary>
        public bool IsBoundaryConfigured => OVRManager.boundary.GetConfigured();

        private void Start()
        {
            if (OVRManager.display != null)
                OVRManager.display.RecenteredPose += OnRecenterPose;
        }

        private void OnDestroy()
        {
            if (OVRManager.display != null)
                OVRManager.display.RecenteredPose -= OnRecenterPose;
        }

        private void OnRecenterPose()
        {
            if (!this) return;
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this) return;
                onRecenter?.Invoke();
            });
        }

        /// <summary>
        /// Loads a map of anchors from the Oculus system.
        /// </summary>
        /// <param name="anchorIds">The IDs of the anchors to load.</param>
        /// <param name="onAnchorReady">This callback is invoked when the anchors are ready and localized.</param>
        /// <param name="onError">Optional callback invoked if an error occurs during the loading process.</param>
        public void LoadMapAsync(
            string[] anchorIds,
            Action<OVRSpatialAnchor> onAnchorReady, 
            Action<object> onError = null)
        {
            var anchorGuids = new List<Guid>();
            foreach (var anchorId in anchorIds)
            {
                if (Guid.TryParse(anchorId, out var guid))
                    anchorGuids.Add(guid);
            }
            
            if (!this) return;
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this) return;
                UniTask.Void(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    var anchors = 
                        await OVRSpatialAnchor.LoadUnboundAnchorsAsync(anchorGuids, _unboundAnchors);
                    
                    if (!this) return;
                    if (!anchors.Success)
                    {
                        onError?.Invoke($"Failed to load anchors: {anchors.Status}");
                        return;
                    }
                    
                    foreach (var unboundAnchor in anchors.Value)
                    {
                        unboundAnchor.LocalizeAsync().ContinueWith(result =>
                        {
                            if (!result) return;
                            MetaverseDispatcher.AtEndOfFrame(() =>
                            {
                                if (!this) return;
                                var newAnchor = new GameObject($"{unboundAnchor.Uuid}").AddComponent<OVRSpatialAnchor>();
                                unboundAnchor.BindTo(newAnchor);
                                onAnchorReady?.Invoke(newAnchor);
                            });
                        });
                    }
                });
            });
        }
        
        /// <summary>
        /// Checks if the Oculus boundary system is currently visible.
        /// </summary>
        /// <param name="obj">The GameObject to create as an anchor.</param>
        /// <param name="onAnchorReady">Invoked when the anchor is ready and localized.</param>
        /// <param name="onError"></param>
        public void SaveAnchorAsync(
            GameObject obj, 
            Action<OVRSpatialAnchor> onAnchorReady,
            Action<object> onError = null)
        {
            if (!this)
                return;
            
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this)
                    return;

                var spatialAnchor = obj.GetOrAddComponent<OVRSpatialAnchor>();
                spatialAnchor.WhenLocalizedAsync().ContinueWith(result =>
                {
                    MetaverseDispatcher.AtEndOfFrame(() =>
                    {
                        if (!this) return;
                        if (!result)
                        {
                            onError?.Invoke("Failed to localize the spatial anchor.");
                            return;
                        }

                        spatialAnchor.SaveAnchorAsync().ContinueWith(ovrResult =>
                        {
                            MetaverseDispatcher.AtEndOfFrame(() =>
                            {
                                if (!this) return;
                                if (!ovrResult.Success)
                                {
                                    onError?.Invoke($"Failed to save the anchor: {ovrResult.Status}");
                                    return;
                                }
                                onAnchorReady?.Invoke(spatialAnchor);
                            });
                        });
                    });
                });
            });
        }
        
        public void ForceRecenter()
        {
            if (!this) return;
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this) return;
                OVRManager.display.RecenterPose();
            });
        }
#endif
    }
}