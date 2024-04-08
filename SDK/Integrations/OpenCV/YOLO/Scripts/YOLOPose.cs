#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)
#if !(PLATFORM_LUMIN && !UNITY_EDITOR)
#if !UNITY_WSA_10_0

using System.Collections.Generic;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    public class YOLOPose : ImageInferenceNet
    {
        [Tooltip("Confidence threshold.")]
        public float confThreshold = 0.25f;
        [Tooltip("Non-maximum suppression threshold.")]
        public float nmsThreshold = 0.45f;
        [Tooltip("Maximum detections per image.")]
        public int topK = 300;

        private readonly List<Mat> _results = new();
        private YOLOPoseEstimation _poseEstimation;

        protected override IEnumerable<string> GetRequiredAIModelDependencies()
        {
            return new List<string>
            {
                "yolo.v8.pose.onnx",
                "yolo.v8.coco.names"
            };
        }

        protected override bool OnPreInitialize(string[] dependencies, out object error)
        {
            error = null;

            if (dependencies.Length != 2)
            {
                error = "Expected 2 dependencies, but got " + dependencies.Length;
                return false;
            }

            if (string.IsNullOrEmpty(dependencies[0]) || !System.IO.File.Exists(dependencies[0]))
            {
                error = "The model file does not exist.";
                return false;
            }

            if (string.IsNullOrEmpty(dependencies[1]) || !System.IO.File.Exists(dependencies[1]))
            {
                error = "The classes file does not exist.";
                return false;
            }

            _poseEstimation = new YOLOPoseEstimation(dependencies[0], dependencies[1], 
                new Size(640, 640), confThreshold, nmsThreshold, topK);
            return true;
        }

        protected override (IInferenceOutputData, Mat) PerformInference(IFrameMatrix frame)
        {
            using var frameMat = frame.GetMat(); 
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_RGBA2BGR);
            _results.Clear();
            _poseEstimation.infer(frameMat, _results);
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_BGR2RGBA);
            _poseEstimation.visualize_kpts(frameMat, _results[1], 5, true, true);
            _poseEstimation.visualize(frameMat, _results[0], false, true);
            return default;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _poseEstimation?.dispose();
            _poseEstimation = null;
        }
    }
}
#endif
#endif
#endif