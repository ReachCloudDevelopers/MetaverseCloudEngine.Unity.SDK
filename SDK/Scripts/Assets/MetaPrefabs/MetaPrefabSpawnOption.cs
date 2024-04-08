using System;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    [Serializable]
    public class MetaPrefabSpawnOption
    {
        public bool spawnPickedObject = true;
        [Min(-1)] public int maxActiveInstances = -1;
        public bool destroyPreviousInstance;

        [Header("Spawn Check")]
        public MetaPrefabSpawnOptionPoint[] spawnCheckPoints = Array.Empty<MetaPrefabSpawnOptionPoint>();

        [Header("Offline Mode Debug")]
        [MetaPrefabIdProperty] public string offlineModePrefab;

        [Header("Events")]
        public UnityEvent<GameObject> onSpawned;
        public UnityEvent onSpawnFailed;
    }

    [Serializable]
    public class MetaPrefabSpawnOptionPoint
    {
        [Required] public Transform spawnPoint;
        public bool checkSpawnAreaForObjects = true;
        [ShowIf(nameof(checkSpawnAreaForObjects))] public Transform checkArea;
        [ShowIf(nameof(checkSpawnAreaForObjects))] public LayerMask checkLayers = Physics.AllLayers;
        [ShowIf(nameof(checkSpawnAreaForObjects))] public float checkRadius = 2;
    }
}