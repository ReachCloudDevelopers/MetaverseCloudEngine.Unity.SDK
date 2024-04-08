using System;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    [DeclareFoldoutGroup("Identifiers")]
    public class SpawnablePrefab : TriInspectorMonoBehaviour
    {
        [InfoBox("These IDs are used in-app to identify the object that should spawn.")]
        [Group("Identifiers")] [SerializeField, ReadOnly] private string spawnableID;
        [Group("Identifiers")] [SerializeField, ReadOnly] private string sourcePrefabID;

        private Guid? _id;
        public Guid? ID {
            get {
                if (!Application.isPlaying)
                    return Guid.TryParse(spawnableID, out Guid guid) ? guid : null;

                if (_id == null && Guid.TryParse(spawnableID, out Guid id))
                    _id = id;
                return _id;
            }
#if UNITY_EDITOR
            set {
                spawnableID = value?.ToString() ?? string.Empty;
            }
#endif
        }

        private Guid? _sourcePrefabId;
        public Guid? SourcePrefabID {
            get {
                if (!Application.isPlaying)
                    return Guid.TryParse(sourcePrefabID, out Guid guid) ? guid : null;

                if (_sourcePrefabId == null && Guid.TryParse(sourcePrefabID, out Guid id))
                    _sourcePrefabId = id;
                return _sourcePrefabId;
            }
#if UNITY_EDITOR
            set {
                sourcePrefabID = value?.ToString();
            }
#endif
        }
    }
}