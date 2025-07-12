#if MV_SILVERTAU
using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using SilverTau.RoomPlanUnity;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    /// <summary>
    /// A class that maps labels to prefab IDs for SilverTau Meta Prefabs.
    /// </summary>
    [HideMonoScript]
    [RequireComponent(typeof(MetaPrefabSpawner))]
    public class SilverTauMetaPrefabMapping : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// A class that represents a mapping between a label and a prefab ID.
        /// </summary>
        [Serializable]
        public class PrefabLabelMapping
        {
            [Tooltip("The label to map to a prefab ID.")]
            public CapturedRoom.Category label;
            [MetaPrefabIdProperty]
            [Tooltip("The prefab ID to map to the label.")]
            public string prefabID = "";
        }

        [InfoBox("Ensure that all prefabs are 1x1x1 scale and centered at the origin (0,0,0).")]
        [SerializeField]
        [MetaPrefabIdProperty]
        private string fallbackPrefabID = "";
        [Tooltip("A list of prefab label mappings to map labels to prefab IDs.")]
        [SerializeField] private PrefabLabelMapping[] prefabLabelMappings = Array.Empty<PrefabLabelMapping>();

        private MetaPrefabSpawner _metaPrefabSpawner;
        private CapturedRoomObject _capturedRoomObject;
        private Dictionary<CapturedRoom.Category, string> _labelToPrefabIDMap;

        private void Awake()
        {
            _labelToPrefabIDMap = prefabLabelMappings
                .ToDictionary(mapping => mapping.label, mapping => mapping.prefabID);
            _capturedRoomObject = GetComponent<CapturedRoomObject>();
        }

        private void OnEnable()
        {
            if(!_capturedRoomObject) return;
            _capturedRoomObject.initObject += OnInit;
            _capturedRoomObject.updateObject += OnUpdate;
        }

        private void OnDisable()
        {
            if(!_capturedRoomObject) return;
            _capturedRoomObject.initObject -= OnInit;
            _capturedRoomObject.updateObject -= OnUpdate;
        }
        
        private void OnInit() => ApplyPrefabMapping();

        private void OnUpdate() => ApplyPrefabMapping();

        private void ApplyPrefabMapping()
        {
            if (!_capturedRoomObject) return;
            CapturedRoom.Category category = _capturedRoomObject.category;
            if (_labelToPrefabIDMap.TryGetValue(category, out var prefabID) && !string.IsNullOrEmpty(prefabID) && Guid.TryParse(prefabID, out var id))
                _metaPrefabSpawner.IDString = id.ToString();
            else
                MetaverseProgram.Logger.LogWarning($"No prefab ID mapping found for category: {category}");
        }
    }
}
#endif