#if !METAVERSE_CLOUD_ENGINE_INTERNAL
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NetworkObject = MetaverseCloudEngine.Unity.Networking.Components.NetworkObject;

namespace MetaverseCloudEngine.Unity.Audio.Poco
{
    public partial class InternalCommunicationsService
    {
        // Flip constants
        private const bool INVERT_X = false; // set true to mirror horizontally
        private const bool INVERT_Y = true; // set true to mirror vertically

        // --- Video state ---
        private WebCamTexture _webCamTexture;
        private string _activeVideoDevice;
        private int _videoWidth = 640;
        private int _videoHeight = 480;
        private bool _videoDisabled;
        private bool _screenShareMode;
        private bool _backgroundBlurEnabled;

        // Outputs to update when texture or state changes
        private readonly List<Renderer> _videoRenderers = new();
        private readonly List<RawImage> _videoRawImages = new();

        // --- Audio state (minimal offline stubs) ---
        private bool _voiceMuted;
        private string _activeMicDevice;

        partial void CtorInternal()
        {
            // Initialize defaults. Nothing else to do here for offline.
        }

        partial void DisposeInternal()
        {
            StopAndDisposeWebcam();
            _videoRenderers.Clear();
            _videoRawImages.Clear();
        }

        // --- Platform ---
        partial void IsPlatformSupportedInternal(ref bool isSupported)
        {
            // Offline implementation works wherever WebCamTexture is available.
            isSupported = true;
        }

        // --- Video enable/disable ---
        partial void GetVideoDisabledInternal(ref bool isDisabled)
        {
            isDisabled = _videoDisabled;
        }

        partial void SetVideoDisabledInternal(bool value)
        {
            if (_videoDisabled == value) return;
            _videoDisabled = value;

            if (_videoDisabled)
            {
                StopAndDisposeWebcam();
            }
            else
            {
                EnsureWebcamCreated();
                if (_webCamTexture != null && !_webCamTexture.isPlaying)
                    _webCamTexture.Play();
            }

            ApplyTextureToOutputs();
            VideoDisabledChanged?.Invoke(_videoDisabled);
        }

        partial void IsVideoDisabledInternal(NetworkObject networkObject, ref bool isDisabled)
        {
            // Single local flag for now; per-object logic could be added later.
            isDisabled = _videoDisabled;
        }

        // --- Screen share ---
        partial void GetIsScreenShareModeInternal(ref bool value)
        {
            value = _screenShareMode;
        }

        partial void SetIsScreenShareModeInternal(bool value)
        {
            if (_screenShareMode == value) return;
            _screenShareMode = value;
            ScreenShareModeChanged?.Invoke(_screenShareMode);
        }

        // --- Background blur (no-op offline) ---
        partial void GetEnableBackgroundBlurInternal(ref bool value)
        {
            value = _backgroundBlurEnabled;
        }

        partial void SetEnableBackgroundBlurInternal(bool value)
        {
            _backgroundBlurEnabled = value;
        }

        // --- Resolution ---
        partial void SetResolutionInternal(int width, int height)
        {
            if (width > 0) _videoWidth = width;
            if (height > 0) _videoHeight = height;
            RecreateWebcamIfNeeded();
        }

        // --- Devices ---
        partial void GetConnectedVideoRecordingDevicesInternal(ref string[] devices)
        {
            devices = WebCamTexture.devices?.Select(d => d.name).ToArray() ?? Array.Empty<string>();
        }

        partial void GetActiveVideoRecordingDeviceInternal(ref string device)
        {
            device = _activeVideoDevice;
        }

        partial void SetActiveVideoRecordingDeviceInternal(string device, ref bool success)
        {
            var all = WebCamTexture.devices?.Select(d => d.name).ToArray() ?? Array.Empty<string>();
            if (string.IsNullOrEmpty(device) || !all.Contains(device))
            {
                success = false;
                return;
            }

            if (_activeVideoDevice == device)
            {
                success = true;
                return;
            }

            _activeVideoDevice = device;
            RecreateWebcamIfNeeded();
            success = true;
        }

        // --- Sources ---
        partial void AddSourceInternal(Renderer output, NetworkObject networkObject)
        {
            if (output == null) return;
            if (!_videoRenderers.Contains(output))
                _videoRenderers.Add(output);

            EnsureWebcamCreated();
            ApplyTextureToRenderer(output);
        }

        partial void AddSourceInternal(RawImage output, NetworkObject networkObject)
        {
            if (output == null) return;
            if (!_videoRawImages.Contains(output))
                _videoRawImages.Add(output);

            EnsureWebcamCreated();
            ApplyTextureToRawImage(output);
        }

        // --- Audio (minimal) ---
        partial void GetVoiceMutedInternal(ref bool value)
        {
            value = _voiceMuted;
        }

        partial void SetVoiceMutedInternal(bool value)
        {
            if (_voiceMuted == value) return;
            _voiceMuted = value;
            VoiceMuteChanged?.Invoke(_voiceMuted);
        }

        partial void GetConnectedRecordingDevicesInternal(ref string[] value)
        {
            value = Microphone.devices ?? Array.Empty<string>();
        }

        partial void GetActiveRecordingDeviceInternal(ref string deviceName)
        {
            deviceName = _activeMicDevice;
        }

        partial void SetActiveRecordingDeviceInternal(string value, ref bool success)
        {
            var list = Microphone.devices ?? Array.Empty<string>();
            if (!string.IsNullOrEmpty(value) && list.Contains(value))
            {
                _activeMicDevice = value;
                success = true;
            }
            else
            {
                success = false;
            }
        }

        partial void AddSourceInternal(AudioSource source, NetworkObject networkObject)
        {
            // No-op in offline stub.
        }

        partial void IsMutedInternal(NetworkObject networkObject, ref bool isMuted)
        {
            isMuted = _voiceMuted;
        }

        partial void CalculateCurrentAmplitude(ref float amplitude)
        {
            amplitude = 0f;
        }

        partial void IsUserSpeakingInternal(int userId, ref bool isSpeaking)
        {
            isSpeaking = false;
        }

        partial void MuteUserInternal(int userId, bool mute)
        {
            // No-op in offline stub.
        }

        partial void IsUserMutedInternal(int userId, ref bool isMuted)
        {
            isMuted = false;
        }

        // --- Helpers ---
        private void EnsureWebcamCreated()
        {
            if (_videoDisabled)
                return;

            if (_webCamTexture != null)
                return;

            if (string.IsNullOrEmpty(_activeVideoDevice))
            {
                var first = WebCamTexture.devices?.FirstOrDefault();
                _activeVideoDevice = first?.name;
            }

            _webCamTexture = string.IsNullOrEmpty(_activeVideoDevice)
                ? new WebCamTexture(_videoWidth, _videoHeight)
                : new WebCamTexture(_activeVideoDevice, _videoWidth, _videoHeight);

            _webCamTexture.Play();
            ApplyTextureToOutputs();
        }

        private void RecreateWebcamIfNeeded()
        {
            var wasPlaying = _webCamTexture != null && _webCamTexture.isPlaying;
            StopAndDisposeWebcam();

            if (!_videoDisabled)
            {
                EnsureWebcamCreated();
                if (wasPlaying && _webCamTexture != null && !_webCamTexture.isPlaying)
                    _webCamTexture.Play();
            }
        }

        private void StopAndDisposeWebcam()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                    _webCamTexture.Stop();
                _webCamTexture = null;
            }
            ApplyTextureToOutputs();
        }

        private void ApplyTextureToOutputs()
        {
            foreach (var r in _videoRenderers.Where(r => r))
                ApplyTextureToRenderer(r);
            foreach (var img in _videoRawImages.Where(i => i))
                ApplyTextureToRawImage(img);
        }

        private void ApplyTextureToRenderer(Renderer r)
        {
            var mat = r.material;
            if (mat == null)
                return;

            mat.mainTexture = _videoDisabled ? null : _webCamTexture as Texture;

            // Apply flips using texture scale/offset (no custom shader needed)
            var sx = INVERT_X ? -1f : 1f;
            var sy = INVERT_Y ? -1f : 1f;
            mat.mainTextureScale = new Vector2(sx, sy);
            mat.mainTextureOffset = new Vector2(INVERT_X ? 1f : 0f, INVERT_Y ? 1f : 0f);
        }

        private void ApplyTextureToRawImage(RawImage img)
        {
            img.texture = _videoDisabled ? null : _webCamTexture as Texture;

            // Apply flips using uvRect on RawImage (no shader/blit)
            if (_videoDisabled || img.texture == null)
            {
                img.uvRect = new Rect(0, 0, 1, 1);
                return;
            }

            float x = INVERT_X ? 1f : 0f;
            float y = INVERT_Y ? 1f : 0f;
            float w = INVERT_X ? -1f : 1f;
            float h = INVERT_Y ? -1f : 1f;
            img.uvRect = new Rect(x, y, w, h);
        }
    }
}
#endif
