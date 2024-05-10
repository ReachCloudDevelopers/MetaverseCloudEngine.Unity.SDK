#if METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV
using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Pool;
using MetaverseCloudEngine.Unity.OpenCV.BYTETracker;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    [HideMonoScript]
    [RequireComponent(typeof(IObjectDetectionPipeline))]
    public class DepthBasedEnvironmentVoxelizationEngine : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class BaseObjectType
        {
            [Tooltip("The prefab to instantiate to represent the object being detected.")]
            public GameObject prefab;
            [Tooltip("A helpful color distinction.")]
            public Color color = Color.white;
            [Min(0)] [Tooltip("The absolute maximum distance away from the camera a vertex can be.")]
            public float maxDistance;
            [Min(1)] [Tooltip("The minimum number of vertices required to modify the object's voxels.")]
            public int minVertices;
            [FormerlySerializedAs("discardVertexDistance")]
            [Min(0)]
            [Tooltip("The distance relative to the nearest vertex to the camera that any other vertex can be.")]
            public float expectedObjectRadius;
            [Tooltip("Whether or not to ONLY perform positional tracking, rather than generating voxels.")]
            public bool positionTrackingOnly;
            [Tooltip("If true, adds a trigger volume that encapsulates the bounding volume of the object.")]
            public bool addTriggerVolume;
        }

        [Serializable]
        public class ObjectType : BaseObjectType
        {
            [Title("$label")]
            [Required]
            [Tooltip("The label of the object type")]
            [PropertyOrder(-999)]
            public string label;
        }

        [Tooltip("The root parent object to use for the generated objects.")]
        [SerializeField]
        private Transform parent;
        [Tooltip("Object type templates.")]
        [SerializeField]
        private ObjectType[] objectTypes;
        [SerializeField] private bool detectOtherObjects;

        [ShowIf(nameof(detectOtherObjects))] [SerializeField]
        private BaseObjectType baseObjectType = new ObjectType
        {
            color = Color.white,
            maxDistance = 0,
            minVertices = 1,
            expectedObjectRadius = Mathf.Infinity,
            positionTrackingOnly = false,
            addTriggerVolume = true,
        };

        [Tooltip("The minimum size of the voxels when at their nearest to the camera.")] [Min(0)] [SerializeField]
        private float minVoxelSize = 0.001f;

        [Tooltip("The maximum size of the voxels when at their furthest from the camera.")] [Min(0)] [SerializeField]
        private float maxVoxelSize = 0.5f;

        [Tooltip("The larger this value, the longer an object will stay in memory when it is no longer tracked.")]
        [Min(1)]
        [SerializeField]
        private int trackingBuffer = 15;

        [Range(0, 1f)] [SerializeField] private float trackingThreshold = 45f;
        [Range(0, 1f)] [SerializeField] private float highThreshold = 0.65f;
        [Range(0, 1f)] [SerializeField] private float matchThreshold = 0.8f;
        [SerializeField] private bool backgroundEnabled = true;

        private Dictionary<IObjectDetectionPipeline, VoxelizationData> _voxelizations;
        private ObjectInstance _background;

        private class VoxelizationData
        {
            private readonly DepthBasedEnvironmentVoxelizationEngine _engine;

            public VoxelizationData(DepthBasedEnvironmentVoxelizationEngine engine, IObjectDetectionPipeline pipeline)
            {
                _engine = engine;
                _pipeline = pipeline;
            }

            private ByteTracker _tracker;
            private readonly IObjectDetectionPipeline _pipeline;
            private List<IObjectDetectionPipeline.DetectedObject> _lastFrameObjects;
            private Dictionary<string, BaseObjectType> _labelToPrefabs;
            private readonly List<ObjectInstance> _spawnedObjects = new();
            private readonly Dictionary<GameObject, List<GameObject>> _voxelObjectMap = new();
            private readonly List<int> _trackedObjectIds = new();
            private bool _frameDirty;

            public void Init()
            {
                _labelToPrefabs = _engine.objectTypes.ToDictionary(obj => obj.label, obj => (BaseObjectType)obj);
                InitializeTracker();
            }

            private void InitializeTracker()
            {
                if (!Application.isPlaying)
                    return;
                _tracker?.Clear();
                _tracker = new ByteTracker(
                    maxRetentionTime: _engine.trackingBuffer,
                    trackThresh: _engine.trackingThreshold,
                    highThresh: _engine.highThreshold,
                    matchThresh: _engine.matchThreshold);
            }

            public void OnEnable()
            {
                _pipeline.DetectableObjectsUpdated += ProcessFrame;
            }

            public void OnDisable()
            {
                _pipeline.DetectableObjectsUpdated -= ProcessFrame;
            }

            public void DeleteAll()
            {
                foreach (var spawnedObject in _spawnedObjects.Where(spawnedObject => spawnedObject.Instance))
                {
                    ReleaseVoxelsForInstance(spawnedObject);
                    Destroy(spawnedObject.Instance);
                }

                _spawnedObjects.Clear();
            }

            public void ProcessFrame()
            {
                if (!_frameDirty)
                    return;

                if (_engine.backgroundEnabled && 
                    _lastFrameObjects.Count > 0 && 
                    _lastFrameObjects[0].IsBackground)
                {
                    var envInstance = _engine.GetBackgroundObjectInstance();
                    var lastFrameObject = _lastFrameObjects[0];
                    if (lastFrameObject.Vertices.Count > 0)
                        ReleaseVoxelsForInstance(envInstance);

                    for (var vertexIndex = 0; vertexIndex < lastFrameObject.Vertices.Count; vertexIndex++)
                    {
                        AddVoxel(
                            lastFrameObject,
                            vertexIndex,
                            envInstance,
                            Vector3.zero);
                    }
                }

                var trackedResults = _tracker.Update(
                    _lastFrameObjects
                        .Where(data => !data.IsBackground)
                        .Select(data =>
                        {
                            var width = data.Rect.z - data.Rect.x;
                            var height = data.Rect.w - data.Rect.y;
                            var rect = new TlwhRect(data.Rect.y, data.Rect.x, width, height);
                            return (Detection)new Detection<IObjectDetectionPipeline.DetectedObject>(
                                data, 
                                rect,
                                data.Score);
                        })
                        .ToList());

                _trackedObjectIds.Clear();

                for (var objectIndex = trackedResults.Count - 1; objectIndex >= 0; objectIndex--)
                {
                    var trackedResult = trackedResults[objectIndex];
                    var detectionInfo = (Detection<IObjectDetectionPipeline.DetectedObject>)trackedResult.Detection;
                    var detectedObjectReference = detectionInfo.Ref;
                    TrackAndDetectObject(detectionInfo, trackedResult, detectedObjectReference);
                }

                for (var index = _spawnedObjects.Count - 1; index >= 0; index--)
                {
                    var instance = _spawnedObjects[index];
                    if (_trackedObjectIds.Contains(instance.Track.TrackId) &&
                        instance.Track.DetectionState != TrackState.Removed)
                        continue;
                    _spawnedObjects.Remove(instance);
                    ReleaseVoxelsForInstance(instance);
                    Destroy(instance.Instance);
                }

                if (_engine.parent)
                {
                    var ordered = _spawnedObjects.OrderBy(x => x.Track.TrackId);
                    var idx = 0;
                    foreach (var spawnedObj in ordered)
                    {
                        spawnedObj.Instance.transform.SetSiblingIndex(idx);
                        idx++;
                    }
                }

                _lastFrameObjects = null;
                _frameDirty = false;
            }

            private void ProcessFrame(List<IObjectDetectionPipeline.DetectedObject> results)
            {
                _lastFrameObjects = results.ToList();
                _frameDirty = true;
            }

            private void ReleaseVoxelsForInstance(ObjectInstance spawnedObject)
            {
                if (!_voxelObjectMap.ContainsKey(spawnedObject.Instance))
                    return;

                for (var index = _voxelObjectMap[spawnedObject.Instance].Count - 1; index >= 0; index--)
                {
                    var voxel = _voxelObjectMap[spawnedObject.Instance][index];
                    _engine._voxelPool.Release(voxel);
                }

                _voxelObjectMap.Remove(spawnedObject.Instance);
            }

            private void TrackAndDetectObject(
                Detection<IObjectDetectionPipeline.DetectedObject> detectionInfo,
                Track trackedResult, 
                IObjectDetectionPipeline.DetectedObject detectedObjectReference)
            {
                if (detectionInfo.Ref.Vertices.Count == 0)
                    return;

                if (trackedResult.DetectionState != TrackState.Tracked)
                {
                    _trackedObjectIds.Add(trackedResult.TrackId);
                    return;
                }

                if (!TryTrackObject(detectionInfo.Ref.Label, trackedResult, detectionInfo, out var instance))
                    return;

                var objectInstance = instance.Instance;

                if (detectionInfo.Ref.Vertices.Count < instance.Type.minVertices)
                {
                    _trackedObjectIds.Add(trackedResult.TrackId);
                    instance.RevertReplacement();
                    return;
                }

                Bounds bounds = default;
                var vertexCount = 0;
                for (var vertexIndex = detectedObjectReference.Vertices.Count - 1; vertexIndex >= 0; vertexIndex--)
                {
                    var vertex = detectedObjectReference.Vertices[vertexIndex];
                    if (instance.Type.expectedObjectRadius > 0 && vertex.z - detectedObjectReference.NearestZ >
                        instance.Type.expectedObjectRadius) continue;
                    if (instance.Type.maxDistance > 0 && vertex.z > instance.Type.maxDistance) continue;
                    if (vertexCount == 0) bounds = new Bounds(vertex, Vector3.zero);
                    else bounds.Encapsulate(vertex);
                    vertexCount++;
                }

                if (vertexCount < instance.Type.minVertices)
                {
                    _trackedObjectIds.Add(trackedResult.TrackId);
                    instance.RevertReplacement();
                    return;
                }

                ReleaseVoxelsForInstance(instance);

                var objectOrigin = bounds.center;
                var instanceTransform = objectInstance.transform;
                instanceTransform.parent = _engine.parent;
                instanceTransform.localPosition = objectOrigin;
                instanceTransform.localRotation = detectionInfo.Ref.Rotation;
                
                if (instance.Type.addTriggerVolume)
                {
                    instance.Trigger.size = bounds.size;
                    instance.Trigger.enabled = true;
                }

                instance.FinalizeReplacement();
                instance.Instance.SetActive(true);

                if ((_engine.minVoxelSize > 0 || _engine.maxVoxelSize > 0) && !instance.Type.positionTrackingOnly)
                {
                    for (var vertexIndex = detectedObjectReference.Vertices.Count - 1; vertexIndex >= 0; vertexIndex--)
                        AddVoxel(detectedObjectReference, vertexIndex, instance, objectOrigin);
                }

                _trackedObjectIds.Add(trackedResult.TrackId);
            }

            private void AddVoxel(IObjectDetectionPipeline.DetectedObject detectedObjectReference,
                int vertexIndex,
                ObjectInstance instance,
                Vector3 objectOrigin)
            {
                if (!Application.isPlaying)
                    return;

                var vertex = detectedObjectReference.Vertices[vertexIndex];
                if (instance.Type.expectedObjectRadius > 0 &&
                    vertex.z - detectedObjectReference.NearestZ > instance.Type.expectedObjectRadius) return;
                if (instance.Type.maxDistance > 0 && vertex.z > instance.Type.maxDistance) return;
                var voxel = _engine._voxelPool.Get();
                var inverseLerpSize = Mathf.InverseLerp(0.01f, 10f, vertex.z);
                var voxelSize = Mathf.Lerp(_engine.minVoxelSize, _engine.maxVoxelSize, inverseLerpSize);
                voxel.transform.parent = instance.Instance.transform;
                voxel.transform.localPosition = Quaternion.Inverse(instance.Instance.transform.localRotation) * (vertex - objectOrigin);
                voxel.transform.localScale = Vector3.one * voxelSize;
                if (voxel.TryGetComponent<MeshRenderer>(out var ren))
                {
                    if (instance.Type.color.a > 0)
                    {
                        ren.enabled = true;
                        ren.material.color = instance.Type.color;
                    }
                    else ren.enabled = false;
                }

                if (!_voxelObjectMap.TryGetValue(instance.Instance, out var voxels))
                    voxels = _voxelObjectMap[instance.Instance] = new List<GameObject>();
                voxels.Add(voxel);
            }

            private bool TryTrackObject(
                string label, Track track, Detection<IObjectDetectionPipeline.DetectedObject> detection,
                out ObjectInstance instance)
            {
                instance = null;

                if (track.DetectionState != TrackState.Tracked)
                    return false;

                if (!_labelToPrefabs.TryGetValue(detection.Ref.Label, out var type) && !_engine.detectOtherObjects)
                    return false;

                var isNewType = type == null;
                type ??= _engine.baseObjectType;

                if (!isNewType && !type.prefab)
                {
                    type.prefab = new GameObject(detection.Ref.Label) { hideFlags = HideFlags.HideInHierarchy };
                    type.prefab.SetActive(false);
                }

                var existing = FindBestOriginalObjectSource(track, detection, ref instance);
                if (existing)
                    return true;

                var newObj = isNewType && !type.prefab
                    ? new GameObject
                    {
                        transform =
                        {
                            parent = _engine.parent,
                            rotation = detection.Ref.Rotation,
                        }
                    }
                    : Instantiate(type.prefab, _engine.parent);
                newObj.name = $"{detection.Ref.Label}_{track.TrackId}";
                newObj.hideFlags = HideFlags.None;
                newObj.SetActive(false);
                instance = new ObjectInstance
                {
                    Track = track,
                    Instance = newObj,
                    Type = type,
                    Label = label,
                };
                if (type.addTriggerVolume)
                {
                    var bc = instance.Instance.AddComponent<BoxCollider>();
                    bc.isTrigger = true;
                    bc.enabled = false;
                    instance.Trigger = bc;
                }

                _spawnedObjects.Add(instance);
                return true;
            }

            private GameObject FindBestOriginalObjectSource(
                Track newTrack,
                Detection<IObjectDetectionPipeline.DetectedObject> detection,
                ref ObjectInstance instance)
            {
                var match = _spawnedObjects.FirstOrDefault(x =>
                    x.Label == detection.Ref.Label && x.Track.TrackId == newTrack.TrackId);

                if (match == null)
                {
                    match = _spawnedObjects.FirstOrDefault(x =>
                        x.Label == detection.Ref.Label &&
                        x.Track.DetectionState == TrackState.Lost &&
                        NearCoordinates(x, detection));

                    if (match == null)
                        return null;
                }

                var existingObj = match.Instance;
                instance = match;
                if (match.Track.TrackId != newTrack.TrackId)
                    match.ReplaceWith(newTrack);
                return existingObj;
            }
        }

        private readonly ObjectPool<GameObject> _voxelPool = new(() =>
            {
                if (!Application.isPlaying)
                    return null;

                var voxel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                voxel.hideFlags = HideFlags.HideInHierarchy;
                return voxel;
            },
            actionOnGet: go =>
            {
                if (go) go.SetActive(true);
            },
            actionOnRelease: go =>
            {
                if (!go) return;
                go.SetActive(false);
                go.transform.parent = null;
            },
            actionOnDestroy: go =>
            {
                if (go) Destroy(go);
            });

        private class ObjectInstance
        {
            private Track _previousTrack;
            public string Label;
            public Track Track;
            public GameObject Instance;
            public BaseObjectType Type;
            public BoxCollider Trigger;

            public void ReplaceWith(Track newTrack)
            {
                Track.MarkAsRemoved();
                _previousTrack = Track;
                Track = newTrack;
            }

            public void FinalizeReplacement()
            {
                _previousTrack = null;
            }

            public void RevertReplacement()
            {
                if (_previousTrack is null)
                    return;

                Track.MarkAsRemoved();
                Track = _previousTrack;
                Track.DetectionState = TrackState.Tracked;
            }
        }

        private void Awake()
        {
            _voxelizations = GetComponents<IObjectDetectionPipeline>()
                .ToDictionary(x => x, y => new VoxelizationData(this, y).Do(x => x.Init()));
        }

        private void OnValidate()
        {
            DeleteAll();
            InitializeTrackers();
        }

        private void OnEnable()
        {
            InitializeTrackers();
            EnableAll();
        }

        private void OnDisable()
        {
            DeleteAll();
            DisableAll();
        }

        private void OnDestroy()
        {
            _voxelPool?.Dispose();
        }

        private void FixedUpdate()
        {
            ProcessFrames();
        }

        private void EnableAll()
        {
            if (_voxelizations is null)
                return;
            foreach (var o in _voxelizations)
                o.Value.OnEnable();
        }

        private void DisableAll()
        {
            if (_voxelizations is null)
                return;
            foreach (var o in _voxelizations)
                o.Value.OnDisable();
        }

        private void DeleteAll()
        {
            if (_background != null)
            {
                Destroy(_background.Instance);
                _background = null;
            }

            if (_voxelizations is null) return;
            foreach (var o in _voxelizations)
                o.Value.DeleteAll();
        }

        private void InitializeTrackers()
        {
            if (_voxelizations is null)
                return;
            foreach (var o in _voxelizations)
                o.Value.Init();
        }

        private void ProcessFrames()
        {
            if (_voxelizations is null)
                return;
            foreach (var o in _voxelizations)
                o.Value.ProcessFrame();
        }

        private ObjectInstance GetBackgroundObjectInstance()
        {
            _background ??= new ObjectInstance
            {
                Instance = new GameObject("background")
                {
                    transform = { parent = parent }
                }.Do(x => x.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity)),
                Type = new ObjectType
                {
                    color = Color.black,
                }
            };

            return _background;
        }

        private static bool NearCoordinates(ObjectInstance source,
            Detection<IObjectDetectionPipeline.DetectedObject> detection)
        {
            var distance = Vector2.Distance(detection.Ref.Origin, source.Instance.transform.localPosition);
            return distance <= source.Type.expectedObjectRadius * 2f;
        }
    }
}
#endif