#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)
using System;
using OpenCVForUnity.CoreModule;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    public interface IFrameMatrix : IDisposable
    {
        Mat GetMat();
        bool ProvidesDepthData();
        float SampleDepth(int sampleX, int sampleY);
        bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point);
    }
    
    public interface ITextureToMatrixProvider
    {
        event Action Initialized;
        event Action Disposed;
        bool RequestedIsFrontFacing { get; set; }
        void Dispose();
        bool IsInitialized();
        bool IsInitializing();
        void Initialize();
        bool IsStreaming();
        IFrameMatrix DequeueNextFrame();
    }
}
#endif