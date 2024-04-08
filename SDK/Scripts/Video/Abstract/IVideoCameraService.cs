using System;
using MetaverseCloudEngine.Unity.Services.Abstract;
using UnityEngine;
using UnityEngine.UI;
using NetworkObject = MetaverseCloudEngine.Unity.Networking.Components.NetworkObject;

namespace MetaverseCloudEngine.Unity.Video.Abstract
{
    public interface IVideoCameraService : IMetaSpaceService
    {
        event Action<bool> VideoDisabledChanged;
        event Action<bool> ScreenShareModeChanged;

        bool IsLocalVideoDisabled { get; set; }
        bool IsScreenShareMode { get; set; }
        bool BackgroundBlurEnabled { get; set; }
        string[] ConnectedVideoRecordingDevices { get; }
        bool IsPlatformSupported { get; }

        void AddSource(RawImage output, NetworkObject networkObject);
        void AddSource(Renderer output, NetworkObject networkObject);
        bool IsVideoDisabled(NetworkObject networkObject);

        bool SetActiveVideoRecordingDevice(string device);
        string GetActiveVideoRecordingDevice();
        void SetResolution(int width, int height);
    }
}
