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
    public class YOLOSegmentation : ImageInferenceNet
    {
        [Tooltip("Confidence threshold.")] 
        public float confThreshold = 0.25f;
        [Tooltip("Non-maximum suppression threshold.")]
        public float nmsThreshold = 0.45f;
        [Tooltip("Maximum detections per image.")]
        public int topK = 300;
        [Tooltip("Enable mask image upsampling.")]
        public bool upsample = true;
        [Tooltip("Preprocess input image by resizing to a specific width.")]
        public int inpWidth = 640;
        [Tooltip("Preprocess input image by resizing to a specific height.")]
        public int inpHeight = 640;

        private YOLOSegmentPredictor _segmentPredictor;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _segmentPredictor?.Dispose();
        }

        protected override IEnumerable<string> GetRequiredAIModelDependencies()
        {
            return new List<string>
            {
                "yolo.v8.segmentation.onnx",
                "yolo.v8.coco.names"
            };
        }

        protected override (IInferenceOutputData, Mat) PerformInference(ICameraFrame cameraFrame)
        {
            using var frameMat = cameraFrame.GetMat(); 
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_RGBA2BGR);
            var results = _segmentPredictor.Infer(frameMat);
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_BGR2RGBA);
            using var detections = results[0];
            using var masks = results[1];
            _segmentPredictor.VisualizeMasks(frameMat, detections, masks, 0.5f, true);
            _segmentPredictor.Visualize(frameMat, detections, isRGB: true);
            return default;
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

            _segmentPredictor = new YOLOSegmentPredictor(
                dependencies[0], 
                new Size(inpWidth, inpHeight),
                confThreshold, nmsThreshold, topK, upsample);
            return true;
        }
    }
}
#endif

#endif
#endif