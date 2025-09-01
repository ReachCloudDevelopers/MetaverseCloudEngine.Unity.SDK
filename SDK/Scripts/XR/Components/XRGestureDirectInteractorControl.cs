#if MV_XR_HANDS && MV_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.InputSystem.Layouts;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// An on-screen control that sets the value of a button control to 1 when a specified hand gesture is detected,
    /// and 0 otherwise. The gesture detection is performed at a specified interval using joint data
    /// provided by a specified XRHandTrackingEvents component.
    /// </summary>
    public class XRGestureDirectInteractorControl : OnScreenControl
    {
        [SerializeField]
        [Tooltip("The gesture to detect.")]
        private XRHandPose gesture;
        [SerializeField]
        [Tooltip(
            "The hand tracking events component to subscribe to receive updated joint data to be used for gesture detection.")]
        private XRHandTrackingEvents handTrackingEvents;
        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("The interval at which the gesture detection is performed.")]
        private float updateInterval = 0.1f;
        [InputControl(layout = "Button")]
        [SerializeField]
        [Tooltip("The control path of the button to set the value of when the gesture is detected.")]
        private string outputButton;

        private bool _isGestureActive;
        private float _nextUpdateTime;

        protected override string controlPathInternal
        {
            get => outputButton;
            set => outputButton = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (handTrackingEvents)
            {
                handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
                handTrackingEvents.trackingChanged.AddListener(OnTrackingChanged);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (handTrackingEvents)
            {
                handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
                handTrackingEvents.trackingChanged.RemoveListener(OnTrackingChanged);
            }

            if (_isGestureActive)
            {
                SendValueToControl(0f);
                _isGestureActive = false;
            }
        }

        private void OnJointsUpdated(XRHandJointsUpdatedEventArgs e)
        {
            if (!gesture) return;
            if (Time.unscaledTime < _nextUpdateTime) return;
            var isGestureActive = gesture.CheckConditions(e);
            _isGestureActive = isGestureActive;
            _nextUpdateTime = Time.unscaledTime + updateInterval;
        }

        private void OnTrackingChanged(bool tracking)
        {
            if (!tracking && _isGestureActive)
            {
                SendValueToControl(0f);
                _isGestureActive = false;
            }
        }

        private void LateUpdate()
        {
            SendValueToControl(_isGestureActive ? 1f : 0f);
        }
    }
}
#endif