#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)

using System;
using System.Collections.Concurrent;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using TriInspectorMVCE;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MetaverseCloudEngine.Unity.OpenCV.AR
{
    [DisallowMultipleComponent]
    public class ARCoreFrameProvider : TriInspectorMonoBehaviour, ICameraFrameProvider
    {
        [Required]
        [SerializeField] private ARCameraManager arCameraManager;
        [SerializeField, Range(1, 180)] private float fieldOfView = 60;
        [SerializeField] private XRCpuImage.Transformation imageTransformation = XRCpuImage.Transformation.MirrorY;
        [SerializeField] private UnityEvent<Texture2D> frameReceivedEvent;

        private readonly ConcurrentQueue<ICameraFrame> _frameQueue = new();
        private Texture2D _frame;

        public event Action Initialized;
        public event Action Disposed;
        public bool RequestedIsFrontFacing { get; set; }

        private void Awake()
        {
            if (!arCameraManager)
                arCameraManager = FindObjectOfType<ARCameraManager>(true);
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Dispose();
        }

        public void Dispose()
        {
            ClearAllFrames();
            Disposed?.Invoke();
            arCameraManager.frameReceived -= OnFrameReceived;
        }

        private void ClearAllFrames(int frames = 0)
        {
            while (_frameQueue.Count > frames)
            {
                if (_frameQueue.TryDequeue(out var f))
                    f?.Dispose();
            }
        }

        public bool IsInitialized()
        {
            return enabled && arCameraManager && arCameraManager.enabled && arCameraManager.subsystem != null;
        }

        public bool IsInitializing()
        {
            return false;
        }

        public void Initialize()
        {
            if (!arCameraManager) return;
            arCameraManager.frameReceived += OnFrameReceived;
            Initialized?.Invoke();
        }

        public bool IsStreaming()
        {
            return IsInitialized();
        }

        public ICameraFrame DequeueNextFrame()
        {
            ClearAllFrames(1);
            
            return _frameQueue.TryDequeue(out var frame) ? frame : null;
        }

        private unsafe void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return;

            if (!arCameraManager.permissionGranted)
            {
                Debug.Log("Permissions denied.");
                return;
            }
            
            const TextureFormat format = TextureFormat.RGBA32;

            if (_frame == null || _frame.width != image.width || _frame.height != image.height)
            {
                _frame = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
                
                MetaverseDispatcher.AtEndOfFrame(() => frameReceivedEvent?.Invoke(_frame));
            }

            var conversionParams = new XRCpuImage.ConversionParams(image, format, XRCpuImage.Transformation.MirrorY);

            var rawTextureData = _frame.GetRawTextureData<byte>();
            try
            {
                image.Convert(
                    conversionParams, 
                    new IntPtr(rawTextureData.GetUnsafePtr()), 
                    rawTextureData.Length);
            }
            finally
            {
                image.Dispose();
            }
            
            _frame.Apply();
            
            ClearAllFrames(3);
            
            _frameQueue.Enqueue(new ArTexture2dFrame(_frame, this));
        }

        private class ArTexture2dFrame : ICameraFrame
        {
            private readonly Texture2D _texture;
            private Mat _frameMat;
            private readonly ARCoreFrameProvider _frameProvider;

            public ArTexture2dFrame(Texture2D texture, ARCoreFrameProvider frameProvider)
            {
                _texture = texture;
                _frameProvider = frameProvider;
            }
            
            
            public void Dispose()
            {
                if (_frameMat is not null)
                {
                    _frameMat.Dispose();
                    _frameMat = null;
                }
            }

            public Mat GetMat()
            {
                if (_frameMat is not null) 
                    return _frameMat;
                
                _frameMat = new Mat();
                Utils.texture2DToMat(_texture, _frameMat, false);
                
                return _frameMat;
            }

            public ReadOnlySpan<Color32> GetColors32()
            {
                return _texture ? _texture.GetPixels32() : ReadOnlySpan<Color32>.Empty;
            }

            public Vector2Int GetSize()
            {
                return _texture ? new Vector2Int(_texture.width, _texture.height) : Vector2Int.one;
            }

            public float GetFOV(ICameraFrame.FOVType type)
            {
                return _frameProvider.fieldOfView;
            }

            public bool ProvidesDepthData()
            {
                return false;
            }

            public float SampleDepth(int sampleX, int sampleY)
            {
                throw new NotImplementedException();
            }

            public bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point)
            {
                throw new NotImplementedException();
            }
        }
    }
}

#endif
