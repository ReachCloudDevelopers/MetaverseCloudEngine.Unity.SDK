#if !(PLATFORM_LUMIN && !UNITY_EDITOR) && (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV) 

#if !UNITY_WSA_10_0

using System;
using System.Collections.Generic;
using System.IO;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using MetaverseCloudEngine.Unity.OpenCV.Common;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    /// <summary>
    /// A class that performs YOLO Classification.
    /// </summary>
    public sealed class YOLOClassification : ImageInferenceNet
    {
        private YoloClassPredictor _classPredictor;
        private string _classesPath;
        private string _modelPath;

        protected override IEnumerable<string> GetRequiredAIModelDependencies()
        {
            return new List<string>
            {
                "yolo.v8.classification.onnx", 
                "yolo.v8.imagenetlabels.txt"
            };
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _classPredictor?.Dispose();
            _classPredictor = null;
        }

        protected override bool OnPreInitialize(string[] dependencies, out object error)
        {
            error = null;
            
            if (dependencies.Length != 2)
            {
                error = "Expected 2 dependencies, but got " + dependencies.Length;
                return false;
            }
            
            if (string.IsNullOrEmpty(dependencies[0]) || !File.Exists(dependencies[0]))
            {
                error = "The model file does not exist.";
                return false;
            }
            
            if (string.IsNullOrEmpty(dependencies[1]) || !File.Exists(dependencies[1]))
            {
                error = "The classes file does not exist.";
                return false;
            }

            _modelPath = dependencies[0];
            _classesPath = dependencies[1];

            try
            {
                _classPredictor = new YoloClassPredictor(_modelPath, _classesPath, new Size(224, 224));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        protected override (IInferenceOutputData, Mat) PerformInference(ICameraFrame cameraFrame)
        {
            using var frameMat = cameraFrame.GetMat();
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_RGBA2BGR);
            var results = _classPredictor.Infer(frameMat);
            Imgproc.cvtColor(frameMat, frameMat, Imgproc.COLOR_BGR2RGBA);
            _classPredictor.Visualize(frameMat, results, false, true);
            return default;
        }
    }
}
#endif

#endif