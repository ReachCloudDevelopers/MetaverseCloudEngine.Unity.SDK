using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// A component that provides functions for re-centering the current XR input device.
    /// </summary>
    [HideMonoScript]
    public class XRRecenterAPI : TriInspectorMonoBehaviour
    {
        [Tooltip("Called when the XR input device is re-centered.")]
        public UnityEvent onCentered;

        private void OnEnable()
        {
            XRInputTrackingAPI.OriginCentered += OnOriginCentered;
        }

        private void OnDisable()
        {
            XRInputTrackingAPI.OriginCentered -= OnOriginCentered;
        }

        /// <summary>
        /// Tells the input subsystem of the connected device to recenter itself.
        /// </summary>
        public void CenterOrigin()
        {
            XRInputTrackingAPI.CenterOrigin();
        }

        private void OnOriginCentered(XRInputSubsystem system)
        {
            onCentered?.Invoke();
        }
    }
}
