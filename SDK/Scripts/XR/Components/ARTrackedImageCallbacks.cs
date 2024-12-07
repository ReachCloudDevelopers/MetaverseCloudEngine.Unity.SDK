#if MV_UNITY_AR_FOUNDATION
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class ARTrackedImageCallbacks : MonoBehaviour
    {
        public string targetImageName;
        public ARTrackedImageManager manager;
        
        [Header("Events")]
        public UnityEvent<ARTrackedImage> onTracking;
        public UnityEvent<ARTrackedImage> onNotTracking;

        private void Reset()
        {
            manager = GetComponentInParent<ARTrackedImageManager>(true);
        }

        private void Awake()
        {
            if (manager) manager.trackedImagesChanged += OnTrackedImagesChanged;
        }

        private void OnDestroy()
        {
            if (manager) manager.trackedImagesChanged -= OnTrackedImagesChanged;
        }

        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
        {
            ARTrackedImage added = args.added.FirstOrDefault(x => x.referenceImage.name == targetImageName);
            if (added != null)
            {
                onTracking?.Invoke(added);
            }
            else
            {
                ARTrackedImage removed = args.removed.FirstOrDefault(x => x.referenceImage.name == targetImageName);
                if (removed) onNotTracking?.Invoke(removed);
            }
        }
    }
}
#endif