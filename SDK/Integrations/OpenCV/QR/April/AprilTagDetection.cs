﻿#if (METAVERSE_CLOUD_ENGINE && MV_APRIL_TAG) || METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED

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
    public class AprilTagDetection : TriInspectorMonoBehaviour
    {
        public enum AprilTagType
        {
            TagStandard41H12 = 0,
        }
        
        [SerializeField] private AprilTagType tagType;
        [FormerlySerializedAs("tagSize")]
        [Min(0.001f)]
        [SerializeField] private float tagRealWorldSize = 1f;
        [OnValueChanged(nameof(DisposeDetector))]
        [Min(1)] [SerializeField] private int decimation = 4;
        [SerializeField] private bool spawnObjects;
        [ShowIf(nameof(spawnObjects))]
        [SerializeField] private string objectNameFormat = "{0}"; 
        [ShowIf(nameof(spawnObjects))]
        [SerializeField] private Transform spawnParent;
        [ShowIf(nameof(spawnObjects))]
        [SerializeField] private GameObject defaultPrefab;
        [ShowIf(nameof(spawnObjects))]
        [SerializeField] private List<TagPrefab> tagPrefabs = new();

        [Serializable]
        public class TagPrefab
        {
            public int targetTag;
            public string customNameFormat;
            [Required] public GameObject prefab;
        }

        /// <summary>
        /// Gets or sets the decimation value for the AprilTag detector.
        /// </summary>
        public int Decimation
        {
            get => decimation;
            set
            {
                if (decimation == value) return;
                decimation = value;
                DisposeDetector();
            }
        }
        
        private AprilTag.TagDetector _detector;
        private ICameraFrameProvider _textureProvider;
        private Texture2D _t2d;
        private readonly Dictionary<int, GameObject> _spawnedObjects = new();
        private Dictionary<int, TagPrefab> _tagPrefabs = new();

        private void Awake()
        {
            _textureProvider = GetComponent<ICameraFrameProvider>();
            if (_textureProvider is null)
            {
                enabled = false;
                MetaverseProgram.Logger.LogError("No ICameraFrameProvider found. Disabling AprilTagDetection.");
                return;
            }
            
            InitTagPrefabs();
        }

        private void OnValidate()
        {
            InitTagPrefabs();
        }

        private void OnDestroy()
        {
            DisposeDetector();
        }

        private void Update()
        {
            using var frame = _textureProvider.DequeueNextFrame();
            if (frame is null)
                return;
            
            var colors = frame.GetColors32();
            if (colors is { Length: 0 })
                return;
            
            var size = frame.GetSize();
            
            _detector ??= new AprilTag.TagDetector(size.x, size.y, decimation: 4);
            _detector.ProcessImage(colors, frame.GetFOV(ICameraFrame.FOVType.Horizontal) * Mathf.Deg2Rad, tagRealWorldSize);

            if (_detector.DetectedTags is null)
                return;
            
            foreach (var obj in _spawnedObjects
                         .Where(obj => _detector.DetectedTags.All(d => d.ID != obj.Key))
                         .ToArray())
            {
                if (obj.Value)
                    Destroy(obj.Value);
                _spawnedObjects.Remove(obj.Key);
            }

            foreach (var t in _detector.DetectedTags)
            {
                if (_spawnedObjects.TryGetValue(t.ID, out var go))
                {
                    if (go)
                    {
                        var tr = go.transform;
                        tr.localPosition = t.Position;
                        tr.localRotation = t.Rotation;
                        tr.parent = spawnParent;   
                    }
                    continue;
                }
                
                var tagPreset = _tagPrefabs.GetValueOrDefault(t.ID);
                var prefab = tagPreset?.prefab ?? defaultPrefab;
                if (!prefab)
                    go = new GameObject
                    {
                        transform =
                        {
                            localPosition = t.Position,
                            localRotation = t.Rotation,
                            parent = spawnParent
                        }
                    };
                else
                    go = Instantiate(prefab, t.Position, t.Rotation, spawnParent);
                
                go.name = !string.IsNullOrEmpty(tagPreset?.customNameFormat)
                    ? string.Format(tagPreset.customNameFormat)
                    : string.Format(objectNameFormat, t.ID);
                
                _spawnedObjects[t.ID] = go;
            }
        }

        private void InitTagPrefabs()
        {
            _tagPrefabs ??= new Dictionary<int, TagPrefab>();
            foreach (var prefab in tagPrefabs)
                _tagPrefabs[prefab.targetTag] = prefab;
        }

        private void DisposeDetector()
        {
            _detector?.Dispose();
            _detector = null;
        }
    }
}
#endif