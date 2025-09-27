using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Components;

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Editors
{
    /// <summary>
    /// Updates all the <see cref="SpawnablePrefab.ID"/> and <see cref="SpawnablePrefab.SourcePrefabID"/>
    /// values with unique values. This way the objects can be identified at runtime and
    /// properly spawned.
    /// </summary>
    public class SpawnablePrefabIDGenerator : IMetaversePrefabBuildProcessor, IMetaverseSceneBuildProcessor
    {
        public int callbackOrder => -1;

        public void OnPostProcessBuild(GameObject prefab) { }
        public void OnPostProcessBuild(Scene scene) { }

        public void OnPreProcessBuild(GameObject prefab)
        {
            if (!prefab.TryGetComponent<MetaPrefab>(out var sourceMetaPrefab))
                return;

            var uniqueIDs = new List<Guid>();
            var processedObjects = new List<GameObject>();
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            var objects = AssetDatabase.GetDependencies(prefabPath, true);
            foreach (var path in objects)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefabAsset)
                    continue;

                UpdateAssetSpawnables(uniqueIDs, processedObjects, prefabAsset, true, sourceMetaPrefab);
            }

            AssetDatabase.SaveAssets();
        }

        public void OnPreProcessBuild(Scene scene)
        {
            var uniqueIDs = new List<Guid>();
            var processedObjects = new List<GameObject>();
            var prefabDependencies = AssetDatabase.GetDependencies(scene.path, true);
            foreach (var prefabPath in prefabDependencies)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!prefabAsset)
                    continue;
                UpdateAssetSpawnables(uniqueIDs, processedObjects, prefabAsset, true, sourceMetaPrefab: null);
            }

            foreach (var scenePrefabInstance in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpawnablePrefab>()))
            {
                UpdateAssetSpawnables(uniqueIDs, processedObjects, scenePrefabInstance.gameObject, false, sourceMetaPrefab: null);
            }

            AssetDatabase.SaveAssets();
        }

        private static void UpdateAssetSpawnables(ICollection<Guid> uniqueIDs, ICollection<GameObject> processedObjects, GameObject prefabAsset, bool isPrefab, MetaPrefab sourceMetaPrefab = null)
        {
            if (processedObjects.Contains(prefabAsset))
                return;

            var spawnables = prefabAsset.GetComponentsInChildren<SpawnablePrefab>(true);
            if (spawnables.Length > 0)
            {
                if (isPrefab)
                {
                    var parentPrefab = PrefabUtility.GetCorrespondingObjectFromSource(prefabAsset);
                    if (parentPrefab != null)
                    {
                        // Scale up the prefab variant hierarchy and make sure
                        // those IDs are updated first.
                        UpdateAssetSpawnables(uniqueIDs, processedObjects, parentPrefab, sourceMetaPrefab);
                    }
                }

                foreach (var sp in spawnables)
                {
                    UpdateSpawnable(uniqueIDs, sp, sourceMetaPrefab);
                }
            }

            processedObjects.Add(prefabAsset);
        }

        private static void UpdateSpawnable(ICollection<Guid> uniqueIDs, SpawnablePrefab spawnable, MetaPrefab sourceMetaPrefab = null)
        {
            if (spawnable == null)
                return;

            if (sourceMetaPrefab != null && spawnable.SourcePrefabID != sourceMetaPrefab.ID)
            {
#if UNITY_EDITOR
                spawnable.SourcePrefabID = sourceMetaPrefab.ID;
#endif
                EditorUtility.SetDirty(spawnable.gameObject);
            }
            else
            {
#if UNITY_EDITOR
                spawnable.SourcePrefabID = null;
#endif
                EditorUtility.SetDirty(spawnable.gameObject);
            }

            if (spawnable.ID == null || uniqueIDs.Contains(spawnable.ID.Value))
            {
#if UNITY_EDITOR
                spawnable.ID = Guid.NewGuid();
#endif
                EditorUtility.SetDirty(spawnable.gameObject);
            }

            uniqueIDs.Add(spawnable.ID.Value);
        }
    }
}
