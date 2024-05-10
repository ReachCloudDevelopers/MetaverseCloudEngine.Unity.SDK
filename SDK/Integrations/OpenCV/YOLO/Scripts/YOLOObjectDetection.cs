#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)
#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

#if !UNITY_WSA_10_0

using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    [RequireComponent(typeof(WebCameraFrameProvider))]
    [HideMonoScript]
    public class YOLOObjectDetection : ImageInferenceNet
    {
        [Header("Object Detection Model Settings")]
        [Tooltip("Confidence threshold.")]
        public float confThreshold = 0.25f;
        [Tooltip("Non-maximum suppression threshold.")]
        public float nmsThreshold = 0.45f;
        [Tooltip("Maximum detections per image.")]
        public int topK = 300;

        private YOLOObjectDetector _objectDetector;
        private YOLOObjectDetector.DetectionData[] _detectionData;

        public event Action<YOLOObjectDetector.DetectionData[]> DetectionResults;

        protected override IEnumerable<string> GetRequiredAIModelDependencies()
        {
            return new List<string>
            {
                "yolo.v8.objectdetection.onnx",
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

            _objectDetector = new YOLOObjectDetector(dependencies[0], dependencies[1], new Size(640, 640),
                confThreshold, nmsThreshold, topK);
            return true;
        }

        protected override (IInferenceOutputData, Mat) PerformInference(ICameraFrame cameraFrame)
        {
            using var frameMat = cameraFrame.GetMat();
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_RGBA2BGR);
            var results = _objectDetector.infer(frameMat);
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_BGR2RGBA);
            _objectDetector.visualize(frameMat, results, false, true);
            _detectionData = _objectDetector.getData(results);
            return default;
        }

        protected override void OnMainThreadPostProcessInference(IInferenceOutputData outputData)
        {
            base.OnMainThreadPostProcessInference(outputData);
            DetectionResults?.Invoke(_detectionData);
        }

        public string GetLabel(float cls)
        {
            return _objectDetector.getClassLabel(cls);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _objectDetector?.dispose();
            _objectDetector = null;
        }
    }
}
#endif

#endif
#endif