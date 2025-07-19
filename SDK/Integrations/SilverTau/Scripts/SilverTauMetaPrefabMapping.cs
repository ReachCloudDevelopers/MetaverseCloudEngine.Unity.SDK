using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
#if MV_SILVERTAU
using SilverTau.RoomPlanUnity;
#endif
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SilverTau
{
    /// <summary>
    /// A class that maps labels to prefab IDs for SilverTau Meta Prefabs.
    /// </summary>
    [HideMonoScript]
    public class SilverTauMetaPrefabMapping : TriInspectorMonoBehaviour
    {
        public enum Category
        {
            unknown,
            bathtub,
            bed,
            chair,
            dishwasher,
            fireplace,
            oven,
            refrigerator,
            sink,
            sofa,
            stairs,
            storage,
            stove,
            table,
            television,
            toilet,
            washerDryer,
            window,
            wall,
            opening,
            door,
            doorOpen,
            doorClose,
            floor,
            ceiling
        }
        
        /// <summary>
        /// A class that represents a mapping between a label and a prefab ID.
        /// </summary>
        [Serializable]
        public class PrefabLabelMapping
        {
            [Tooltip("The label to map to a prefab ID.")]
            public Category label;
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
        
#if MV_SILVERTAU
        private CapturedRoomObject _capturedRoomObject;
        private Dictionary<Category, string> _labelToPrefabIDMap;

        /// <summary>
        /// The ID of the prefab that is currently applied to the captured room object.
        /// </summary>
        public string ID { get; private set; }

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
            var category = GetCategory();
            if (_labelToPrefabIDMap.TryGetValue(category, out var prefabID) && 
                !string.IsNullOrEmpty(prefabID) && 
                Guid.TryParse(prefabID, out var id))
                ID = id.ToString();
            else
                ID = fallbackPrefabID;
        }

        /// <summary>
        /// Gets the category of the captured room object based on its label.
        /// </summary>
        /// <returns>The category of the captured room object.</returns>
        public Category GetCategory()
        {
            return Enum.Parse<Category>(_capturedRoomObject.category.ToString(), true);
        }
#else
        private void Start() { /* for enabled/disabled toggle. */ }
        
        public Category GetCategory()
        {
            return Category.unknown;
        }
#endif
    }
}
