#if METAVERSE_CLOUD_ENGINE && MV_APRIL_TAG
using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("flipX")] [SerializeField] private bool flipXPosition;
        [FormerlySerializedAs("flipY")] [SerializeField] private bool flipYPosition;
        [FormerlySerializedAs("flipX")] [SerializeField] private bool flipXRotation;
        [FormerlySerializedAs("flipY")] [SerializeField] private bool flipYRotation;
        [SerializeField] private bool spawnObjects;
        [SerializeField] private Transform spawnParent;
        [SerializeField] private GameObject defaultPrefab;
        [SerializeField] private List<TagPrefab> tagPrefabs = new();

        [Serializable]
        public class TagPrefab
        {
            [Required] public string targetTag;
            [Required] public GameObject prefab;
        }
        
        /// <summary>
        /// If true, will flip the X axis of the detected object's position.
        /// </summary>
        public bool FlipXPosition
        {
            get => flipXPosition;
            set => flipXPosition = value;
        }
        
        /// <summary>
        /// If true, will flip the Y axis of the detected object's position.
        /// </summary>
        public bool FlipYPosition
        {
            get => flipYPosition;
            set => flipYPosition = value;
        }
        
        /// <summary>
        /// If true, will flip the X axis of the detected object's rotation.
        /// </summary>
        public bool FlipXRotation
        {
            get => flipXRotation;
            set => flipXRotation = value;
        }
        
        /// <summary>
        /// If true, will flip the Y axis of the detected object's rotation.
        /// </summary>
        public bool FlipYRotation
        {
            get => flipYRotation;
            set => flipYRotation = value;
        }

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
            _detector.ProcessImage(colors, frame.GetFOV(ICameraFrame.FOVType.Vertical), tagSize);
            
            var detectedObjects = _detector.DetectedTags.Select(t =>
            {
                var v0 = new Vector2(size.x - (float)t.Detection.Corner1.x, size.y - (float)t.Detection.Corner1.y); // bl
                var v1 = new Vector2(size.x - (float)t.Detection.Corner2.x, size.y - (float)t.Detection.Corner2.y); // br
                var v2 = new Vector2(size.x - (float)t.Detection.Corner3.x, size.y - (float)t.Detection.Corner3.y); // tr
                var v3 = new Vector2(size.x - (float)t.Detection.Corner4.x, size.y - (float)t.Detection.Corner4.y); // tl

                var distance = 0f;
                switch (distanceCalculation)
                {
                    case TagDistanceCalculationMode.FixedSize:
                        var fov = frame.GetFOV(ICameraFrame.FOVType.Vertical);
                        var focalLength = size.x / (2 * Mathf.Tan(fov * Mathf.Deg2Rad / 2));
                        distance = tagSize * focalLength / (v1 - v0).magnitude;
                        break;
                    case TagDistanceCalculationMode.DepthSensor:
                    {
                        var center = (v0 + v1 + v2 + v3) / 4;
                        var depth = frame.SampleDepth((int)center.x, (int)center.y);
                        if (depth > 0)
                            distance = depth;
                        break;
                    }
                }
                
                Debug.DrawLine(v0, v1, Color.red);
                Debug.DrawLine(v1, v2, Color.green);
                Debug.DrawLine(v2, v3, Color.blue);
                Debug.DrawLine(v3, v0, Color.cyan);

                var position = t.Position;
                var rotation = t.Rotation;
                position.z = distance;
                if (flipXPosition) position.x = -position.x;
                if (flipYPosition) position.y = -position.y;
                if (flipXRotation) rotation *= Quaternion.Euler(0, 0, 180);
                if (flipYRotation) rotation *= Quaternion.Euler(0, 180, 0);
                
                var o = new IObjectDetectionPipeline.DetectedObject
                {
                    Label = t.Detection.ID.ToString(),
                    Score = 1,
                    Vertices = new List<Vector3> { position },
                    Origin = position,
                    Rotation = rotation,
                    NearestZ = position.z,
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
                    go.transform.localPosition = detectedObject.Origin;
                    go.transform.localRotation = detectedObject.Rotation;
                    go.transform.parent = spawnParent;
                }
                else
                {
                    var prefab = tagPrefabs.FirstOrDefault(p => p.targetTag == detectedObject.Label)?.prefab ?? defaultPrefab;
                    if (!prefab)
                        go = new GameObject
                        {
                            name = detectedObject.Label,
                            transform =
                            {
                                localPosition = detectedObject.Origin,
                                localRotation = detectedObject.Rotation,
                                parent = spawnParent
                            }
                        };
                    else
                    {
                        go = Instantiate(prefab, detectedObject.Origin, detectedObject.Rotation, spawnParent);
                        go.name = detectedObject.Label;
                    }
                    
                    _spawnedObjects[detectedObject.Label] = go;
                }
            }
        }
    }
}
#endif