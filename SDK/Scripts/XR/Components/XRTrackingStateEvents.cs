using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// A helper class that allows easy access to the information about the tracking
    /// state of an input device.
    /// </summary>
    public class XRTrackingStateEvents : MonoBehaviour
    {
        public InputActionProperty trackingStateAction;
        public InputTrackingState requiredFlags = InputTrackingState.Position | InputTrackingState.Rotation;

        public UnityEvent onTracked;
        public UnityEvent onNotTracked;
        public UnityEvent<bool> onTrackedValue;
        
        private InputTrackingState _lastTrackingState;
        
        private void OnEnable()
        {
            if (!trackingStateAction.reference)
                trackingStateAction.action.Enable();
        }

        private void OnDisable()
        {
            if (!trackingStateAction.reference)
                trackingStateAction.action.Disable();
        }

        private void Update()
        {
            var state = trackingStateAction.action.ReadValue<int>();
            InputTrackingState trackingState = (InputTrackingState) state;
            if (trackingState == _lastTrackingState) 
                return;
            
            _lastTrackingState = trackingState;

            if (trackingState.HasFlag(requiredFlags))
            {
                onTracked?.Invoke();
                onTrackedValue?.Invoke(true);
            }
            else
            {
                onNotTracked?.Invoke();
                onTrackedValue?.Invoke(false);
            }
        }
    }
}
