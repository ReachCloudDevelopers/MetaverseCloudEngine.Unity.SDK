using System;
using MetaverseCloudEngine.Unity.Services.Abstract;
using UnityEngine;
using NetworkObject = MetaverseCloudEngine.Unity.Networking.Components.NetworkObject;

namespace MetaverseCloudEngine.Unity.Audio.Abstract
{

    public interface IMicrophoneService : IMetaSpaceService
    {
        event Action<bool> VoiceMuteChanged;

        bool IsLocallyMuted { get; set; }
        string[] ConnectedAudioRecordingDevices { get; }
        bool IsPlatformSupported { get; }

        void AddSource(AudioSource source, NetworkObject networkObject);
        bool IsAudioSourceMuted(NetworkObject networkObject);

        bool IsUserMuted(int userId);
        bool IsUserSpeaking(int userId);
        bool SetActiveAudioRecordingDevice(string device);
        string GetActiveAudioRecordingDevice();
        float CalculateMicrophoneCurrentAmplitude();
        void MuteUser(int userId, bool mute);
    }
}
