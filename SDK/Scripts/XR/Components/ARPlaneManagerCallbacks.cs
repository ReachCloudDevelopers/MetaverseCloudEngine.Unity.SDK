#if MV_UNITY_AR_FOUNDATION
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class ARPlaneManagerCallbacks : MonoBehaviour
    {
        public ARPlaneManager manager;

        [Header("Events")]
        public UnityEvent<ARPlane> onTracking;
        public UnityEvent<ARPlane> onNotTracking;

        public UnityEvent onTrackingAny;
        public UnityEvent onNotTrackingAny;

        private void Reset()
        {
            manager = GetComponentInParent<ARPlaneManager>(true);
        }

        private void Awake()
        {
            if (manager) manager.planesChanged += OnPlanesChanged;
        }

        private void OnDestroy()
        {
            if (manager) manager.planesChanged -= OnPlanesChanged;
        }

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var arg in args.added)
                onTracking?.Invoke(arg);
            
            foreach (var arg in args.removed)
                onNotTracking?.Invoke(arg);

            if (manager.trackables.count > 0)
                onTrackingAny?.Invoke();
            else onNotTrackingAny?.Invoke();
        }
    }
}
#endif