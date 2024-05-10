using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.OpenCV
{
    [HideMonoScript]
    [RequireComponent(typeof(ICameraFrameProvider))]
    public class DepthEstimationWithAprilDetection : TriInspectorMonoBehaviour, IObjectDetectionPipeline
    {
        public enum TagDistanceCalculationMode
        {
            FixedSize,
            DepthSensor,
        }

        [SerializeField] private TagDistanceCalculationMode distanceCalculation;
        [Min(0.001f)]
        [ShowIf(nameof(distanceCalculation), TagDistanceCalculationMode.FixedSize)]
        [SerializeField] private float tagSize = 1f;
        [SerializeField] private bool flipX;
        [SerializeField] private bool flipY;
        
        private AprilTag.TagDetector _detector;
        private ICameraFrameProvider _textureProvider;
        private Texture2D _t2d;

        public event Action<List<IObjectDetectionPipeline.DetectedObject>> DetectableObjectsUpdated;

        private void Awake()
        {
            _textureProvider = GetComponent<ICameraFrameProvider>();
        }

        private void OnDestroy()
        {
            _detector?.Dispose();
        }

        private void Update()
        {
            using var frame = _textureProvider.DequeueNextFrame();
            if (frame is null)
                return;
            
            var colors = frame.GetColors32();
            var size = frame.GetSize();
            
            _detector ??= new AprilTag.TagDetector(size.x, size.y);
            _detector.ProcessImage(colors, frame.GetFOV(1), tagSize);
            
            var detectedObjects = _detector.DetectedTags.Select(t =>
            {
                var v0 = new Vector2(size.x - (float)t.Detection.Corner1.x, size.y - (float)t.Detection.Corner1.y); // bl
                var v1 = new Vector2(size.x - (float)t.Detection.Corner2.x, size.y - (float)t.Detection.Corner2.y); // br
                var v2 = new Vector2(size.x - (float)t.Detection.Corner3.x, size.y - (float)t.Detection.Corner3.y); // tr
                var v3 = new Vector2(size.x - (float)t.Detection.Corner4.x, size.y - (float)t.Detection.Corner4.y); // tl
                
                Debug.DrawLine(v0, v1, Color.red);
                Debug.DrawLine(v1, v2, Color.green);
                Debug.DrawLine(v2, v3, Color.blue);
                Debug.DrawLine(v3, v0, Color.cyan);
                
                var o = new IObjectDetectionPipeline.DetectedObject
                {
                    Label = t.Detection.ID.ToString(),
                    Score = t.Detection.DecisionMargin / 100f,
                    Vertices = new List<Vector3> { t.Position },
                    Origin = t.Position,
                    Rotation = t.Rotation,
                    NearestZ = t.Position.z,
                    Rect = new Vector4(v0.x, v0.y, v2.x, v2.y),
                    IsBackground = false,
                };
                return o;
            })
            .ToList();
            
            DetectableObjectsUpdated?.Invoke(detectedObjects);
        }
    }
}