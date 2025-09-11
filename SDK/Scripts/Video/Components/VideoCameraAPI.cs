using System.Linq;
using System.Text.RegularExpressions;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Video.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Video.Components
{
    public class VideoCameraAPI : MetaSpaceBehaviour
    {
        public VideoCameraEvents events;

        private bool _initialized;
        private static WebCamTexture _offlineTexture;

        private IVideoCameraService _videoService;

        public IVideoCameraService VideoService
        {
            get
            {
                if (_videoService == null)
                {
                    _videoService = MetaSpace ? MetaSpace.GetService<IVideoCameraService>() : null;
                    InitializeVidService(_videoService);
                }

                return _videoService;
            }
        }

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            InitializeVidService(VideoService);
        }

        private void InitializeVidService(IVideoCameraService microphoneService)
        {
            if (_initialized)
                return;

            if (microphoneService != null)
            {
                _initialized = true;
                microphoneService.VideoDisabledChanged += OnVideoDisabledChanged;
                microphoneService.ScreenShareModeChanged += OnVideoScreenShareChanged;
                OnVideoDisabledChanged(microphoneService.IsLocalVideoDisabled);
            }
            else
                MetaverseProgram.Logger.LogWarning("Video Service not found in scene.");
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;

            base.OnDestroy();

            if (_videoService != null)
            {
                _videoService.VideoDisabledChanged -= OnVideoDisabledChanged;
                _videoService.ScreenShareModeChanged -= OnVideoScreenShareChanged;
            }
        }

        private void OnVideoScreenShareChanged(bool value)
        {
            if (value) events?.onScreenShareActive?.Invoke();
            else events?.onScreenShareDisabled?.Invoke();
        }

        private void OnVideoDisabledChanged(bool value)
        {
            if (value) events?.onVideoDisabled?.Invoke();
            else events?.onVideoActive?.Invoke();
        }

        public void SetBackgroundBlur(bool value)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Cannot start/stop screen share because service does not exist in the scene.");
                    return;
                }

                VideoService.BackgroundBlurEnabled = value;
            });
        }

        public void SetScreenShare(bool value)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Cannot start/stop screen share because service does not exist in the scene.");
                    return;
                }

                VideoService.IsScreenShareMode = value;
            });
        }

        public void SetVideoDisabled(bool value)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    if (!value)
                    {
                        if (_offlineTexture == null)
                            _offlineTexture = new WebCamTexture();
                        _offlineTexture.Play();
                        events?.onVideoActive?.Invoke();
                    }
                    else
                    {
                        if (_offlineTexture != null)
                        {
                            _offlineTexture.Stop();
                            _offlineTexture = null;
                        }

                        events?.onVideoDisabled?.Invoke();
                    }

                    return;
                }

                VideoService.IsLocalVideoDisabled = value;
            });
        }

        public void SetVideoEnabled(bool value)
        {
            SetVideoDisabled(!value);
        }

        public void SetResolution(string res)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Cannot set video resolution because service does not exist in the scene.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(res))
                {
                    MetaverseProgram.Logger.LogWarning("Invalid resolution format. Expected format: 1920x1080");
                    return;
                }

                var xy = res.Trim().Split("x");
                if (xy.Length != 2)
                {
                    MetaverseProgram.Logger.LogWarning("Invalid resolution format. Expected format: 1920x1080");
                    return;
                }

                if (!int.TryParse(xy[0].Trim(), out var x) || !int.TryParse(xy[1].Trim(), out var y))
                {
                    MetaverseProgram.Logger.LogWarning("Invalid resolution format. Expected format: 1920x1080");
                    return;
                }

                VideoService.SetResolution(x, y);
            });
        }

        public void SetActiveVideoRecordingDevice(string device)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Cannot set active video recording device because service does not exist in the scene.");
                    return;
                }

                VideoService.SetActiveVideoRecordingDevice(device);
            });
        }
        
        public void SetActiveVideoRecordingDeviceRegex(string pattern)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Cannot set active video recording device because service does not exist in the scene.");
                    return;
                }

                var devices = VideoService.ConnectedVideoRecordingDevices;
                var device = devices.FirstOrDefault(d => Regex.IsMatch(d, pattern.Replace("\\_", "_")));
                if (device != null)
                    VideoService.SetActiveVideoRecordingDevice(device);
            });
        }

        public void SetActiveVideoRecordingDevice(int index)
        {
            MetaSpace.OnReady(() =>
            {
                if (VideoService == null)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Cannot set active video recording device because service does not exist in the scene.");
                    return;
                }

                var devices = VideoService.ConnectedVideoRecordingDevices.ToArray();
                if (devices.Length == 0)
                {
                    MetaverseProgram.Logger.LogWarning("No video recording devices found.");
                    return;
                }
                
                if (index < 0)
                    index = 0;
                if (index >= devices.Length)
                    index = devices.Length - 1;
                VideoService.SetActiveVideoRecordingDevice(devices[index]);
            });
        }
    }
}
