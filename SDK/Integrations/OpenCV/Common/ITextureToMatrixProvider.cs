#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)
using System;
using OpenCVForUnity.CoreModule;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    public interface IFrameMatrix : IDisposable
    {
        Mat GetMat();
        bool ProvidesDepthData();
        float SampleDepth(int sampleX, int sampleY);
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