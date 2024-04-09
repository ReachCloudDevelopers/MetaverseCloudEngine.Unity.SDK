using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Intel.RealSense;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Assertions;

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
        [Range(-0.1f, 0.1f)]
        [Tooltip("If the depth sensor is damaged or not properly aligned internally, you can use this value to adjust the coordinates on the X axis.")]
        [SerializeField] private float xSlide;
        [Range(-0.1f, 0.1f)]
        [Tooltip("If the depth sensor is damaged or not properly aligned internally, you can use this value to adjust the coordinates on the Y axis.")]
        [SerializeField] private float ySlide;

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
            private readonly Intrinsics _intrinsics;
            private readonly IntelRealSenseTextureSource _source;
            private readonly IntPtr _depthBuffer;
            private readonly float _depthUnits;

            public RealSenseFrameData(
                Vector3Int colorSize, 
                IntPtr colorBuffer, 
                Vector3Int depthSize, 
                IntPtr depthBuffer, 
                float depthUnits,
                Intrinsics intrinsics,
                IntelRealSenseTextureSource source)
            {
                _colorSize = colorSize;
                _colorBuffer = colorBuffer;
                _depthSize = depthSize;
                _depthBuffer = depthBuffer;
                _depthUnits = depthUnits;
                _intrinsics = intrinsics;
                _source = source;
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

            public bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point)
            {
                point = default;

                if (_intrinsics.model == Distortion.ModifiedBrownConrady)
                    return false;
                
                var depth = SampleDepth(sampleX, sampleY);
                if (depth <= 0)
                    return false;
                
                var x = (sampleX - _intrinsics.ppx) / _intrinsics.fx;
                var y = (sampleY - _intrinsics.ppy) / _intrinsics.fy;
                var xo = x;
                var yo = y;

                switch (_intrinsics.model)
                {
                    case Distortion.InverseBrownConrady:
                    {
                        // need to loop until convergence 
                        // 10 iterations determined empirically
                        for (var i = 0; i < 10; i++)
                        {
                            var r2 = x * x + y * y;
                            var icDist = 1 / (1 + ((_intrinsics.coeffs[4] * r2 + _intrinsics.coeffs[1]) * r2 + _intrinsics.coeffs[0]) * r2);
                            var xq = x / icDist;
                            var yq = y / icDist;
                            var deltaX = 2 * _intrinsics.coeffs[2] * xq * yq + _intrinsics.coeffs[3] * (r2 + 2 * xq * xq);
                            var deltaY = 2 * _intrinsics.coeffs[3] * xq * yq + _intrinsics.coeffs[2] * (r2 + 2 * yq * yq);
                            x = (xo - deltaX) * icDist;
                            y = (yo - deltaY) * icDist;
                        }

                        break;
                    }
                    case Distortion.BrownConrady:
                    {
                        // need to loop until convergence 
                        // 10 iterations determined empirically
                        for (var i = 0; i < 10; i++)
                        {
                            var r2 = x * x + y * y;
                            var icDist = 1f / (1f + ((_intrinsics.coeffs[4] * r2 + _intrinsics.coeffs[1]) * r2 + _intrinsics.coeffs[0]) * r2);
                            var deltaX = 2 * _intrinsics.coeffs[2] * x * y + _intrinsics.coeffs[3] * (r2 + 2 * x * x);
                            var deltaY = 2 * _intrinsics.coeffs[3] * x * y + _intrinsics.coeffs[2] * (r2 + 2 * y * y);
                            x = (xo - deltaX) * icDist;
                            y = (yo - deltaY) * icDist;
                        }

                        break;
                    }
                    case Distortion.KannalaBrandt4:
                    {
                        var rd = Mathf.Sqrt(x * x + y * y);
                        if (rd < float.Epsilon)
                        {
                            rd = float.Epsilon;
                        }

                        var theta = rd;
                        var theta2 = rd * rd;
                        for (var i = 0; i < 4; i++)
                        {
                            var f = theta * (1 + theta2 * (_intrinsics.coeffs[0] + theta2 * (_intrinsics.coeffs[1] + theta2 * (_intrinsics.coeffs[2] + theta2 * _intrinsics.coeffs[3])))) - rd;
                            if (Mathf.Abs(f) < float.Epsilon)
                                break;
                            var df = 1 + theta2 * (3 * _intrinsics.coeffs[0] + theta2 * (5 * _intrinsics.coeffs[1] + theta2 * (7 * _intrinsics.coeffs[2] + 9 * theta2 * _intrinsics.coeffs[3])));
                            theta -= f / df;
                            theta2 = theta * theta;
                        }
                        var r = Mathf.Tan(theta);
                        x *= r / rd;
                        y *= r / rd;
                        break;
                    }
                    case Distortion.Ftheta:
                    {
                        var rd = Mathf.Sqrt(x * x + y * y);
                        if (rd < float.Epsilon)
                            rd = float.Epsilon;
                        var r = Mathf.Tan(_intrinsics.coeffs[0] * rd) / Mathf.Atan(2 * Mathf.Tan(_intrinsics.coeffs[0] / 2.0f));
                        x *= r / rd;
                        y *= r / rd;
                        break;
                    }
                }

                point[0] = (depth * x) + _source.xSlide;
                point[1] = (depth * -y) + _source.ySlide;
                point[2] = depth;

                return true;
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
                
                var intrinsics = cFrame.Profile.As<VideoStreamProfile>().GetIntrinsics();
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
                    dUnits,
                    intrinsics,
                    this));

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