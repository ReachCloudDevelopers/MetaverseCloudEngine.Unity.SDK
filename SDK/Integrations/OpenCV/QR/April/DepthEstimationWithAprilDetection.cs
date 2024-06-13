#if (METAVERSE_CLOUD_ENGINE && MV_APRIL_TAG) || METAVERSE_CLOUD_ENGINE_INTERNAL

using System;
using System.Collections.Generic;
using System.Linq;
using AprilTag.Interop;
using MetaverseCloudEngine.Unity.OpenCV.Common;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.CoreModule;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;
using Rect = UnityEngine.Rect;

namespace MetaverseCloudEngine.Unity.OpenCV
{
    [HideMonoScript]
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
                var position = t.Position;
                var rotation = t.Rotation; // Use normal to compute rotation
                if (flipXPosition) position.x = -position.x;
                if (flipYPosition) position.y = -position.y;
                if (flipXRotation) rotation *= Quaternion.Euler(0, 0, 180);
                if (flipYRotation) rotation *= Quaternion.Euler(0, 180, 0);

                var o = new IObjectDetectionPipeline.DetectedObject
                {
                    Label = t.ID.ToString(),
                    Score = 1,
                    Vertices = new List<Vector3> { position },
                    Origin = position,
                    NearestZ = position.z,
                    Rotation = rotation,
                    IsBackground = false,
                };
                return o;
            })
            .ToList();

            DetectableObjectsUpdated?.Invoke(detectedObjects);

            if (!spawnObjects) 
                return;

            foreach (var obj in _spawnedObjects.Where(obj => detectedObjects.All(d => d.Label != obj.Key)).ToArray())
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