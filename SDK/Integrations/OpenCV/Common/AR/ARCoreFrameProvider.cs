#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)

using System;
using System.Collections.Concurrent;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using TriInspectorMVCE;
using Unity.Collections;
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
        [SerializeField] private UnityEvent<Texture2D> frameReceivedEvent;

        private readonly ConcurrentQueue<ICameraFrame> _frameQueue = new();

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
            while (_frameQueue.Count > 0)
            {
                if (_frameQueue.TryDequeue(out var f))
                    f.Dispose();
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

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!arCameraManager.TryAcquireLatestCpuImage(out var image))
                return;

            try
            {
                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, image.width, image.height),
                    outputDimensions = new Vector2Int(image.width, image.height),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = XRCpuImage.Transformation.None
                };

                var frame = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
                var rawTextureData = frame.GetRawTextureData<byte>();
                image.Convert(conversionParams, new NativeArray<byte>(rawTextureData, Allocator.Temp));
                
                frame.Apply();
                frameReceivedEvent?.Invoke(frame);

                while (_frameQueue.Count > 5)
                {
                    if (_frameQueue.TryDequeue(out var f))
                        f.Dispose();
                }
                
                _frameQueue.Enqueue(new ArTexture2dFrame(frame, this));
            }
            finally
            {
                image.Dispose();
            }
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
                if (_texture)
                    Destroy(_texture);
                if (_frameMat is not null)
                {
                    _frameMat.Dispose();
                    _frameMat = null;
                }
            }

            public Mat GetMat()
            {
                if (_frameMat is null)
                    Utils.texture2DToMat(_texture, _frameMat, false);
                return _frameMat;
            }

            public ReadOnlySpan<Color32> GetColors32()
            {
                return _texture.GetRawTextureData<Color32>();
            }

            public Vector2Int GetSize()
            {
                return new Vector2Int(_texture.width, _texture.height);
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
