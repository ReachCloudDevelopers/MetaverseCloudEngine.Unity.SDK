#pragma warning disable CS0067

using System;
using MetaverseCloudEngine.Unity.Audio.Abstract;
using MetaverseCloudEngine.Unity.Video.Abstract;
using UnityEngine;
using UnityEngine.UI;
using NetworkObject = MetaverseCloudEngine.Unity.Networking.Components.NetworkObject;

namespace MetaverseCloudEngine.Unity.Audio.Poco
{
    public partial class InternalCommunicationsService : IMicrophoneService, IVideoCameraService
    {
        public InternalCommunicationsService()
        {
            CtorInternal();
        }
        
        public bool IsLocallyMuted {
            get {
                var value = false;
                GetVoiceMutedInternal(ref value);
                return value;
            }
            set => SetVoiceMutedInternal(value);
        }

        public string[] ConnectedAudioRecordingDevices {
            get {
                var value = Array.Empty<string>();
                GetConnectedRecordingDevicesInternal(ref value);
                return value;
            }
        }

        public bool IsPlatformSupported
        {
            get
            {
                var isSupported = false;
                IsPlatformSupportedInternal(ref isSupported);
                return isSupported;
            }
        }

        public bool IsLocalVideoDisabled {
            get {
                var value = false;
                GetVideoDisabledInternal(ref value);
                return value;
            }
            set => SetVideoDisabledInternal(value);
        }

        public string[] ConnectedVideoRecordingDevices {
            get {
                var value = Array.Empty<string>();
                GetConnectedVideoRecordingDevicesInternal(ref value);
                return value;
            }
        }

        public bool IsScreenShareMode {
            get {
                var value = false;
                GetIsScreenShareModeInternal(ref value);
                return value;
            }
            set => SetIsScreenShareModeInternal(value);
        }

        public bool BackgroundBlurEnabled {
            get {
                var value = false;
                GetEnableBackgroundBlurInternal(ref value);
                return value;
            }
            set => SetEnableBackgroundBlurInternal(value);
        }

        public event Action<bool> VoiceMuteChanged;
        public event Action<bool> VideoDisabledChanged;
        public event Action<bool> ScreenShareModeChanged;

        public void Dispose()
        {
            DisposeInternal();
        }

        public void Initialize()
        {
        }

        public void AddSource(RawImage output, NetworkObject networkObject)
        {
            AddSourceInternal(output, networkObject);
        }

        public void AddSource(Renderer output, NetworkObject networkObject)
        {
            AddSourceInternal(output, networkObject);
        }

        public bool IsVideoDisabled(NetworkObject networkObject)
        {
            var isDisabled = false;
            IsVideoDisabledInternal(networkObject, ref isDisabled);
            return isDisabled;
        }

        public bool SetActiveVideoRecordingDevice(string device)
        {
            var success = false;
            SetActiveVideoRecordingDeviceInternal(device, ref success);
            return success;
        }

        public string GetActiveVideoRecordingDevice()
        {
            var device = string.Empty;
            GetActiveVideoRecordingDeviceInternal(ref device);
            return device;
        }

        public void SetResolution(int width, int height)
        {
            SetResolutionInternal(width, height);
        }

        public void AddSource(AudioSource source, NetworkObject networkObject)
        {
            AddSourceInternal(source, networkObject);
        }

        public bool IsAudioSourceMuted(NetworkObject networkObject)
        {
            var isMuted = true;
            IsMutedInternal(networkObject, ref isMuted);
            return isMuted;
        }

        public bool IsUserMuted(int userId)
        {
            var isMuted = true;
            IsUserMutedInternal(userId, ref isMuted);
            return isMuted;
        }

        public bool IsUserSpeaking(int userId)
        {
            var isSpeaking = false;
            IsUserSpeakingInternal(userId, ref isSpeaking);
            return isSpeaking;
        }

        public bool SetActiveAudioRecordingDevice(string device)
        {
            var success = false;
            SetActiveRecordingDeviceInternal(device, ref success);
            return success;
        }

        public float CalculateMicrophoneCurrentAmplitude()
        {
            var amplitude = 0.0f;
            CalculateCurrentAmplitude(ref amplitude);
            return amplitude;
        }

        public void MuteUser(int userId, bool mute)
        {
            MuteUserInternal(userId, mute);
        }

        public string GetActiveAudioRecordingDevice()
        {
            string deviceName = null;
            GetActiveRecordingDeviceInternal(ref deviceName);
            return deviceName;
        }

        partial void IsPlatformSupportedInternal(ref bool isSupported);
        
        partial void IsUserSpeakingInternal(int userId, ref bool isSpeaking);
        
        partial void MuteUserInternal(int userId, bool mute);
        
        partial void IsUserMutedInternal(int userId, ref bool isMuted);

        partial void DisposeInternal();

        partial void CtorInternal();

        partial void CalculateCurrentAmplitude(ref float amplitude);

        partial void GetVoiceMutedInternal(ref bool value);

        partial void SetVoiceMutedInternal(bool value);

        partial void GetActiveRecordingDeviceInternal(ref string deviceName);

        partial void GetConnectedRecordingDevicesInternal(ref string[] value);

        partial void GetEnableBackgroundBlurInternal(ref bool value);

        partial void SetEnableBackgroundBlurInternal(bool value);

        partial void SetActiveRecordingDeviceInternal(string value, ref bool success);

        partial void IsVideoDisabledInternal(NetworkObject networkObject, ref bool isDisabled);

        partial void GetVideoDisabledInternal(ref bool isDisabled);

        partial void SetVideoDisabledInternal(bool value);

        partial void GetConnectedVideoRecordingDevicesInternal(ref string[] devices);

        partial void SetActiveVideoRecordingDeviceInternal(string device, ref bool success);

        partial void GetActiveVideoRecordingDeviceInternal(ref string device);

        partial void AddSourceInternal(Renderer output, NetworkObject networkObject);

        partial void AddSourceInternal(RawImage output, NetworkObject networkObject);

        partial void AddSourceInternal(AudioSource source, NetworkObject networkObject);

        partial void IsMutedInternal(NetworkObject networkObject, ref bool isMuted);

        partial void GetIsScreenShareModeInternal(ref bool value);

        partial void SetIsScreenShareModeInternal(bool value);
        
        partial void SetResolutionInternal(int width, int height);
    }
}
