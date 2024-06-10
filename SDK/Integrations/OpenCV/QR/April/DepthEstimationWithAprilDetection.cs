#if METAVERSE_CLOUD_ENGINE && MV_APRIL_TAG
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
        [SerializeField] private bool spawnObjects;
        [SerializeField] private Transform spawnParent;
        
        private AprilTag.TagDetector _detector;
        private ICameraFrameProvider _textureProvider;
        private Texture2D _t2d;
        private readonly Dictionary<string, GameObject> _spawnedObjects = new();

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

                var position = t.Position;
                if (flipX) position.x = size.x - position.x;
                if (flipY) position.y = size.y - position.y;
                
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

            if (!spawnObjects) 
                return;

            foreach (var obj in _spawnedObjects.Where(obj => detectedObjects.All(d => d.Label != obj.Key)))
            {
                Destroy(obj.Value);
                _spawnedObjects.Remove(obj.Key);
            }
            
            foreach (var detectedObject in detectedObjects)
            {
                if (_spawnedObjects.TryGetValue(detectedObject.Label, out var go))
                {
                    go.transform.position = detectedObject.Origin;
                    go.transform.rotation = detectedObject.Rotation;
                    go.transform.parent = spawnParent;
                }
                else
                {
                    go = new GameObject
                    {
                        name = detectedObject.Label,
                        transform =
                        {
                            position = detectedObject.Origin,
                            rotation = detectedObject.Rotation,
                            parent = spawnParent
                        }
                    };
                    _spawnedObjects[detectedObject.Label] = go;
                }
            }
        }
    }
}
#endif