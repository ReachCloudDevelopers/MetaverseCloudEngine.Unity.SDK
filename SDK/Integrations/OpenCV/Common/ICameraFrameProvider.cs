#if (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV)
using System;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    public interface ICameraFrameProvider
    {
        event Action Initialized;
        event Action Disposed;
        bool RequestedIsFrontFacing { get; set; }
        void Dispose();
        bool IsInitialized();
        bool IsInitializing();
        void Initialize();
        bool IsStreaming();
        ICameraFrame DequeueNextFrame();
    }
}
#endif