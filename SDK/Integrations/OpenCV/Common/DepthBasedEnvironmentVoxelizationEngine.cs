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
        public struct ObjectType
        {
            [Title("$label")]
            [Required]
            [Tooltip("The label of the object type")]
            public string label;
            [Tooltip("If true, the tracking ID will be appended to the object's name.")]
            public bool appendTrackingIdToName;
            [Tooltip("The prefab to instantiate to represent the object being detected.")]
            public GameObject prefab;
            [Tooltip("A helpful color distinction.")]
            public Color color;
            [Min(0)]
            [Tooltip("The absolute maximum distance away from the camera a vertex can be.")]
            public float maxDistance;
            [Min(1)]
            [Tooltip("The minimum number of vertices required to modify the object's voxels.")]
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

        [Tooltip("The root parent object to use for the generated objects.")]
        [SerializeField] private Transform parent;
        [Tooltip("Object type templates.")]
        [SerializeField] private ObjectType[] objectTypes;
        [Tooltip("The minimum size of the voxels when at their nearest to the camera.")]
        [Min(0)] [SerializeField] private float minVoxelSize = 0.001f;
        [Tooltip("The maximum size of the voxels when at their furthest from the camera.")]
        [Min(0)] [SerializeField] private float maxVoxelSize = 0.5f;
        [Tooltip("The larger this value, the longer an object will stay in memory when it is no longer tracked.")]
        [Min(1)] [SerializeField] private int trackingBuffer = 5;
        [SerializeField] private bool backgroundEnabled = true;

        private ByteTracker _tracker;
        private IObjectDetectionPipeline _pipeline;
        private List<IObjectDetectionPipeline.DetectedObject> _lastFrameObjects;
        private Dictionary<string, ObjectType> _labelToPrefabs;
        private readonly List<ObjectInstance> _spawnedObjects = new();
        private readonly Dictionary<GameObject, List<GameObject>> _voxelObjectMap = new();
        private readonly List<int> _trackedObjectIds = new();
        private bool _frameDirty;
        private ObjectInstance _background;

        private readonly ObjectPool<GameObject> _voxelPool = new(() =>
            {
                if (!Application.isPlaying)
                    return null;
                
                var voxel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                voxel.hideFlags = HideFlags.HideInHierarchy;
                return voxel;
            },
            go =>
            {
                if (go) go.SetActive(true);
            },
            go =>
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

            public Track Track;
            public GameObject Instance;
            public ObjectType ObjectType;
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
            _labelToPrefabs = objectTypes.ToDictionary(obj => obj.label, obj => obj);
            _pipeline = GetComponent<IObjectDetectionPipeline>();
            InitializeTracker();
        }

        private void InitializeTracker()
        {
            if (!Application.isPlaying)
                return;
            _tracker?.Clear();
            _tracker = new ByteTracker(maxRetentionTime: trackingBuffer);
        }

        private void OnValidate()
        {
            _labelToPrefabs = objectTypes.ToDictionary(obj => obj.label, obj => obj);
            DeleteAll();
            InitializeTracker();
        }

        private void OnEnable()
        {
            InitializeTracker();
            _pipeline.DetectableObjectsUpdated += ProcessFrame;
        }

        private void OnDisable()
        {
            DeleteAll();
            _tracker.Clear();
            _pipeline.DetectableObjectsUpdated -= ProcessFrame;
        }

        private void OnDestroy()
        {
            _voxelPool?.Dispose();
        }

        private void FixedUpdate()
        {
            ProcessFrame();
        }

        private void ReleaseVoxelsForInstance(ObjectInstance spawnedObject)
        {
            if (!_voxelObjectMap.ContainsKey(spawnedObject.Instance))
                return;

            for (var index = _voxelObjectMap[spawnedObject.Instance].Count - 1; index >= 0; index--)
            {
                var voxel = _voxelObjectMap[spawnedObject.Instance][index];
                _voxelPool.Release(voxel);
            }

            _voxelObjectMap.Remove(spawnedObject.Instance);
        }

        private void DeleteAll()
        {
            if (_background != null)
            {
                ReleaseVoxelsForInstance(_background);
                Destroy(_background.Instance);
                _background = null;
            }
            
            foreach (var spawnedObject in _spawnedObjects.Where(spawnedObject => spawnedObject.Instance))
            {
                ReleaseVoxelsForInstance(spawnedObject);
                Destroy(spawnedObject.Instance);
            }
            
            _spawnedObjects.Clear();
        }

        private void ProcessFrame(List<IObjectDetectionPipeline.DetectedObject> results)
        {
            _lastFrameObjects = results.ToList();
            _frameDirty = true;
        }

        private void ProcessFrame()
        {
            if (!_frameDirty)
                return;

            if (backgroundEnabled && _lastFrameObjects.Count > 0 && _lastFrameObjects[0].IsBackground)
            {
                var envInstance = GetBackgroundObjectInstance();
                var lastFrameObject = _lastFrameObjects[0];
                if (lastFrameObject.Vertices.Count > 0)
                    ReleaseVoxelsForInstance(envInstance);
                
                for (var vertexIndex = 0; vertexIndex < lastFrameObject.Vertices.Count; vertexIndex++)
                {
                    AddVoxel(
                        lastFrameObject, 
                        vertexIndex, 
                        envInstance, 
                        envInstance.Instance.transform,
                        Vector3.zero, 
                        envInstance.Instance);
                }
            }

            var trackedResults = _tracker.Update(
                _lastFrameObjects
                    .Where(data => !data.IsBackground)
                    .Select(data =>
                    {
                        var width = data.Rect.z - data.Rect.x;
                        var height = data.Rect.w - data.Rect.y;
                        var rect = new TlwhRect(data.Rect.x, data.Rect.y, width, height);
                        return (Detection)new Detection<IObjectDetectionPipeline.DetectedObject>(data, rect, data.Score);
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
                if (_trackedObjectIds.Contains(instance.Track.TrackId) && instance.Track.DetectionState != TrackState.Removed) 
                    continue;
                _spawnedObjects.Remove(instance);
                ReleaseVoxelsForInstance(instance);
                Destroy(instance.Instance);
            }

            if (parent)
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

        private ObjectInstance GetBackgroundObjectInstance()
        {
            _background ??= new ObjectInstance
            {
                Instance = new GameObject("background")
                {
                    transform = { parent = parent }
                    
                }.Do(x => x.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity)),
                ObjectType = new ObjectType
                {
                    color = Color.black,
                }
            };

            return _background;
        }

        private void TrackAndDetectObject(Detection<IObjectDetectionPipeline.DetectedObject> detectionInfo, Track trackedResult, IObjectDetectionPipeline.DetectedObject detectedObjectReference)
        {
            if (detectionInfo.Ref.Vertices.Count == 0)
                return;
                
            if (trackedResult.DetectionState != TrackState.Tracked)
            {
                _trackedObjectIds.Add(trackedResult.TrackId);
                return;
            }

            if (!TryTrackObject(trackedResult, detectionInfo, out var instance))
                return;
                
            var objectInstance = instance.Instance;
            if (instance.ObjectType.appendTrackingIdToName)
                objectInstance.name = $"{detectionInfo.Ref.Label}#{trackedResult.TrackId}";
                
            if (detectionInfo.Ref.Vertices.Count < instance.ObjectType.minVertices)
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
                if (instance.ObjectType.expectedObjectRadius > 0 && vertex.z - detectedObjectReference.NearestZ > instance.ObjectType.expectedObjectRadius) continue;
                if (instance.ObjectType.maxDistance > 0 && vertex.z > instance.ObjectType.maxDistance) continue;
                if (vertexCount == 0) bounds = new Bounds(vertex, Vector3.zero);
                else bounds.Encapsulate(vertex);
                vertexCount++;
            }

            if (vertexCount < instance.ObjectType.minVertices)
            {
                _trackedObjectIds.Add(trackedResult.TrackId);
                instance.RevertReplacement();
                return;
            }

            ReleaseVoxelsForInstance(instance);

            var objectOrigin = bounds.center;
            var instanceTransform = objectInstance.transform;
            instanceTransform.parent = parent;
            instanceTransform.localPosition = objectOrigin;
            if (instance.ObjectType.addTriggerVolume)
            {
                instance.Trigger.size = bounds.size;
                instance.Trigger.enabled = true;
            }
            instance.FinalizeReplacement();
            instance.Instance.SetActive(true);

            if ((minVoxelSize > 0 || maxVoxelSize > 0) && !instance.ObjectType.positionTrackingOnly)
            {
                for (var vertexIndex = detectedObjectReference.Vertices.Count - 1; vertexIndex >= 0; vertexIndex--)
                    AddVoxel(detectedObjectReference, vertexIndex, instance, instanceTransform, objectOrigin, objectInstance);
            }

            _trackedObjectIds.Add(trackedResult.TrackId);
        }

        private void AddVoxel(
            IObjectDetectionPipeline.DetectedObject detectedObjectReference, 
            int vertexIndex, 
            ObjectInstance instance,
            Transform instanceTransform,
            Vector3 objectOrigin,
            GameObject objectInstance)
        {
            if (!Application.isPlaying)
                return;
            
            var vertex = detectedObjectReference.Vertices[vertexIndex];
            if (instance.ObjectType.expectedObjectRadius > 0 && vertex.z - detectedObjectReference.NearestZ > instance.ObjectType.expectedObjectRadius) return;
            if (instance.ObjectType.maxDistance > 0 && vertex.z > instance.ObjectType.maxDistance) return;
            var voxel = _voxelPool.Get();
            var inverseLerpSize = Mathf.InverseLerp(0.01f, 10f, vertex.z);
            var voxelSize = Mathf.Lerp(minVoxelSize, maxVoxelSize, inverseLerpSize);
            voxel.transform.parent = instanceTransform;
            voxel.transform.localPosition = vertex - objectOrigin;
            voxel.transform.localScale = Vector3.one * voxelSize;
            if (voxel.TryGetComponent<MeshRenderer>(out var ren))
            {
                if (instance.ObjectType.color.a > 0)
                {
                    ren.enabled = true;
                    ren.material.color = instance.ObjectType.color;
                }
                else ren.enabled = false;
            }

            if (!_voxelObjectMap.TryGetValue(objectInstance, out var voxels))
                voxels = _voxelObjectMap[objectInstance] = new List<GameObject>();
            voxels.Add(voxel);
        }

        private bool TryTrackObject(
            Track track, Detection<IObjectDetectionPipeline.DetectedObject> detection, out ObjectInstance instance)
        {
            instance = null;

            if (track.DetectionState != TrackState.Tracked || !_labelToPrefabs.TryGetValue(detection.Ref.Label, out var type))
                return false;

            if (!type.prefab)
            {
                type.prefab = new GameObject(detection.Ref.Label) { hideFlags = HideFlags.HideInHierarchy };
                type.prefab.SetActive(false);
            }

            var existing = FindBestOriginalObjectSource(track, detection, ref instance);
            if (existing)
                return true;

            var newObj = Instantiate(type.prefab, parent);
            newObj.name = detection.Ref.Label;
            newObj.hideFlags = HideFlags.None;
            newObj.SetActive(false);
            instance = new ObjectInstance
            {
                Track = track,
                Instance = newObj,
                ObjectType = type,
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
                x.ObjectType.label == detection.Ref.Label && x.Track.TrackId == newTrack.TrackId);
            
            if (match == null)
            {
                match = _spawnedObjects.FirstOrDefault(x =>
                    x.ObjectType.label == detection.Ref.Label &&
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

        private static bool NearCoordinates(ObjectInstance source, Detection<IObjectDetectionPipeline.DetectedObject> detection)
        {
            var distance = Vector2.Distance(detection.Ref.Origin, source.Instance.transform.localPosition);
            return distance <= source.ObjectType.expectedObjectRadius * 2f;
        }
    }
}
#endif