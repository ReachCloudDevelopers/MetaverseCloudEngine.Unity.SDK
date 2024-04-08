using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// An abstraction of a XR tracker. This is used to allow for different XR SDKs to be used. Example: Vive Tracker,
    /// Google MediaPipe, etc.
    /// </summary>
    [DisallowMultipleComponent]
    public partial class MetaverseXRTracker : MonoBehaviour
    {
        [Tooltip("The origin point. This is not the tracking space, this is the point relative to the pelvis.")]
        [SerializeField] private Transform origin;
        [Tooltip("(Optional) The target transform to apply the tracking data to.")]
        [SerializeField] private Transform customTarget;
        [Tooltip("The type of tracker this is.")]
        [SerializeField] private MetaverseXRTrackerType trackerType;
        [Tooltip("In the case of pose detection systems, this is the index of the human to track. This may not " +
                 "apply to all implementations.")]
        [SerializeField, Min(0)] private int humanIndex;
        [Tooltip("If true, the tracker will track the position of the target.")]
        [SerializeField] private bool trackPosition = true;
        [Tooltip("If true, the tracker will track the rotation of the target.")]
        [SerializeField] private bool trackRotation = true;
        
        [Header("Tracking Events")]
        [Tooltip("Invoked when the confidence of the tracker changes. Note that not all trackers will output confidence values.")]
        [SerializeField] private UnityEvent<float> onConfidenceChanged;
        [Tooltip("Invoked when the tracker is found.")]
        [SerializeField] private UnityEvent onTrackingFound;
        [Tooltip("Invoked when the tracker is lost.")]
        [SerializeField] private UnityEvent onTrackingLost;

        public MetaverseXRTrackerType Type => trackerType;

        private void Awake()
        {
            onTrackingLost?.Invoke();
            onConfidenceChanged?.Invoke(0);
            
            if (!customTarget)
                customTarget = transform;
        }
    }
}