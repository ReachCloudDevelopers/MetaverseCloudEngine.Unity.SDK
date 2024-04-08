using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    public abstract class YOLODepthSampler : MonoBehaviour
    {
        public abstract float SampleDepth(float sampleX, float sampleY);
    }
}