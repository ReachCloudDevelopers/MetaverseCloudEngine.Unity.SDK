#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)
using System;
using OpenCVForUnity.CoreModule;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    public interface ICameraFrame : IDisposable
    {
        public enum FOVType
        {
            Horizontal,
            Vertical,
        }
        
        Mat GetMat();
        ReadOnlySpan<Color32> GetColors32();
        Vector2Int GetSize();
        float GetFOV(FOVType type);
        bool ProvidesDepthData();
        float SampleDepth(int sampleX, int sampleY);
        bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point);
    }
}
#endif