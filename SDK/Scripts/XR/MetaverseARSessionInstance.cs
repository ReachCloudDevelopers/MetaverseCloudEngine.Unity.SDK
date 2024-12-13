#if MV_UNITY_AR_FOUNDATION
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MetaverseCloudEngine.Unity.XR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ARSession))]
    [DefaultExecutionOrder(-int.MaxValue)]
    [AddComponentMenu("")]
    [HideMonoScript]
    internal sealed class MetaverseARSessionInstance : TriInspectorMonoBehaviour
    {
        private ARSession _arSession;
        private static bool _resetRequested;
        private static bool _isResetInProgress;
        
        private static MetaverseARSessionInstance _instance;
        private static MetaverseARSessionInstance Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindObjectOfType<MetaverseARSessionInstance>(true);
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;
                var go = new GameObject(nameof(MetaverseARSessionInstance));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MetaverseARSessionInstance>();
                return _instance;
            }
        }
        
        public static ARSession ARSession => Instance && Instance._arSession ? Instance._arSession : null;
        
        private void Awake() => _arSession = GetComponent<ARSession>();

        private void LateUpdate() => DequeueReset();

        // ReSharper disable once Unity.IncorrectMethodSignature

        /// <summary>
        /// Resets the <see cref="ARSession"/>.
        /// </summary>
        public static void Reset()
        {
            if (!Application.isPlaying) return;
            _ = Instance;
            _resetRequested = true;
        }

        /// <summary>
        /// Checks if the <see cref="MetaverseARSessionInstance"/> exists.
        /// </summary>
        /// <returns></returns>
        public static bool Exists() => _instance != null;

        private static void CleanUp()
        {
            var anchorManager = FindObjectOfType<ARAnchorManager>(true);
            if (!anchorManager) return;
            // ReSharper disable once IdentifierTypo
            var anchorManagerTrackables = anchorManager.trackables;
            var list = new List<ARAnchor>();
            foreach (var anchor in anchorManagerTrackables)
                if (anchor) list.Add(anchor);
            foreach (var anchor in list.Where(anchor => anchor)) 
                try { Destroy(anchor.gameObject); } catch { /* ignored */ }
        }

        private void DequeueReset()
        {
            if (!_resetRequested || _isResetInProgress ||
                ARSession.state != ARSessionState.SessionTracking ||
                ARSession.notTrackingReason != NotTrackingReason.None) return;
            _resetRequested = false;
            _isResetInProgress = true;
            CleanUp();
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                _arSession.Reset();
                MetaverseDispatcher.WaitForSeconds(0.1f, () =>
                {
                    _isResetInProgress = false;
                });
            });
        }
    }
}
#endif
