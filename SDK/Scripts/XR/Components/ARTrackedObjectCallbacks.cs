#if MV_UNITY_AR_FOUNDATION
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class ARTrackedObjectCallbacks : MonoBehaviour
    {
        public string targetObjectName;
        public ARTrackedObjectManager manager;
        
        [Header("Events")]
        public UnityEvent<ARTrackedObject> onTracking;
        public UnityEvent<ARTrackedObject> onNotTracking;

        private void Awake()
        {
            if (manager) manager.trackedObjectsChanged += OnTrackedObjectsChanged;
        }

        private void Reset()
        {
            manager = GetComponentInParent<ARTrackedObjectManager>();
        }

        private void OnDestroy()
        {
            if (manager) manager.trackedObjectsChanged -= OnTrackedObjectsChanged;
        }

        private void OnTrackedObjectsChanged(ARTrackedObjectsChangedEventArgs args)
        {
            ARTrackedObject added = args.added.FirstOrDefault(x => x.referenceObject.name == targetObjectName);
            if (added != null)
            {
                onTracking?.Invoke(added);
            }
            else
            {
                ARTrackedObject removed = args.removed.FirstOrDefault(x => x.referenceObject.name == targetObjectName);
                if (removed) onNotTracking?.Invoke(removed);
            }
        }
    }
}
#endif