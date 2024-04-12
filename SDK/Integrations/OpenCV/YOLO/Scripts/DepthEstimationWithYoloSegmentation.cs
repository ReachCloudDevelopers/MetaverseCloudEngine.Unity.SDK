using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    [HideMonoScript]
    [DeclareFoldoutGroup("Advanced")]
    public class DepthEstimationWithYoloSegmentation : ImageInferenceNet, IObjectDetectionPipeline
    {
        public enum YoloModelDataSet
        {
            Coco,
        }

        public YoloModelDataSet modelDataSet;
        [FormerlySerializedAs("labelsToConsider")]
        [Group("Advanced")]
        [Title("Advanced Settings")]
        public List<string> labelWhitelist = new ();
        [Group("Advanced")]
        [Tooltip("Non-maximum suppression threshold. This value helps prevent duplicate detections. Lower = more detections but with more potential duplicates, Higher = less detections but less potential duplicates. Default is 0.45.")]
        [Range(0, 1f)]
        public float nmsThreshold = 0.45f;
        [Group("Advanced")]
        [Tooltip("Maximum detections per image.")]
        [Range(1, 100)]
        public int topK = 30;
        [Group("Advanced")]
        [Range(5, 64)]
        [Tooltip("The space between each pixel. A higher value means less pixels to process, but less accurate depth estimation.")]
        public int pixelMargin = 25;
        [Group("Advanced")]
        [Tooltip("The number of pixels around the object mask to discard from the object detection.")]
        public int objectBoundaryMargin = 5;
        [Group("Advanced")]
        [Tooltip("Enable mask image upsampling.")]
        public bool upsample = true;
        [Group("Advanced")]
        [Tooltip("Visualize bounding boxes. Note: This will cost performance.")]
        public bool visualizeBoundingBoxes;
        [Group("Advanced")]
        [Tooltip("Visualize masks. Note: This will cost performance.")]
        public bool visualizeMasks;
        [Group("Advanced")]
        [Tooltip("A label to assign to the background. This is not a label from the YOLO model.")]
        public string backgroundLabel = "background";
        
        private IYoloModel _segmentPredictor;
        private bool _destroyed;
        private readonly object _lock = new();
        
        public event Action<List<IObjectDetectionPipeline.DetectedObject>> DetectableObjectsUpdated;

        protected override IEnumerable<string> GetRequiredAIModelDependencies()
        {
            return new List<string>
            {
                GetYoloModelName()
            };
        }

        protected override void OnDestroy()
        {
            lock (_lock)
            {
                base.OnDestroy();
                _segmentPredictor?.Dispose();
            }
        }

        protected override bool OnPreInitialize(string[] dependencies, out object error)
        {
            error = null;

            if (string.IsNullOrEmpty(dependencies[0]) || !System.IO.File.Exists(dependencies[0]))
            {
                error = "The YOLO model file does not exist.";
                return false;
            }

            LoadSegmentPredictor(dependencies[0]);
            return true;
        }

        protected override (IInferenceOutputData, Mat) PerformInference(IFrameMatrix frame)
        {
            lock (_lock)
                try
                {
                    if (_destroyed)
                        return default;

                    using var frameMat = frame.GetMat();
                    using var inferenceMat = new Mat();
                    
                    Imgproc.cvtColor(frameMat, inferenceMat, Imgproc.COLOR_RGBA2BGR);
                    var results = _segmentPredictor.Infer(inferenceMat);
                    var visualization = frameMat.clone();
                    using var masks = results[1];
                    using var detections = results[0];
                    if (visualizeBoundingBoxes) _segmentPredictor.Visualize(visualization, detections, isRGB: true);
                    if (visualizeMasks) _segmentPredictor.VisualizeMasks(visualization, detections, masks, isRGB: true);

                    var visualFrameWidth = frameMat.width();
                    var visualFrameHeight = frameMat.height();
                    var detectedObjects = new List<IObjectDetectionPipeline.DetectedObject>();
                    var objectRects = _segmentPredictor.GetObjectRects(detections);
                    var environment = !string.IsNullOrEmpty(backgroundLabel) ? new IObjectDetectionPipeline.DetectedObject
                    {
                        Label = backgroundLabel,
                        Vertices = new List<Vector3>(),
                        Score = 1,
                        IsBackground = true,
                        
                    } : null;

                    if (environment is not null)
                    {
                        for (var visualFrameX = 0; visualFrameX < visualFrameWidth; visualFrameX += pixelMargin)
                        {
                            for (var visualFrameY = 0; visualFrameY < visualFrameHeight; visualFrameY += pixelMargin)
                            {
                                var blocked = false;
                                for (var objectIndex = 0; objectIndex < objectRects.Length; objectIndex++)
                                {
                                    var obj = objectRects[objectIndex];
                                    var classLabel = _segmentPredictor.GetClassLabel(obj.cls);
                                    if (DiscardObject(classLabel))
                                        break;

                                    var inMask = IsInMask(masks, visualFrameX, visualFrameY, visualFrameWidth, visualFrameHeight, objectIndex);
                                    if (inMask)
                                    {
                                        blocked = true;
                                        break;
                                    }

                                    if (!AreAnyAdjacentPixelsInsideOfMask(masks, visualFrameX, visualFrameY, visualFrameWidth, visualFrameHeight, objectIndex, pixelMargin + 2))
                                        continue;

                                    blocked = true;
                                    break;
                                }
                            
                                if (blocked)
                                    continue;

                                if (!frame.TryGetCameraRelativePoint(visualFrameX, visualFrameY, out var point))
                                    continue;
                            
                                environment.Vertices.Add(point);
                            }
                        }

                        if (environment.Vertices.Count > 0)
                            detectedObjects.Add(environment);
                    }

                    for (var objectIndex = 0; objectIndex < objectRects.Length; objectIndex++)
                    {
                        IObjectDetectionPipeline.DetectedObject detectableObject = null;
                        var nearestZ = float.MaxValue;
                    
                        var obj = objectRects[objectIndex];
                        var classLabel = _segmentPredictor.GetClassLabel(obj.cls);
                    
                        if (DiscardObject(classLabel))
                            continue;

                        var calculatedBounds = false;
                        Bounds bounds = default;
                        for (var visualFrameX = (int)obj.x1; visualFrameX < obj.x2; visualFrameX += pixelMargin)
                        {
                            if (visualFrameX < 0 || visualFrameX >= visualFrameWidth)
                                continue;

                            for (var visualFrameY = (int)obj.y1; visualFrameY < obj.y2; visualFrameY += pixelMargin)
                            {
                                if (visualFrameY < 0 || visualFrameY >= visualFrameHeight)
                                    continue;

                                var inMask = IsInMask(masks, visualFrameX, visualFrameY, visualFrameWidth, visualFrameHeight, objectIndex);
                                if (!inMask) 
                                    continue;
                                
                                if (AreAnyAdjacentPixelsOutOfMask(masks, visualFrameX, visualFrameY, visualFrameWidth, visualFrameHeight, objectIndex, objectBoundaryMargin))
                                    continue;

                                if (!frame.TryGetCameraRelativePoint(visualFrameX, visualFrameY, out var point))
                                    continue;

                                if (point.z < nearestZ)
                                    nearestZ = point.z;

                                detectableObject ??= new IObjectDetectionPipeline.DetectedObject
                                {
                                    Label = classLabel,
                                    Vertices = new List<Vector3>(),
                                    Rect = new Vector4(obj.x1, obj.y1, obj.x2, obj.y2)
                                };

                                if (!calculatedBounds) bounds = new Bounds(point, Vector3.zero);
                                else bounds.Encapsulate(point);
                                calculatedBounds = true;
                                
                                detectableObject.Vertices.Add(point);
                            }
                        }

                        if (detectableObject != null && detectableObject.Vertices.Count > 1)
                        {
                            detectedObjects.Add(detectableObject);
                            detectableObject.NearestZ = nearestZ;
                            detectableObject.Score = obj.conf;
                            detectableObject.Origin = bounds.center;
                        }
                    }

                    return (new DepthInferenceOutputData
                    {
                        Objects = detectedObjects
                        
                    }, visualization);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    throw;
                }
        }

        /// <summary>
        /// Updates the model at runtime.
        /// </summary>
        /// <param name="model">The model ID.</param>
        public void SetModel(int model)
        {
            SetModel((YoloModelDataSet)model);
        }

        /// <summary>
        /// Updates the model at runtime.
        /// </summary>
        /// <param name="modelDataSet">The yolo model to use.</param>
        public void SetModel(YoloModelDataSet modelDataSet)
        {
            this.modelDataSet = modelDataSet;
            FetchResources();
        }

        private string GetYoloModelName()
        {
            switch (modelDataSet)
            {
                case YoloModelDataSet.Coco:
                    return "yolo.v8.segmentation.onnx";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool DiscardObject(string classLabel)
        {
            return labelWhitelist.Count > 0 && !labelWhitelist.Contains(classLabel);
        }

        private void LoadSegmentPredictor(string modelPath)
        {
            lock (_lock)
            {
                _segmentPredictor?.Dispose();
                _segmentPredictor = null;
                _segmentPredictor = new YOLOSegmentPredictor(
                    modelPath,
                    new Size(640, 640),
                    0,
                    nmsThreshold,
                    topK,
                    upsample);
            }
        }

        private static bool AreAnyAdjacentPixelsOutOfMask(Mat masks, int visualFrameX, int visualFrameY, int imageWidth, int imageHeight, int objectIndex, int neighborThreshold = 1)
        {
            if (neighborThreshold <= 0)
                return false;
            if (!IsInMask(masks, visualFrameX - neighborThreshold, visualFrameY - neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            if (!IsInMask(masks, visualFrameX + neighborThreshold, visualFrameY - neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            if (!IsInMask(masks, visualFrameX - neighborThreshold, visualFrameY + neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            if (!IsInMask(masks, visualFrameX + neighborThreshold, visualFrameY + neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            return false;
        }
        
        private static bool AreAnyAdjacentPixelsInsideOfMask(
            Mat masks, int visualFrameX, int visualFrameY, int imageWidth, int imageHeight, int objectIndex, int neighborThreshold = 1)
        {
            if (neighborThreshold <= 0)
                return false;
            if (IsInMask(masks, visualFrameX - neighborThreshold, visualFrameY - neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            if (IsInMask(masks, visualFrameX + neighborThreshold, visualFrameY - neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            if (IsInMask(masks, visualFrameX - neighborThreshold, visualFrameY + neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            if (IsInMask(masks, visualFrameX + neighborThreshold, visualFrameY + neighborThreshold, imageWidth, imageHeight, objectIndex))
                return true;
            return false;
        }

        protected override void OnMainThreadPostProcessInference(IInferenceOutputData outputData)
        {
            base.OnMainThreadPostProcessInference(outputData);
            
            if (outputData is DepthInferenceOutputData opt)
                DetectableObjectsUpdated?.Invoke(opt.Objects);
        }

        private static bool IsInMask(Mat masks, int x, int y, int imageWidth, int imageHeight, int nIndex)
        {
            if (masks.empty())
                return false;
            
            if (nIndex >= masks.size(0) || nIndex < 0)
                return false;
            
            var imageAspectRatio = (float) imageWidth / imageHeight;
            var xDelta = x / (float)imageWidth;
            var yDelta = y / (float)imageHeight;
            var maskWidth = masks.size(2);
            var maskHeight = masks.size(1);
            
            // Scale down the image to fit within the mask, but keep the image's aspect ratio
            if (imageHeight > imageWidth)
            {
                imageHeight = maskHeight;
                imageWidth = (int)(maskHeight * imageAspectRatio);
            }
            else
            {
                imageWidth = maskWidth;
                imageHeight = (int)(maskWidth / imageAspectRatio);
            }

            x = (int) (xDelta * imageWidth);
            y = (int) (yDelta * imageHeight);

            // For example if the image size is 640 x 480, the padding y
            // is 80 because 640 - 480 = 160, and 160 / 2 = 80 therefore...
            x += (maskWidth - imageWidth) / 2;
            y += (maskHeight - imageHeight) / 2;
            
            if (x >= maskWidth || x < 0 || y >= maskHeight || y < 0)
                return false;

            var weights = masks.get(new [] { nIndex, y, x });
            if (weights == null)
                return false;
            
            return Math.Abs(weights[0] - 255) < 1e-5;
        }

        public class DepthInferenceOutputData : IInferenceOutputData
        {
            public List<IObjectDetectionPipeline.DetectedObject> Objects = new();

            public void Dispose()
            {
                Objects = null;
            }
        }
    }
}