using System;
using UnityEngine;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.XR
{
    /// <summary>
    /// A helper class that allows easy access to the information about the VR HMD.
    /// </summary>
    public static class XRInputTrackingAPI
    {
        /// <summary>
        /// The current HMD device info.
        /// </summary>
        public static InputDevice CurrentDevice;

        /// <summary>
        /// Invoked when the origin was centered on the headset.
        /// </summary>
        public static event Action<XRInputSubsystem> OriginCentered;

        /// <summary>
        /// Invoked when the HMD is connected to the device.
        /// </summary>
        public static event Action<InputDevice> HmdConnected;

        /// <summary>
        /// Invoked when the HMD is disconnected from the device.
        /// </summary>
        public static event Action<InputDevice> HmdDisconnected;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;

            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (hmd.isValid)
                OnDeviceConnected(hmd);

            QualitySettings.activeQualityLevelChanged += OnQualityLevelChanged;
            UpdateXRApplicationState();

#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED && MV_META_CORE
            OVRManager.HMDMounted += OnOvrManagerHmdMounted;
#endif
        }

#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED && MV_META_CORE
        private static void OnOvrManagerHmdMounted()
        {
            if (OVRManager.instance && OVRManager.display is not null)
            {
                OVRManager.display.RecenteredPose -= OnOvrManagerDisplayRecenter;
                OVRManager.display.RecenteredPose += OnOvrManagerDisplayRecenter;
            }
        }
        
        private static void OnOvrManagerDisplayRecenter() => OriginCentered?.Invoke(CurrentDevice.subsystem);
#endif

        private static void OnDeviceDisconnected(InputDevice device)
        {
            if (CurrentDevice != device) return;
            MetaverseProgram.Logger?.Log("[XRInputTrackingAPI] XRNode.CenterEye Disconnected");
            if (CurrentDevice.subsystem != null)
            {
                CurrentDevice.subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
                CurrentDevice.subsystem.boundaryChanged -= OnTrackingOriginUpdated;
            }
            HmdDisconnected?.Invoke(CurrentDevice);
        }

        private static void OnDeviceConnected(InputDevice device)
        {
            if (Application.isMobilePlatform && MVUtils.IsVRCompatible())
            {
                InputDevices.deviceConnected -= OnDeviceConnected;
                InputDevices.deviceDisconnected -= OnDeviceDisconnected;
            }

            CurrentDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (CurrentDevice != device) return;
            MetaverseProgram.Logger?.Log("[XRInputTrackingAPI] XRNode.CenterEye Connected");
            if (CurrentDevice.subsystem != null)
            {
                CurrentDevice.subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
                CurrentDevice.subsystem.boundaryChanged += OnTrackingOriginUpdated;
            }
            HmdConnected?.Invoke(CurrentDevice);
            UpdateXRApplicationState();
        }

        private static void OnTrackingOriginUpdated(XRInputSubsystem system)
        {
            OriginCentered?.Invoke(system);
        }

        /// <summary>
        /// Tells the connected input subsystem for the connected HMD to recenter it's origin.
        /// </summary>
        public static void CenterOrigin()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED && MV_META_CORE
            if (OVRManager.instance && OVRManager.display is not null)
                OVRManager.display.RecenterPose();
#endif
        }

        private static void OnQualityLevelChanged(int oldLevel, int newLevel)
        {
            if (CurrentDevice.isValid)
                UpdateXRApplicationState();
        }

        private static void UpdateXRApplicationState()
        {
            if (!MVUtils.IsVRCompatible())
                return;

            QualitySettings.vSyncCount = 0;
        }
    }
}