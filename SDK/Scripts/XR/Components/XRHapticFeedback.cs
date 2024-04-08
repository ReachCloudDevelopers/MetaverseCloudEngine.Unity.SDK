using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class XRHapticFeedback : MonoBehaviour
    {
        [Flags]
        public enum SupportedDeviceType
        {
            LeftController = 1,
            RightController = 2,
        }
        
        [Tooltip("The device type that this haptic feedback is supported on.")]
        [SerializeField] private SupportedDeviceType supportedDeviceType = 
            SupportedDeviceType.LeftController | 
            SupportedDeviceType.RightController;
        [Tooltip("The duration of the haptic feedback.")]
        [SerializeField] private float duration = 0.1f;
        [Tooltip("The amplitude of the haptic feedback.")]
        [SerializeField] private float amplitude = 0.5f;
        
        [Header("Callbacks")]
        [SerializeField] private UnityEvent onImpulseSent;
        [SerializeField] private UnityEvent onImpulseFailedToSend;
        [SerializeField] private UnityEvent onImpulseStopped;
        
        /// <summary>
        /// The duration of the haptic feedback.
        /// </summary>
        public float Duration
        {
            get => duration;
            set => duration = value;
        }
        
        /// <summary>
        /// The amplitude of the haptic feedback.
        /// </summary>
        public float Amplitude
        {
            get => amplitude;
            set => amplitude = value;
        }
        
        /// <summary>
        /// The device type that this haptic feedback is supported on. 1 = LeftController, 2 = RightController, 3 = Both.
        /// </summary>
        public int SupportedDeviceTypeValue
        {
            get => (int) supportedDeviceType;
            set => supportedDeviceType = (SupportedDeviceType) value;
        }

        public void SendImpulse()
        {
            SendFeedbackToDeviceType(SupportedDeviceType.LeftController);
            SendFeedbackToDeviceType(SupportedDeviceType.RightController);
        }

        public void SendImpulseToLeftController() => SendFeedbackToDeviceType(SupportedDeviceType.LeftController);

        public void SendImpulseToRightController() => SendFeedbackToDeviceType(SupportedDeviceType.RightController);
        
        public void StopImpulse()
        {
            StopFeedbackToDeviceType(SupportedDeviceType.LeftController);
            StopFeedbackToDeviceType(SupportedDeviceType.RightController);
        }
        
        public void StopImpulseToLeftController() => StopFeedbackToDeviceType(SupportedDeviceType.LeftController);
        
        public void StopImpulseToRightController() => StopFeedbackToDeviceType(SupportedDeviceType.RightController);
        
        private void StopFeedbackToDeviceType(SupportedDeviceType type)
        {
            if (!supportedDeviceType.HasFlag(type))
                return;
            
            var device = type == SupportedDeviceType.LeftController ? XRNode.LeftHand : XRNode.RightHand;
            var inputDevice = InputDevices.GetDeviceAtXRNode(device);
            if (!inputDevice.isValid)
                return;
            
            inputDevice.StopHaptics();
            onImpulseStopped?.Invoke();
        }

        private void SendFeedbackToDeviceType(SupportedDeviceType type)
        {
            if (!supportedDeviceType.HasFlag(type))
                return;
            
            var device = type == SupportedDeviceType.LeftController ? XRNode.LeftHand : XRNode.RightHand;
            var inputDevice = InputDevices.GetDeviceAtXRNode(device);
            if (!inputDevice.isValid)
                return;
            
            if (inputDevice.SendHapticImpulse(0, amplitude, duration))
                onImpulseSent?.Invoke();
            else
                onImpulseFailedToSend?.Invoke();
        }
    }
}