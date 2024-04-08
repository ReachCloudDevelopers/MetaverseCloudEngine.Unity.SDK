using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Intel.RealSense;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO.RealSense
{
    [HideMonoScript]
    [DefaultExecutionOrder(-int.MaxValue)]
    public class IntelRealSenseTextureSource : TriInspectorMonoBehaviour, ITextureToMatrixProvider
    {
        [Required]
        [Tooltip("The RS frame provider that will feed the color and depth frames to this texture source.")]
        [SerializeField]
        private RsFrameProvider frameProvider;

        private bool _isInitialized;
        private bool _isInitializing;
        private bool _disposed;
        private bool _gotFrame;
        private readonly object _destroyLock = new();
        private Align _align;
        private readonly ConcurrentQueue<RealSenseFrameData> _frames = new();

        private readonly struct RealSenseFrameData : IFrameMatrix
        {
            private readonly Vector3Int _colorSize;
            private readonly IntPtr _colorBuffer;
            private readonly Vector3Int _depthSize;
            private readonly IntPtr _depthBuffer;
            private readonly float _depthUnits;

            public RealSenseFrameData(Vector3Int colorSize, IntPtr colorBuffer, Vector3Int depthSize, IntPtr depthBuffer, float depthUnits)
            {
                _colorSize = colorSize;
                _colorBuffer = colorBuffer;
                _depthSize = depthSize;
                _depthBuffer = depthBuffer;
                _depthUnits = depthUnits;
            }
            
            public Mat GetMat()
            {
                var mat = new Mat(_colorSize.y, _colorSize.x, CvType.CV_8UC3, _colorBuffer);
                return mat;
            }

            public bool ProvidesDepthData()
            {
                return true;
            }

            public float SampleDepth(int sampleX, int sampleY)
            {
                if (sampleX < 0 || sampleX >= _depthSize.x || sampleY < 0 || sampleY >= _depthSize.y)
                    return -1;
                var depth = Marshal.ReadInt16(_depthBuffer, sampleY * _depthSize.z + sampleX * 2);
                return depth * _depthUnits;
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(_colorBuffer);
                Marshal.FreeHGlobal(_depthBuffer);
            }
        }

        public event Action Initialized;
        public event Action Disposed;

        public bool RequestedIsFrontFacing { get; set; }

        private void Awake()
        {
            if (!frameProvider)
                frameProvider = GetComponent<RsFrameProvider>();
            if (frameProvider is RsDevice d)
                d.processMode = RsDevice.ProcessMode.Multithread;
            frameProvider.OnNewSample += OnNewSample;
        }

        private void OnDestroy()
        {
            lock (_destroyLock)
                Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _align?.Dispose();
            _align = null;

            frameProvider.OnNewSample -= OnNewSample;
            Disposed?.Invoke();
            
            ClearFrames(true);
        }

        private void Update()
        {
            if (_gotFrame && _isInitializing)
            {
                _isInitialized = true;
                _isInitializing = false;
                Initialized?.Invoke();
            }
        }

        private unsafe void OnNewSample(Frame f)
        {
            lock (_destroyLock)
            {
                if (_disposed)
                    return;
                
                _align ??= new Align(Stream.Color);
                using var frame = _align.Process(f);

                if (!frame.IsComposite) 
                    return;
                
                using var composite = frame.As<FrameSet>();
                using var cFrame = composite.ColorFrame;
                using var dFrame = composite.DepthFrame;
                if (cFrame is null || dFrame is null)
                    return;

                var dBuffer = Marshal.AllocHGlobal(dFrame.Stride * dFrame.Height);
                var dSize = new Vector3Int(dFrame.Width, dFrame.Height, dFrame.Stride);
                var dUnits = dFrame.GetUnits();
                var cBuffer = Marshal.AllocHGlobal(cFrame.Stride * cFrame.Height);
                var cSize = new Vector3Int(cFrame.Width, cFrame.Height, cFrame.Stride);

                Buffer.MemoryCopy(
                    dFrame.Data.ToPointer(),
                    dBuffer.ToPointer(),
                    dFrame.Stride * dFrame.Height,
                    dFrame.Stride * dFrame.Height);
                Buffer.MemoryCopy(
                    cFrame.Data.ToPointer(),
                    cBuffer.ToPointer(),
                    cFrame.Stride * cFrame.Height,
                    cFrame.Stride * cFrame.Height);

                ClearFrames();
                
                _frames.Enqueue(new RealSenseFrameData(
                    cSize,
                    cBuffer,
                    dSize,
                    dBuffer,
                    dUnits));

                _gotFrame = true;
            }
        }

        private void ClearFrames(bool all = false)
        {
            while (_frames.Count > (all ? 0 : 1))
            {
                if (_frames.TryDequeue(out var __))
                    __.Dispose();
            }
        }

        public bool IsInitialized()
        {
            return _isInitialized && !_disposed;
        }

        public bool IsInitializing()
        {
            return _isInitializing;
        }

        public void Initialize()
        {
            if (_disposed)
                return;

            if (_isInitialized || _isInitializing)
                return;

            _isInitializing = true;
        }

        public bool IsStreaming()
        {
            return _isInitialized && !_disposed && frameProvider;
        }

        public IFrameMatrix DequeueNextFrame()
        {
            if (!_frames.TryDequeue(out var frame))
                return null;
            ClearFrames(true);
            return frame;
        }
    }
}