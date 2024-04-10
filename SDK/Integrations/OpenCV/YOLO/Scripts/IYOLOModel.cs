using System.Runtime.InteropServices;
using OpenCVForUnity.CoreModule;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    public interface IYoloModel
    {
        void Dispose();
        Mat[] Infer(Mat inferenceMat);
        void Visualize(Mat visualizationMat, Mat detections, bool isRGB = true);
        void VisualizeMasks(Mat image, Mat detections, Mat masks, float alpha = 0.5f, bool isRGB = true);
        DetectionData[] GetObjectRects(Mat objects);
        string GetClassLabel(float clsValue);

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct DetectionData
        {
            public readonly float x1;
            public readonly float y1;
            public readonly float x2;
            public readonly float y2;
            public readonly float conf;
            public readonly float cls;

            public override string ToString()
            {
                return "x1:" + x1 + " y1:" + y1 + "x2:" + x2 + " y2:" + y2 + " conf:" + conf + "  cls:" + cls;
            }
        }
    }
}