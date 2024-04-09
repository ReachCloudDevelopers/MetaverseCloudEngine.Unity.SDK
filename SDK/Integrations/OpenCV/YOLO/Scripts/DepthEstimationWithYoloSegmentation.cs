using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV.YOLO
{
    [HideMonoScript]
    public class DepthEstimationWithYoloSegmentation : ImageInferenceNet, IObjectDetectionPipeline
    {
        [Header("YOLO Settings")]
        [Range(0, 1)]
        [Tooltip("Confidence threshold. Lower = more less accurate detections, Higher = less more accurate detections. Default is 0.5.")]
        public float confThreshold = 0.5f;
        [Tooltip("Non-maximum suppression threshold. This value helps prevent duplicate detections. Lower = more detections but with more potential duplicates, Higher = less detections but less potential duplicates. Default is 0.45.")]
        [Range(0, 1f)]
        public float nmsThreshold = 0.45f;
        [Tooltip("Maximum detections per image.")]
        [Range(1, 100)]
        public int topK = 20;
        [Tooltip("Enable mask image upsampling.")]
        public bool upsample = true;
        [Tooltip("Visualize bounding boxes. Note: This will cost performance.")]
        public bool visualizeBoundingBoxes;
        [Tooltip("Visualize masks. Note: This will cost performance.")]
        public bool visualizeMasks;
        public List<string> labelsToConsider = new ();

        [Header("Depth Est. Settings")]
        [Range(1, 180)]
        public float verticalFov = 58;
        [Range(1, 180)]
        public float horizontalFov = 58;
        [Range(5, 64)]
        [Tooltip("The space between each pixel. A higher value means less pixels to process, but less accurate depth estimation.")]
        public int pixelMargin = 15;
        [Tooltip("The number of pixels around the object mask to discard from the object detection.")]
        public int objectBoundaryMargin = 5;
        [Tooltip("A label to assign to the environment. This is not a label from the YOLO model.")]
        public string environmentLabel = "env";
        
        private YOLOSegmentPredictor _segmentPredictor;
        private bool _destroyed;
        private readonly object _destroyLock = new();
        
        public event Action<List<IObjectDetectionPipeline.DetectedObject>> DetectableObjectsUpdated;
        
        protected override IEnumerable<string> GetRequiredAIModelDependencies()
        {
            return new List<string>
            {
                "yolo.v8.segmentation.onnx",
                "yolo.v8.coco.names"
            };
        }

        protected override void OnDestroy()
        {
            lock (_destroyLock)
            {
                base.OnDestroy();
                _segmentPredictor?.Dispose();
            }
        }

        protected override bool OnPreInitialize(string[] dependencies, out object error)
        {
            error = null;

            if (dependencies.Length != 2)
            {
                error = "Expected 3 dependencies, but got " + dependencies.Length;
                return false;
            }

            if (string.IsNullOrEmpty(dependencies[0]) || !System.IO.File.Exists(dependencies[0]))
            {
                error = "The YOLO model file does not exist.";
                return false;
            }

            if (string.IsNullOrEmpty(dependencies[1]) || !System.IO.File.Exists(dependencies[1]))
            {
                error = "The YOLO classes file does not exist.";
                return false;
            }

            lock (_destroyLock)
                _segmentPredictor = new YOLOSegmentPredictor(
                    dependencies[0],
                    dependencies[1],
                    new Size(640, 640),
                    confThreshold,
                    nmsThreshold,
                    topK,
                    upsample);
            return true;
        }

        protected override (IInferenceOutputData, Mat) PerformInference(IFrameMatrix frame)
        {
            lock (_destroyLock)
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
                    var environment = !string.IsNullOrEmpty(environmentLabel) ? new IObjectDetectionPipeline.DetectedObject
                    {
                        Label = environmentLabel,
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
                                    if (DiscardObject(classLabel, obj))
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

                                var depth = frame.SampleDepth(
                                    visualFrameX, visualFrameY);
                            
                                if (depth <= 0)
                                    continue;

                                var pixelToWorld = MVUtils.PixelToWorld(
                                    visualFrameX,
                                    visualFrameY,
                                    depth,
                                    visualFrameWidth,
                                    visualFrameHeight,
                                    verticalFov,
                                    horizontalFov);

                                environment.Vertices.Add(pixelToWorld);
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
                    
                        if (DiscardObject(classLabel, obj))
                        {
                            continue;
                        }

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
                                var cameraRelativeZ = frame.SampleDepth(
                                    visualFrameX, visualFrameY);

                                if (cameraRelativeZ <= 0)
                                    continue;
                            
                                var pos = MVUtils.PixelToWorld(
                                    visualFrameX,
                                    visualFrameY,
                                    cameraRelativeZ,
                                    visualFrameWidth,
                                    visualFrameHeight,
                                    verticalFov,
                                    horizontalFov);

                                if (!inMask) 
                                    continue;

                                if (AreAnyAdjacentPixelsOutOfMask(masks, visualFrameX, visualFrameY, visualFrameWidth, visualFrameHeight, objectIndex, objectBoundaryMargin))
                                    continue;

                                if (cameraRelativeZ < nearestZ)
                                    nearestZ = cameraRelativeZ;

                                detectableObject ??= new IObjectDetectionPipeline.DetectedObject
                                {
                                    Label = classLabel,
                                    Vertices = new List<Vector3>(),
                                    Rect = new Vector4(obj.x1, obj.y1, obj.x2, obj.y2)
                                };

                                if (!calculatedBounds) bounds = new Bounds(pos, Vector3.zero);
                                else bounds.Encapsulate(pos);
                                calculatedBounds = true;
                                
                                detectableObject.Vertices.Add(pos);
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

        private bool DiscardObject(string classLabel, YOLOSegmentPredictor.DetectionData obj)
        {
            return labelsToConsider.Count > 0 && 
                   !labelsToConsider.Contains(classLabel) ||
                   obj.conf < confThreshold;
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
            y += (maskHeight - imageHeight) / 2;
            x += (maskWidth - imageWidth) / 2;
            
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