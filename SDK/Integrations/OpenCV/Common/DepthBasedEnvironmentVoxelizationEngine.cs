#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV
using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    #region DepthBasedEnvironmentVoxelizationEngine Class
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

        [Header("BYTETracker Settings")]
        [Tooltip("The maximum number of frames an object track can be lost before being removed.")]
        [Min(1)]
        [SerializeField]
        private int trackingBuffer = 60; // Updated default for long-term tracking

        [Tooltip("Score threshold to initiate a new track. Lower values start tracks more easily.")]
        [Range(0, 1f)]
        [SerializeField]
        private float trackingThreshold = 0.4f; // Updated default

        [Tooltip("Score threshold for high-confidence detections (matched first).")]
        [Range(0, 1f)]
        [SerializeField]
        private float highThreshold = 0.65f; // Kept default, seems reasonable

        [Tooltip("IOU threshold for matching low-score detections to existing tracks.")]
        [Range(0, 1f)]
        [SerializeField]
        private float matchThreshold = 0.7f; // Updated default, 0.8 was likely too high

        [SerializeField] private bool backgroundEnabled = true;
        


        
        private Dictionary<IObjectDetectionPipeline, VoxelizationData> _voxelizations;
        private ObjectInstance _background;
        // ** NEW: Map to hold voxels specifically for the background object, managed by the engine **
        private readonly Dictionary<GameObject, List<GameObject>> _engineBackgroundVoxelMap = new();
        

        private class VoxelizationData
        {
            private readonly DepthBasedEnvironmentVoxelizationEngine _engine; // Reference to parent engine

            public VoxelizationData(DepthBasedEnvironmentVoxelizationEngine engine, IObjectDetectionPipeline pipeline)
            {
                _engine = engine;
                _pipeline = pipeline;
            }

            private ByteTracker _tracker;
            private readonly IObjectDetectionPipeline _pipeline;
            private List<IObjectDetectionPipeline.DetectedObject> _lastFrameObjects;
            private Dictionary<string, BaseObjectType> _labelToPrefabs;
            // List of TRACKED object instances managed by THIS pipeline instance
            private readonly List<ObjectInstance> _spawnedObjects = new();
            // Map of voxels associated with TRACKED objects managed by THIS pipeline instance
            private readonly Dictionary<GameObject, List<GameObject>> _voxelObjectMap = new();
            private bool _frameDirty;

            public void Init()
            {
                _labelToPrefabs = _engine.objectTypes.ToDictionary(obj => obj.label, obj => (BaseObjectType)obj);
                InitializeTracker();
            }

            private void InitializeTracker()
            {
                if (!Application.isPlaying) return;
                _tracker?.Clear();
                _tracker = new ByteTracker(
                    rt: _engine.trackingBuffer,         // Use 'rt' for maxRetentionTime
                    t: _engine.trackingThreshold,      // Use 't' for trackThresh
                    h: _engine.highThreshold,          // Use 'h' for highThresh
                    m: _engine.matchThreshold);        // Use 'm' for matchThresh
            }

            public void OnEnable()
            {
                if(_pipeline != null) _pipeline.DetectableObjectsUpdated += HandleDetectableObjectsUpdated;
            }

            public void OnDisable()
            {
                 if(_pipeline != null) _pipeline.DetectableObjectsUpdated -= HandleDetectableObjectsUpdated;
            }

             // Renamed from ProcessFrame to avoid confusion with engine's ProcessFrames method name
            private void HandleDetectableObjectsUpdated(List<IObjectDetectionPipeline.DetectedObject> results)
            {
                 if (!_frameDirty) // Basic check to prevent overwriting if processing is slow
                 {
                    _lastFrameObjects = results;
                    _frameDirty = true;
                 }
            }

            /// <summary>
            /// Cleans up resources managed by this specific VoxelizationData instance (tracked objects only).
            /// </summary>
            public void DeleteAll()
            {
                foreach (var spawnedObject in _spawnedObjects.Where(spawnedObject => spawnedObject.Instance != null))
                {
                    // Release voxels associated with this tracked object
                    ReleaseVoxelsForInstance(spawnedObject);
                    // Destroy the tracked object's GameObject
                    if (Application.isPlaying) UnityEngine.Object.Destroy(spawnedObject.Instance);
                    else UnityEngine.Object.DestroyImmediate(spawnedObject.Instance);
                }
                _spawnedObjects.Clear();
                _voxelObjectMap.Clear(); // Clear the map for tracked objects
                _tracker?.Clear();
            }

             /// <summary>
             /// Processes the latest detection results for this pipeline, updates tracking, and manages object instances.
             /// Called by the engine's FixedUpdate -> ProcessFrames loop.
             /// </summary>
            public void ProcessLatestFrame() // Renamed from ProcessFrame
            {
                if (!_frameDirty || _lastFrameObjects == null)
                    return;

                try
                {
                    
                    // Check if background processing is enabled AND this frame contains background data
                    if (_engine.backgroundEnabled &&
                        _lastFrameObjects.Count > 0 &&
                        _lastFrameObjects[0].IsBackground)
                    {
                        var envInstance = _engine.GetBackgroundObjectInstance(); // Get the shared background instance
                        var lastFrameObject = _lastFrameObjects[0];

                        // ** CRITICAL CHANGE: Release background voxels via ENGINE before adding new ones **
                        _engine.ReleaseBackgroundVoxels();

                        if (envInstance != null && lastFrameObject.Vertices.Count > 0) // Ensure instance exists
                        {
                            for (var vertexIndex = 0; vertexIndex < lastFrameObject.Vertices.Count; vertexIndex++)
                            {
                                // AddVoxel will now correctly add these to the engine's map
                                AddVoxel(
                                    lastFrameObject,
                                    vertexIndex,
                                    envInstance,
                                    Vector3.zero);
                            }
                        }
                    }

                    
                    var objectDetections = _lastFrameObjects
                        .Where(data => !data.IsBackground && data.Rect != default) // Filter out background and invalid Rects
                        .Select(data =>
                        {
                            // Assuming data.Rect is Vector4(xMin, yMin, xMax, yMax)
                            float xMin = data.Rect.x;
                            float yMin = data.Rect.y;
                            float xMax = data.Rect.z;
                            float yMax = data.Rect.w;
                            float width = xMax - xMin;
                            float height = yMax - yMin;
                            // Validate dimensions
                            if (width <= 0 || height <= 0) return null; // Skip invalid rects
                            var rect = new TlwhRect(xMin, yMin, width, height); // Use xMin, yMin for top-left
                            return (Detection)new Detection<IObjectDetectionPipeline.DetectedObject>(
                                data,
                                rect,
                                data.Score);
                        })
                        .Where(d => d != null) // Filter out nulls from invalid rects
                        .ToList();

                    
                    var trackedResults = _tracker.Update(objectDetections);

                    
                    // Use a set for efficient lookup of processed track IDs this frame
                    HashSet<int> processedTrackIds = new HashSet<int>();
                    List<ObjectInstance> nextActiveInstances = new List<ObjectInstance>();

                    foreach (var trackedResult in trackedResults)
                    {
                        processedTrackIds.Add(trackedResult.TrackId); // Mark ID as seen this frame

                        // Attempt to process only if it's a valid Detection<T> and Tracked state
                        if (trackedResult.DetectionState == TrackState.Tracked &&
                            trackedResult.Detection is Detection<IObjectDetectionPipeline.DetectedObject> detectionInfo)
                        {
                             var detectedObjectReference = detectionInfo.Ref;
                             if (TrackAndDetectObject(detectionInfo, trackedResult, detectedObjectReference))
                             {
                                 // Successfully processed, find the instance and add to next active list
                                 var instance = _spawnedObjects.FirstOrDefault(inst => inst.Track?.TrackId == trackedResult.TrackId);
                                 if (instance != null)
                                 {
                                     nextActiveInstances.Add(instance);
                                 }
                             }
                             // If TrackAndDetectObject returns false, it means it failed processing (e.g., vertex count),
                             // but the track ID is still considered processed this frame.
                        }
                        else if (trackedResult.DetectionState == TrackState.Lost)
                        {
                             // Keep lost tracks in the list for potential reactivation
                             var instance = _spawnedObjects.FirstOrDefault(inst => inst.Track?.TrackId == trackedResult.TrackId);
                             if (instance != null)
                             {
                                 instance.Instance?.SetActive(false); // Optionally hide lost tracks
                                 nextActiveInstances.Add(instance); // Keep it in the active list
                             }
                        }
                        // Ignore New/Removed states here as they are handled internally or in the removal step
                    }

                    
                    // Iterate through the current spawned objects and remove those whose track IDs weren't in the tracker's output this frame
                    for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
                    {
                        var instance = _spawnedObjects[i];
                        if (instance.Track == null || !processedTrackIds.Contains(instance.Track.TrackId))
                        {
                            // This instance's track ID was not returned by the tracker, remove it
                            ReleaseVoxelsForInstance(instance);
                            if (instance.Instance != null)
                            {
                                 if (Application.isPlaying) UnityEngine.Object.Destroy(instance.Instance);
                                 else UnityEngine.Object.DestroyImmediate(instance.Instance);
                            }
                            // Remove from _spawnedObjects is implicitly handled by rebuilding with nextActiveInstances
                        }
                    }

                    // Update the main list to only contain currently active/lost instances
                    _spawnedObjects.Clear();
                    _spawnedObjects.AddRange(nextActiveInstances);


                    
                    if (_engine.parent && _spawnedObjects.Count > 0)
                    {
                        var ordered = _spawnedObjects.OrderBy(x => x.Track?.TrackId ?? int.MaxValue).ToList(); // Handle potential null track
                        for(int i=0; i < ordered.Count; ++i)
                        {
                            if (ordered[i].Instance != null) // Check if instance exists
                            {
                                try { // Add try-catch for safety if object was destroyed unexpectedly
                                    ordered[i].Instance.transform.SetSiblingIndex(i);
                                } catch (Exception ex) {
                                    Debug.LogWarning($"Failed to set sibling index for {ordered[i].Instance?.name}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error during VoxelizationData.ProcessLatestFrame: {ex.Message}\n{ex.StackTrace}");
                }
                finally // Ensure frame state is reset
                {
                    _lastFrameObjects = null; // Clear processed frame data
                    _frameDirty = false;
                }
            }


            /// <summary>
            /// Releases voxels associated with a specific TRACKED object instance managed by this VoxelizationData.
            /// This method is now private and only called internally or by DeleteAll for tracked objects.
            /// </summary>
            private void ReleaseVoxelsForInstance(ObjectInstance spawnedObject) // Keep private
            {
                 if (spawnedObject == null || spawnedObject.Instance == null) return;

                 // *** IMPORTANT: Only release from this instance's map, NOT the engine's background map ***
                 if (_voxelObjectMap.TryGetValue(spawnedObject.Instance, out var voxels))
                 {
                     for (var index = voxels.Count - 1; index >= 0; index--)
                     {
                         var voxel = voxels[index];
                         if(voxel != null) _engine._voxelPool.Release(voxel); // Use engine's pool
                     }
                     voxels.Clear();
                     _voxelObjectMap.Remove(spawnedObject.Instance);
                 }
            }

            /// <summary>
            /// Creates or updates a tracked object instance based on detection and track info.
            /// </summary>
            /// <returns>True if instance was processed (created/updated/hidden), false on failure.</returns>
            private bool TrackAndDetectObject(
                Detection<IObjectDetectionPipeline.DetectedObject> detectionInfo,
                Track trackedResult,
                IObjectDetectionPipeline.DetectedObject detectedObjectReference)
            {
                
                if (detectionInfo?.Ref == null || trackedResult == null) return false;
                if (detectedObjectReference.Vertices == null || detectedObjectReference.Vertices.Count == 0) return false;

                
                if (!TryTrackObject(detectionInfo.Ref.Label, trackedResult, detectionInfo, out var instance) || instance == null) {
                    return false;
                }
                if (instance.Instance == null) { // Check if instance's GameObject is valid
                    Debug.LogWarning($"TrackAndDetectObject: Instance {instance.Label} (TrackID {trackedResult.TrackId}) has null GameObject. Removing.");
                    _spawnedObjects.Remove(instance); // Clean up list
                    return false;
                }

                
                if (detectedObjectReference.Vertices.Count < instance.Type.minVertices) {
                    instance.RevertReplacement();
                    instance.Instance.SetActive(false);
                    return true; // Handled by hiding
                }

                
                Bounds bounds = default;
                int vertexCount = 0;
                float nearestZ = detectedObjectReference.NearestZ > 0 ? detectedObjectReference.NearestZ : float.MaxValue;
                List<Vector3> validVertices = ListPool<Vector3>.Get(); // Use pool for temporary list
                try {
                    for (var i = 0; i < detectedObjectReference.Vertices.Count; i++) {
                        var vertex = detectedObjectReference.Vertices[i];
                        if (instance.Type.expectedObjectRadius > 0 && nearestZ != float.MaxValue && vertex.z - nearestZ > instance.Type.expectedObjectRadius) continue;
                        if (instance.Type.maxDistance > 0 && vertex.z > instance.Type.maxDistance) continue;

                        if (vertexCount == 0) bounds = new Bounds(vertex, Vector3.zero);
                        else bounds.Encapsulate(vertex);
                        validVertices.Add(vertex); // Store valid vertex
                        vertexCount++;
                    }

                    
                    if (vertexCount < instance.Type.minVertices) {
                        instance.RevertReplacement();
                        instance.Instance.SetActive(false);
                        return true; // Handled by hiding
                    }

                    
                    ReleaseVoxelsForInstance(instance); // Release OLD voxels for this tracked object

                    var objectOrigin = bounds.center;
                    var instanceTransform = instance.Instance.transform;
                    instanceTransform.SetParent(_engine.parent, false); // Ensure parent and worldPositionStays
                    instanceTransform.localPosition = objectOrigin;
                    instanceTransform.localRotation = detectedObjectReference.Rotation;

                    if (instance.Type.addTriggerVolume && instance.Trigger != null) {
                        instance.Trigger.center = Vector3.zero;
                        instance.Trigger.size = Vector3.Max(bounds.size, Vector3.one * 0.01f); // Prevent zero size collider
                        instance.Trigger.enabled = true;
                    } else if (instance.Trigger != null) {
                        instance.Trigger.enabled = false;
                    }

                    instance.FinalizeReplacement();
                    instance.Instance.SetActive(true);

                    
                    if ((_engine.minVoxelSize > 0 || _engine.maxVoxelSize > 0) && !instance.Type.positionTrackingOnly) {
                        if (!_voxelObjectMap.ContainsKey(instance.Instance)) {
                            _voxelObjectMap[instance.Instance] = new List<GameObject>();
                        }
                        // Iterate through the VALID vertices stored earlier
                        foreach (var vertex in validVertices) {
                            // Call AddVoxel using the valid vertex - Note AddVoxel still needs index conceptually if using raw list
                            // Let's modify AddVoxel to take the vertex directly? Or just pass 0 as index since we filtered?
                            // Passing vertex is cleaner if AddVoxel can be adapted. Assuming AddVoxel checks instance type.
                            AddVoxelDirect(vertex, detectedObjectReference.NearestZ, instance, objectOrigin); // Use direct vertex
                        }
                    }
                } finally {
                    ListPool<Vector3>.Release(validVertices); // Release pooled list
                }

                return true; // Processed successfully
            }


            /// <summary>
            /// Adds a single voxel based on a pre-filtered vertex.
            /// Determines if it belongs to the background (engine map) or a tracked object (local map).
            /// </summary>
            private void AddVoxelDirect(Vector3 vertex, float nearestZForObject, ObjectInstance instance, Vector3 objectOrigin)
            {
                if (!Application.isPlaying || instance?.Instance == null) return;

                // Filters were already applied by caller (TrackAndDetectObject)
                // We just need to create and place the voxel.

                GameObject voxel = null;
                try {
                    voxel = _engine._voxelPool.Get();
                    if (voxel == null) return;

                    var inverseLerpSize = Mathf.InverseLerp(0.01f, 10f, Mathf.Max(0.01f, vertex.z));
                    var voxelSize = Mathf.Lerp(_engine.minVoxelSize, _engine.maxVoxelSize, inverseLerpSize);

                    var voxelTransform = voxel.transform;
                    voxelTransform.SetParent(instance.Instance.transform, false); // Parent should exist, worldPositionStays=false

                    // Calculate local position relative to the parent's rotation
                    voxelTransform.localPosition = Quaternion.Inverse(instance.Instance.transform.localRotation) * (vertex - objectOrigin);
                    voxelTransform.localScale = Vector3.one * Mathf.Max(0.0001f, voxelSize); // Ensure non-zero scale
                    voxelTransform.localRotation = Quaternion.identity; // Ensure default rotation

                    if (voxel.TryGetComponent<MeshRenderer>(out var ren))
                    {
                        ren.enabled = instance.Type.color.a > 0; // Use alpha to control visibility
                        if(ren.enabled) {
                            // Consider using MaterialPropertyBlock for performance if many voxels share materials
                             // A simple optimization: cache the material if possible
                            if (ren.sharedMaterial != null) ren.sharedMaterial.color = instance.Type.color;
                            else if (ren.material != null) ren.material.color = instance.Type.color; // Fallback to instance material
                        }
                    }

                    
                    if (instance == _engine._background) { // Check if it's the background object
                        // Use the engine's map for background voxels
                        if (!_engine._engineBackgroundVoxelMap.TryGetValue(instance.Instance, out var engineVoxels)) {
                            engineVoxels = _engine._engineBackgroundVoxelMap[instance.Instance] = new List<GameObject>();
                        }
                        engineVoxels.Add(voxel);
                    } else { // It's a regular tracked object
                        // Use this VoxelizationData instance's map
                        if (_voxelObjectMap.TryGetValue(instance.Instance, out var voxels)) {
                            voxels.Add(voxel);
                        } else {
                            // Should have been created in TrackAndDetectObject, but handle defensively
                            Debug.LogWarning($"Voxel list missing for tracked instance {instance.Instance.name}. Creating new list.");
                            _voxelObjectMap[instance.Instance] = new List<GameObject> { voxel };
                        }
                    }
                } catch (Exception ex) {
                    Debug.LogError($"Error adding voxel direct: {ex.Message}\n{ex.StackTrace}");
                    if (voxel != null) _engine._voxelPool.Release(voxel);
                }
            }

             /// <summary>
             /// Original AddVoxel using index - less efficient now but kept for background processing consistency.
             /// Calls AddVoxelDirect after getting the vertex.
             /// </summary>
             private void AddVoxel(IObjectDetectionPipeline.DetectedObject detectedObjectReference,
                 int vertexIndex,
                 ObjectInstance instance,
                 Vector3 objectOrigin)
            {
                 if (instance?.Instance == null || detectedObjectReference?.Vertices == null || vertexIndex < 0 || vertexIndex >= detectedObjectReference.Vertices.Count)
                     return;
                 var vertex = detectedObjectReference.Vertices[vertexIndex];
                 // Uses the nearestZ from the whole detectedObjectReference, which is correct for background/object filtering here
                 AddVoxelDirect(vertex, detectedObjectReference.NearestZ, instance, objectOrigin);
            }


            /// <summary> Finds or creates an ObjectInstance for a given track. </summary>
            private bool TryTrackObject(
                string label, Track track, Detection<IObjectDetectionPipeline.DetectedObject> detection,
                out ObjectInstance instance)
            {
                instance = null;
                if (detection?.Ref == null || track == null) return false;

                // 1. Find by Track ID
                instance = _spawnedObjects.FirstOrDefault(x => x.Track != null && x.Track.TrackId == track.TrackId);
                if (instance != null) {
                     if(instance.Instance == null) { // Stale instance check
                         _spawnedObjects.Remove(instance); instance = null; return false;
                     }
                    instance.Track = track; // Update track reference
                    instance.Label = label;
                    return true;
                }

                // 2. Re-identification
                instance = _spawnedObjects
                    .Where(x => x.Track != null && x.Track.DetectionState == TrackState.Lost && x.Label == label && x.Instance != null)
                    .OrderBy(x => Vector3.Distance(detection.Ref.Origin, x.Instance.transform.localPosition)) // Order by distance
                    .FirstOrDefault(x => NearCoordinates(x, detection)); // Check proximity

                if (instance != null) {
                     if(instance.Instance == null) { // Stale instance check
                         _spawnedObjects.Remove(instance); instance = null; return false;
                     }
                    instance.ReplaceWith(track); // Updates Track reference internally
                    instance.Label = label;
                    return true;
                }

                // 3. Create New
                if (!_labelToPrefabs.TryGetValue(label, out var type) && !_engine.detectOtherObjects) return false; // Check if type is allowed
                bool isOtherType = type == null;
                type ??= _engine.baseObjectType; // Assign base type if allowed but not explicitly configured
                if (type == null) return false; // No base type available either

                GameObject prefabToUse = type.prefab ?? (isOtherType ? _engine.baseObjectType?.prefab : null); // Determine prefab

                GameObject newObjInstance;
                string instanceName = $"{label}_{track.TrackId}"; // Generate name (ensure track ID is valid here)
                try {
                    if (prefabToUse != null) newObjInstance = Instantiate(prefabToUse, _engine.parent);
                    else {
                        newObjInstance = new GameObject(instanceName);
                        if(_engine.parent != null) newObjInstance.transform.SetParent(_engine.parent, false);
                    }
                } catch (Exception e) {
                    Debug.LogError($"Failed to instantiate for {instanceName}: {e.Message}");
                    return false;
                }

                newObjInstance.name = instanceName;
                newObjInstance.hideFlags = HideFlags.None;
                newObjInstance.SetActive(false);

                // Create the wrapper instance
                instance = new ObjectInstance {
                    Track = track, // Assign the track reference
                    Instance = newObjInstance,
                    Type = type,
                    Label = label
                };

                // Add collider if needed
                if (type.addTriggerVolume) {
                    if (!instance.Instance.TryGetComponent<BoxCollider>(out var bc)) bc = instance.Instance.AddComponent<BoxCollider>();
                    bc.isTrigger = true; bc.enabled = false; instance.Trigger = bc;
                }

                _spawnedObjects.Add(instance); // Add to managed list
                return true;
            }

        }
        


        
        private readonly ObjectPool<GameObject> _voxelPool = new(() =>
        {
            if (!Application.isPlaying) return null;
            try {
                var voxel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var collider = voxel.GetComponent<Collider>();
                if (collider != null) Destroy(collider); // Remove collider immediately
                //voxel.hideFlags = HideFlags.HideInHierarchy;
                return voxel;
            } catch { return null; }
        },
        actionOnGet: go => { if (go) go.SetActive(true); },
        actionOnRelease: go => {
            if (!go) return;
            go.SetActive(false);
            go.transform.SetParent(null, false); // Unparent
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        },
        actionOnDestroy: go => { if (go) Destroy(go); },
        collectionCheck: true, maxSize: 10000);
        


        
        private class ObjectInstance
        {
            internal Track _previousTrack;
            public string Label;
            public Track Track;
            public GameObject Instance;
            public BaseObjectType Type;
            public BoxCollider Trigger;

            public void ReplaceWith(Track newTrack) {
                 if (Track != null && Track.TrackId != newTrack.TrackId) _previousTrack = Track;
                 else if (Track == null) _previousTrack = null;
                Track = newTrack;
            }
            public void FinalizeReplacement() { _previousTrack = null; }
            public void RevertReplacement() {
                if (_previousTrack != null) {
                     Track = _previousTrack;
                     Track?.Reactivate(Track.LatestRect, Track.Score);
                     _previousTrack = null;
                }
            }
        }
        


        
        private void Awake()
        {
            var pipelines = GetComponents<IObjectDetectionPipeline>();
            if(pipelines == null || pipelines.Length == 0) {
                Debug.LogError("No IObjectDetectionPipeline components found!", this);
                _voxelizations = new Dictionary<IObjectDetectionPipeline, VoxelizationData>();
                enabled = false; // Disable self if no pipeline
                return;
            }
            _voxelizations = pipelines.ToDictionary(p => p, p => new VoxelizationData(this, p).Do(vd => vd.Init()));
        }

        private void OnValidate()
        {
             if (Application.isPlaying && _voxelizations != null) InitializeTrackers();
        }

        private void OnEnable()
        {
            if (_voxelizations == null) Awake(); // Try init if null
            if (_voxelizations == null) { enabled = false; return; } // Disable if still null
            InitializeTrackers();
            EnableAll();
        }

        private void OnDisable()
        {
            DisableAll(); // Stop listening first
            DeleteAll();  // Then clean up state
        }

        private void OnDestroy()
        {
            _voxelPool?.Dispose(); // Dispose the object pool
            // Voxelizations dictionary might be null if Awake failed
            _voxelizations?.Clear();
             // Clean up background object thoroughly if it exists
             if (_background?.Instance != null) {
                  if (Application.isPlaying) Destroy(_background.Instance);
                  else DestroyImmediate(_background.Instance);
             }
             _background = null;
             _engineBackgroundVoxelMap.Clear();
        }

        private void FixedUpdate()
        {
            if (Application.isPlaying && _voxelizations != null) ProcessFrames();
        }

        private void EnableAll()
        {
            if (_voxelizations is null) return;
            foreach (var kvp in _voxelizations) kvp.Value.OnEnable();
        }

        private void DisableAll()
        {
            if (_voxelizations is null) return;
            foreach (var kvp in _voxelizations) kvp.Value.OnDisable();
        }

        /// <summary> Releases voxels associated ONLY with the background object. </summary>
        internal void ReleaseBackgroundVoxels() // Made internal for VoxelizationData access
        {
            if (_background?.Instance != null && _engineBackgroundVoxelMap.TryGetValue(_background.Instance, out var backgroundVoxels))
            {
                for (int i = backgroundVoxels.Count - 1; i >= 0; i--) {
                    if(backgroundVoxels[i] != null) _voxelPool.Release(backgroundVoxels[i]);
                }
                backgroundVoxels.Clear(); // Clear the list after releasing
                // Keep the map entry for next frame unless background is fully deleted
            }
        }

        private void DeleteAll()
        {
            // Destroy background object and release ITS voxels
            if (_background?.Instance != null)
            {
                 // ** Use the helper method **
                 ReleaseBackgroundVoxels();
                 // ** Clean up map entry **
                 _engineBackgroundVoxelMap.Remove(_background.Instance); // Remove entry from engine map

                if (Application.isPlaying) UnityEngine.Object.Destroy(_background.Instance);
                else UnityEngine.Object.DestroyImmediate(_background.Instance);
                _background = null;
            }
             // Also clear the map in case the instance was null but map entry remained
             _engineBackgroundVoxelMap.Clear();


            // Delete objects managed by each pipeline
            if (_voxelizations != null)
            {
                foreach (var kvp in _voxelizations)
                    kvp.Value.DeleteAll(); // This correctly handles tracked objects now
            }
        }

        private void InitializeTrackers()
        {
            if (!Application.isPlaying || _voxelizations is null) return;
            foreach (var kvp in _voxelizations) kvp.Value.Init();
        }

        private void ProcessFrames()
        {
            if (_voxelizations is null) return;
            // Process latest frame data for each pipeline
            foreach (var kvp in _voxelizations) kvp.Value.ProcessLatestFrame();
        }

        private ObjectInstance GetBackgroundObjectInstance()
        {
            // Check if instance still exists in the scene
            if (_background == null || _background.Instance == null)
            {
                // Ensure previous is cleaned up if somehow reference remains but object is gone
                 if (_background?.Instance != null) {
                     if(Application.isPlaying) Destroy(_background.Instance);
                     else DestroyImmediate(_background.Instance);
                 }

                _background = new ObjectInstance {
                    Instance = new GameObject("background_voxels") { transform = { parent = this.parent } }
                                .Do(x => x.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity)),
                    Type = new ObjectType { // Use ObjectType for consistency, can configure if needed
                        color = Color.clear, // Default to invisible voxels for background
                        expectedObjectRadius = Mathf.Infinity,
                        maxDistance = Mathf.Infinity,
                        minVertices = 1,
                        positionTrackingOnly = true // Background doesn't need tracking state usually
                    },
                    Label = "background" // Assign label
                };
                 // Ensure the background object exists in the engine's map, even if no voxels yet
                 if (!_engineBackgroundVoxelMap.ContainsKey(_background.Instance)) {
                     _engineBackgroundVoxelMap[_background.Instance] = new List<GameObject>();
                 }
            }
            return _background;
        }

        /// <summary>
        /// Checks if a detection is spatially near an existing object instance.
        /// Used as a simple heuristic for re-identification.
        /// </summary>
        private static bool NearCoordinates(ObjectInstance source, Detection<IObjectDetectionPipeline.DetectedObject> detection)
        {
            if (source?.Instance == null || detection?.Ref == null) return false;
            Vector3 detectionOrigin = detection.Ref.Origin;
            Vector3 instancePosition = source.Instance.transform.localPosition;
            var distance = Vector3.Distance(detectionOrigin, instancePosition);
            float checkRadiusMultiplier = 2.0f;
            float baseRadius = source.Type?.expectedObjectRadius ?? 0.5f;
            float checkRadius = Mathf.Max(0.2f, baseRadius * checkRadiusMultiplier);
            return distance <= checkRadius;
        }
        

    } // End DepthBasedEnvironmentVoxelizationEngine Class
    #endregion


    
    #region BYTETracker Implementation (Integrated)

    public enum TrackState { New, Tracked, Lost, Removed }

    public struct TlwhRect {
        public float X, Y, Width, Height;
        public float Left => X; public float Top => Y; public float Right => X + Width; public float Bottom => Y + Height; public float Area => Width * Height;
        public Vector2 TopLeft => new Vector2(X, Y); public Vector2 BottomRight => new Vector2(Right, Bottom);
        public TlwhRect(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; }
        public float IoU(TlwhRect other) {
             float w1 = Mathf.Max(0, this.Width); float h1 = Mathf.Max(0, this.Height); float w2 = Mathf.Max(0, other.Width); float h2 = Mathf.Max(0, other.Height);
             float iL = Mathf.Max(this.Left, other.Left); float iT = Mathf.Max(this.Top, other.Top);
             float iR = Mathf.Min(this.Left + w1, other.Left + w2); float iB = Mathf.Min(this.Top + h1, other.Top + h2);
             float iW = Mathf.Max(0, iR - iL); float iH = Mathf.Max(0, iB - iT);
             float iArea = iW * iH; float uArea = (w1 * h1) + (w2 * h2) - iArea;
             return (uArea > 1e-5f) ? (iArea / uArea) : 0f;
        }
        public Vector4 ToTlbr() => new Vector4(X, Y, X + Width, Y + Height);
    }

    public abstract class Detection { public TlwhRect Rect { get; protected set; } public float Score { get; protected set; } }
    public class Detection<T> : Detection { public T Ref { get; } public Detection(T r, TlwhRect rt, float s) { Ref = r; Rect = rt; Score = s; } }

    public class Track {
        public static int _nextTrackId = 1;
        public int TrackId { get; internal set; } = -1; public bool IsActivated { get; internal set; } = false;
        public TrackState DetectionState { get; set; } = TrackState.New; public int StartFrame { get; private set; } = -1;
        public int FrameId { get; private set; } = -1; public int TrackletLength { get; private set; } = 0;
        public int TimeSinceUpdate { get; private set; } = 0; public TlwhRect LatestRect { get; private set; }
        public float Score { get; private set; } public Detection Detection { get; private set; }
        private KalmanFilter _kalmanFilter; private float[] _mean; private float[] _covariance;
        public Track(TlwhRect tlwh, float score, Detection detection = null) { LatestRect = tlwh; Score = score; Detection = detection; _kalmanFilter = new KalmanFilter(); (_mean, _covariance) = _kalmanFilter.Initiate(TlwhToXyah(tlwh)); }
        public void Predict() { if (_mean != null && _covariance != null) (_mean, _covariance) = _kalmanFilter.Predict(_mean, _covariance); if (IsActivated) TrackletLength++; TimeSinceUpdate++; }
        public void Update(Track detTrack, int frameId) { if(detTrack == null) return; LatestRect = detTrack.LatestRect; Score = detTrack.Score; Detection = detTrack.Detection; DetectionState = TrackState.Tracked; IsActivated = true; TimeSinceUpdate = 0; if (_mean != null && _covariance != null) (_mean, _covariance) = _kalmanFilter.Update(_mean, _covariance, TlwhToXyah(this.LatestRect)); FrameId = frameId; }
        public void Reactivate(TlwhRect nr, float ns, int fId, bool newId = false, Detection nDet = null) { LatestRect = nr; Score = ns; Detection = nDet; DetectionState = TrackState.Tracked; IsActivated = true; TimeSinceUpdate = 0; if (_mean != null && _covariance != null) (_mean, _covariance) = _kalmanFilter.Update(_mean, _covariance, TlwhToXyah(nr)); FrameId = fId; if (newId) { TrackId = _nextTrackId++; StartFrame = fId; TrackletLength = 1; } }
        internal void Reactivate(TlwhRect nr, float ns) { LatestRect = nr; Score = ns; DetectionState = TrackState.Tracked; IsActivated = true; TimeSinceUpdate = 0; if (_mean != null && _covariance != null) (_mean, _covariance) = _kalmanFilter.Update(_mean, _covariance, TlwhToXyah(nr)); }
        public void MarkAsLost() { if (this.DetectionState == TrackState.Tracked) this.DetectionState = TrackState.Lost; }
        public void MarkAsRemoved() { this.DetectionState = TrackState.Removed; }
        public void Activate(int frameId) { if (!IsActivated) { TrackId = _nextTrackId++; IsActivated = true; DetectionState = TrackState.Tracked; StartFrame = frameId; FrameId = frameId; TrackletLength = 1; TimeSinceUpdate = 0; } }
        private static float[] TlwhToXyah(TlwhRect t) { float w=Mathf.Max(1e-5f,t.Width); float h=Mathf.Max(1e-5f,t.Height); return new[]{ t.X+w/2f, t.Y+h/2f, w/h, h }; }
        public TlwhRect MeanToTlwh() { if (_mean==null||_mean.Length<4) return default; float h=Mathf.Max(1e-5f,_mean[3]); float ar=Mathf.Max(1e-5f,_mean[2]); float w=ar*h; return new TlwhRect(_mean[0]-w/2f, _mean[1]-h/2f, w, h); }
        public static float[,] IouDistance(List<Track> a, List<Track> b) { int nA=a?.Count??0; int nB=b?.Count??0; var m=new float[nA,nB]; if(nA==0||nB==0)return m; for(int i=0;i<nA;i++) for(int j=0;j<nB;j++) m[i,j]=1f-a[i].MeanToTlwh().IoU(b[j].LatestRect); return m;}
    }

     public class ByteTracker {
         private readonly List<Track> _trackedTracks = new List<Track>(); private readonly List<Track> _lostTracks = new List<Track>(); private readonly List<Track> _removedTracks = new List<Track>();
         private int _frameCounter = 0; private readonly float _trackThresh; private readonly float _highThresh; private readonly float _matchThresh; private readonly int _maxRetentionTime;
         public ByteTracker(float t=0.4f, float h=0.65f, float m=0.7f, int rt=60) { _trackThresh=t; _highThresh=h; _matchThresh=m; _maxRetentionTime=rt; Track._nextTrackId = 1; }
         public void Clear() { _trackedTracks.Clear(); _lostTracks.Clear(); _removedTracks.Clear(); _frameCounter = 0; Track._nextTrackId = 1; }
         public List<Track> Update(List<Detection> detections) {
             _frameCounter++; List<Track> actT=new List<Track>(), reactT=new List<Track>(), lostP=new List<Track>(), newP=new List<Track>(), remL=new List<Track>();
             foreach (var t in _trackedTracks) { t.Predict(); if (t.DetectionState == TrackState.Tracked) actT.Add(t); else lostP.Add(t); } // If state is not Tracked (e.g. Lost), move to pool
             foreach (var t in _lostTracks) { t.Predict(); if (t.TimeSinceUpdate>_maxRetentionTime) { t.MarkAsRemoved(); remL.Add(t); } else lostP.Add(t); } _removedTracks.AddRange(remL);
             List<Track> hiD=new List<Track>(), loD=new List<Track>(); foreach(var d in detections){var t=new Track(d.Rect, d.Score, d); if(d.Score>=_highThresh) hiD.Add(t); else if(d.Score>=_trackThresh) loD.Add(t);}
             float iouDistTh=1f-_matchThresh; (var m1, var uActIdx, var uHiIdx) = LinearAssignment(Track.IouDistance(actT, hiD), iouDistTh);
             foreach(var m in m1) actT[m.TrackIndex].Update(hiD[m.DetectionIndex], _frameCounter);
             List<Track> remLost=new List<Track>(lostP); List<Track> remActU=new List<Track>(); foreach(var i in uActIdx) remActU.Add(actT[i]);
             (var m2, var uRemActIdx, var uLoIdx) = LinearAssignment(Track.IouDistance(remActU, loD), 0.5f); // Use 0.5 distance for low score
             foreach(var m in m2) remActU[m.TrackIndex].Update(loD[m.DetectionIndex], _frameCounter);
             List<Track> newLost=new List<Track>(); foreach(var i in uRemActIdx) { var t=remActU[i]; t.MarkAsLost(); newLost.Add(t); }
             List<Track> remHiD=uHiIdx.Select(i=>hiD[i]).ToList(); (var m3, var uRemLostIdx, var fUHiIdx) = LinearAssignment(Track.IouDistance(remLost, remHiD), iouDistTh);
             foreach(var m in m3) { var t=remLost[m.TrackIndex]; t.Reactivate(remHiD[m.DetectionIndex].LatestRect, remHiD[m.DetectionIndex].Score, _frameCounter, false, remHiD[m.DetectionIndex].Detection); reactT.Add(t); }
             List<Track> fLost=new List<Track>(); foreach(var i in uRemLostIdx) fLost.Add(remLost[i]);
             List<Track> fNew=new List<Track>(); foreach(var i in fUHiIdx) { var t=remHiD[i]; if(t.Score>=_trackThresh) { t.Activate(_frameCounter); fNew.Add(t); } }
             _trackedTracks.Clear(); _trackedTracks.AddRange(actT.Where(t=>t.FrameId==_frameCounter && t.DetectionState==TrackState.Tracked)); _trackedTracks.AddRange(reactT); _trackedTracks.AddRange(fNew);
             _lostTracks.Clear(); _lostTracks.AddRange(newLost); _lostTracks.AddRange(fLost);
             List<Track> output=new List<Track>(); output.AddRange(_trackedTracks.Where(t=>t.DetectionState!=TrackState.Removed)); output.AddRange(_lostTracks.Where(t=>t.DetectionState!=TrackState.Removed)); return output;
         }
         private (List<Match> M, List<int> uT, List<int> uD) LinearAssignment(float[,] costs, float dTh) {
             var M=new List<Match>(); if(costs==null||costs.Length==0){int nT=costs?.GetLength(0)??0; int nD=costs?.GetLength(1)??0; return(M, Enumerable.Range(0,nT).ToList(), Enumerable.Range(0,nD).ToList());}
             int nTrk=costs.GetLength(0); int nDet=costs.GetLength(1); var uT=Enumerable.Range(0,nTrk).ToList(); var uD=Enumerable.Range(0,nDet).ToList(); if(nTrk==0||nDet==0) return(M,uT,uD);
             var pM=new List<(float C,int T,int D)>(); for(int i=0;i<nTrk;i++)for(int j=0;j<nDet;j++)if(costs[i,j]<dTh)pM.Add((costs[i,j],i,j));
             pM.Sort((a,b)=>a.C.CompareTo(b.C)); var aT=new HashSet<int>(); var aD=new HashSet<int>();
             foreach(var p in pM) if(!aT.Contains(p.T)&&!aD.Contains(p.D)){ M.Add(new Match{TrackIndex=p.T, DetectionIndex=p.D}); aT.Add(p.T); aD.Add(p.D);}
             uT.RemoveAll(aT.Contains); uD.RemoveAll(aD.Contains); return(M,uT,uD);
         }
         internal struct Match { public int TrackIndex; public int DetectionIndex; }
     }

    // ... (KalmanFilter Class - Unchanged) ...
    public class KalmanFilter {
        private const int StateDim=8, MeasDim=4; private readonly float _stdWPos, _stdWVel; private readonly float[] _fMat, _hMat;
        public KalmanFilter(float sp=1f/20f, float sv=1f/160f) { _stdWPos=sp; _stdWVel=sv; _fMat=Id(StateDim); _fMat[0*StateDim+4]=1f;_fMat[1*StateDim+5]=1f;_fMat[2*StateDim+6]=1f;_fMat[3*StateDim+7]=1f; _hMat=new float[MeasDim*StateDim]; for(int i=0;i<MeasDim;i++)_hMat[i*StateDim+i]=1f;}
        public(float[] m,float[] c) Initiate(float[] z){ if(z==null||z.Length!=MeasDim) throw new ArgumentException("Bad measurement"); var m=new float[StateDim]; Array.Copy(z,0,m,0,MeasDim); var s=new float[StateDim]; s[0]=s[1]=s[3]=2*_stdWPos*z[3];s[2]=1e-2f;s[4]=s[5]=s[7]=10*_stdWVel*z[3];s[6]=1e-5f; var c=Diag(StateDim,s); for(int i=0;i<StateDim;i++)c[i*StateDim+i]=s[i]*s[i]; return(m,c);}
        public(float[] m,float[] c) Predict(float[] m, float[] c){ if(m==null||c==null) return(m,c); float sp=_stdWPos*m[3],sv=_stdWVel*m[3]; var q_s=new float[]{sp,sp,1e-2f,sp,sv,sv,1e-5f,sv}; var Q=Diag(StateDim,q_s); for(int i=0;i<StateDim;i++)Q[i*StateDim+i]=q_s[i]*q_s[i]; var pM=Mult(m,Tr(_fMat,StateDim,StateDim),1,StateDim,StateDim); var p_ft=Mult(c,Tr(_fMat,StateDim,StateDim),StateDim,StateDim,StateDim); var pC=Mult(_fMat,p_ft,StateDim,StateDim,StateDim); Add(pC,Q,StateDim,StateDim); return(pM,pC);}
        public(float[] m,float[] c) Update(float[] m,float[] c,float[] z){ if(m==null||c==null||z==null||z.Length!=MeasDim) return(m,c); float sm=_stdWPos*m[3]; var r_s=new float[]{sm,sm,1e-1f,sm}; var R=Diag(MeasDim,r_s); for(int i=0;i<MeasDim;i++)R[i*MeasDim+i]=r_s[i]*r_s[i]; var pM=Mult(m,Tr(_hMat,MeasDim,StateDim),1,StateDim,MeasDim); var p_ht=Mult(c,Tr(_hMat,MeasDim,StateDim),StateDim,StateDim,MeasDim); var h_p_ht=Mult(_hMat,p_ht,MeasDim,StateDim,MeasDim); var S=h_p_ht; Add(S,R,MeasDim,MeasDim); var S_inv=Inv(S,MeasDim); if(S_inv==null) return(m,c); var K=Mult(p_ht,S_inv,StateDim,MeasDim,MeasDim); var y=new float[MeasDim]; for(int i=0;i<MeasDim;i++)y[i]=z[i]-pM[i]; var corr=Mult(y,Tr(K,StateDim,MeasDim),1,MeasDim,StateDim); var uM=new float[StateDim]; for(int i=0;i<StateDim;i++)uM[i]=m[i]+corr[i]; var k_h=Mult(K,_hMat,StateDim,MeasDim,StateDim); var i_kh=Id(StateDim); Sub(i_kh,k_h,StateDim,StateDim); var uC=Mult(i_kh,c,StateDim,StateDim,StateDim); return(uM,uC);}
        private static float[] Id(int d){var r=new float[d*d];for(int i=0;i<d;i++)r[i*d+i]=1f;return r;} private static float[] Diag(int d,float[]v){var r=new float[d*d];int k=Math.Min(d,v?.Length??0); for(int i=0;i<k;i++)if(v!=null)r[i*d+i]=v[i];return r;} private static float[] Tr(float[] M,int r,int c){if(M==null||M.Length!=r*c)throw new ArgumentException();var R=new float[c*r];for(int i=0;i<r;i++)for(int j=0;j<c;j++)R[j*r+i]=M[i*c+j];return R;}
        private static float[] Mult(float[]A,float[]B,int rA,int cA,int cB){int rB=cA;if(A==null||B==null||A.Length!=rA*cA||B.Length!=rB*cB)throw new ArgumentException($"MatMult Dim Err: A({rA}x{cA}) B({rB}x{cB})");var C=new float[rA*cB];for(int i=0;i<rA;i++)for(int j=0;j<cB;j++){float s=0;for(int k=0;k<cA;k++)s+=A[i*cA+k]*B[k*cB+j];C[i*cB+j]=s;}return C;}
        private static void Add(float[]A,float[]B,int r,int c){if(A==null||B==null||A.Length!=r*c||B.Length!=r*c)throw new ArgumentException();int l=A.Length;for(int i=0;i<l;i++)A[i]+=B[i];} private static void Sub(float[]A,float[]B,int r,int c){if(A==null||B==null||A.Length!=r*c||B.Length!=r*c)throw new ArgumentException();int l=A.Length;for(int i=0;i<l;i++)A[i]-=B[i];}
        private static float[] Inv(float[]M,int d){if(M==null||M.Length!=d*d||d>4)return null;int ac=d*2;var a=new float[d*ac];for(int i=0;i<d;i++){for(int j=0;j<d;j++)a[i*ac+j]=M[i*d+j];a[i*ac+d+i]=1f;}for(int i=0;i<d;i++){int pr=i;float mv=Mathf.Abs(a[i*ac+i]);for(int k=i+1;k<d;k++)if(Mathf.Abs(a[k*ac+i])>mv){mv=Mathf.Abs(a[k*ac+i]);pr=k;}if(pr!=i)for(int j=0;j<ac;j++){float t=a[i*ac+j];a[i*ac+j]=a[pr*ac+j];a[pr*ac+j]=t;}float pv=a[i*ac+i];if(Mathf.Abs(pv)<1e-8)return null;for(int j=i;j<ac;j++)a[i*ac+j]/=pv;for(int k=0;k<d;k++)if(k!=i){float f=a[k*ac+i];for(int j=i;j<ac;j++)a[k*ac+j]-=f*a[i*ac+j];}}var inv=new float[d*d];for(int i=0;i<d;i++)for(int j=0;j<d;j++)inv[i*d+j]=a[i*ac+d+j];return inv;}
    }
    #endregion

} // End Namespace MetaverseCloudEngine.Unity.OpenCV.Common

// Helper extension method class (keep in appropriate namespace)
namespace MetaverseCloudEngine.Unity.OpenCV
{
    // Pooled List helper using Unity's ObjectPool (if available) or standard List
    // Requires adding: using UnityEngine.Pool;
    internal static class ListPool<T>
    {
        // Simple non-pooled implementation if UnityEngine.Pool is not desired/available
        // Replace with actual pooling for performance gains.
        private static readonly ObjectPool<List<T>> pool = new ObjectPool<List<T>>(
            () => new List<T>(), // create Action
            actionOnGet: (list) => list.Clear(), // actionOnGet
            actionOnRelease: (list) => list.Clear(), // actionOnRelease (clear on release too)
            actionOnDestroy: (list) => { }, // actionOnDestroy (optional)
            collectionCheck: false, // collectionCheck (optional, can impact performance)
            defaultCapacity: 10, // defaultCapacity (optional)
            maxSize: 100 // maxSize (optional)
        );

        public static List<T> Get() => pool.Get();
        public static void Release(List<T> list) => pool.Release(list);
    }


    internal static class FunctionalExtensions
    {
        public static T Do<T>(this T self, Action<T> action)
        {
            if (self != null && action != null) action(self);
            return self;
        }
    }
}
#endif // METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV