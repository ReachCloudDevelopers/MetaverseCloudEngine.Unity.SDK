// ReSharper disable InconsistentNaming

#if MV_UNITY_AR_FOUNDATION
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [DisallowMultipleComponent]
    [HideMonoScript]
    [DefaultExecutionOrder(-int.MaxValue + 1)]
    [AddComponentMenu(MetaverseConstants.ProductName + "/XR/AR Session Bridge")]
    public class MetaverseARSessionBridge : TriInspectorMonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        private bool _initialAttemptUpdate = true;
     
        /// <summary>
        /// Whether to check for AR updates on the device and perform automatic installation if necessary.
        /// </summary>
        [ShowInInspector]
        public bool attemptUpdate
        {
            get => !Application.isPlaying ? _initialAttemptUpdate : MetaverseARSessionInstance.ARSession.attemptUpdate;
            set
            {
                if (!Application.isPlaying)
                {
                    _initialAttemptUpdate = value;
                    return;
                }
                MetaverseARSessionInstance.ARSession.attemptUpdate = value;
            }
        }
        
        [SerializeField]
        [HideInInspector]
        private bool _initialMatchFrameRate = true;
        
        /// <summary>
        /// Whether the <see cref="ARSession"/> should match the frame rate of the device.
        /// </summary>
        [ShowInInspector]
        public bool matchFrameRate
        {
            get => !Application.isPlaying ? _initialMatchFrameRate : MetaverseARSessionInstance.ARSession.matchFrameRateRequested;
            set
            {
                if (!Application.isPlaying)
                {
                    _initialMatchFrameRate = value;
                    return;
                }
                MetaverseARSessionInstance.ARSession.matchFrameRateRequested = value;
            }
        }

        [SerializeField]
        [HideInInspector]
        private TrackingMode _initialRequestedTrackingMode = TrackingMode.PositionAndRotation;
        
        /// <summary>
        /// The requested tracking mode for the <see cref="ARSession"/>.
        /// </summary>
        [ShowInInspector]
        public TrackingMode requestedTrackingMode
        {
            get => !Application.isPlaying ? _initialRequestedTrackingMode : MetaverseARSessionInstance.ARSession.requestedTrackingMode;
            set
            {
                if (!Application.isPlaying)
                {
                    _initialRequestedTrackingMode = value;
                    return;
                }
                MetaverseARSessionInstance.ARSession.requestedTrackingMode = value;
            }
        }
        
        [InfoBox("This event is called on Awake before any scripts are initialized.")]
        [SerializeField] private UnityEvent<ARSession> awake = new();

        /// <summary>
        /// This event is called on Awake before any scripts are initialized.
        /// </summary>
        public UnityEvent<ARSession> OnAwake => awake;

#if MV_AR_CORE_EXTENSIONS
        [SerializeField]
        [InfoBox("To supply the ARSession to ARCoreExtensions, assign the ARCoreExtensions component to this field.")]
        private Google.XR.ARCoreExtensions.ARCoreExtensions _arCoreExtensions;
#endif

        public XRSessionSubsystem subsystem => MetaverseARSessionInstance.ARSession.subsystem;
        public new bool enabled => MetaverseARSessionInstance.ARSession.enabled;
        public static TrackingState trackingState => MetaverseARSessionInstance.ARSession.subsystem.trackingState;
        public static NotTrackingReason notTrackingReason => MetaverseARSessionInstance.ARSession.subsystem.notTrackingReason;

        private void Awake()
        {
            MetaverseARSessionInstance.ARSession.attemptUpdate = _initialAttemptUpdate;
            MetaverseARSessionInstance.ARSession.matchFrameRateRequested = _initialMatchFrameRate;
            MetaverseARSessionInstance.ARSession.requestedTrackingMode = _initialRequestedTrackingMode;
#if MV_AR_CORE_EXTENSIONS
            if (_arCoreExtensions) _arCoreExtensions.Session = MetaverseARSessionInstance.ARSession;
#endif
            awake?.Invoke(MetaverseARSessionInstance.ARSession);
        }

        /// <summary>
        /// Calls <see cref="UnityEngine.XR.ARFoundation.ARSession.Reset"/> on the <see cref="ARSession"/>.
        /// </summary>
        public void Reset()
        {
            if (!Application.isPlaying) return;
            MetaverseARSessionInstance.Reset();
        }
    }
}
#endif
