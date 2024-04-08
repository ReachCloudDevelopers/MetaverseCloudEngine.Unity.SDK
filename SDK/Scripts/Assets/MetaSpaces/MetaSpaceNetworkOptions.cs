using MetaverseCloudEngine.Unity.Services.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Networking.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using MetaverseCloudEngine.Unity.Services.Options;
using MetaverseCloudEngine.Unity.Components;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    /// <summary>
    /// Contains network options for a Meta Space, including the maximum number of players allowed in the Meta Space and a list of prefabs that can be spawned in the Meta Space.
    /// </summary>
    [Serializable]
    public class MetaSpaceNetworkOptions : IMetaSpaceResources, IMetaSpaceNetworkOptions
    {
        [Tooltip("If there are more players than this in the room, the room will not be join-able. A value of -1 means unlimited.")]
        [SerializeField, Min(-1)] private int totalMaxPlayers = -1;

        [Tooltip("List of prefabs that can be spawned in the Meta Space")]
        [SerializeField, HideInInspector] private List<GameObject> spawnableObjects = new();

        private Dictionary<GameObject, int> _embeddedPrefabIds;
        private Dictionary<int, GameObject> _embeddedIdPrefabs;
        private Dictionary<string, GameObject> _embeddedNamedPrefabs;

        private Dictionary<(Guid, Guid), GameObject> _spawnablePrefabs;
        private Dictionary<Guid, (Guid, Guid)> _spawnablePrefabKeyMap;
        private Dictionary<Guid, List<SpawnableResourceCallback>> _spawnableCallbacks;

        private GameObject _defaultPlayerPrefab;
        private IPlayerGroupOptions _playerGroupOptions;

        /// <summary>
        /// Gets the maximum number of players that can join the room.
        /// A value of -1 means unlimited.
        /// </summary>
        public int TotalMaxPlayers => Mathf.Min(255, totalMaxPlayers);

        /// <summary>
        /// Gets the list of objects that can be spawned in the scene.
        /// </summary>
        public List<GameObject> EmbeddedResources => spawnableObjects;

        public void Initialize(GameObject defaultPlayerPrefab, IPlayerGroupOptions playerGroupOptions)
        {
            _defaultPlayerPrefab = defaultPlayerPrefab;
            _playerGroupOptions = playerGroupOptions;
        }

        public void ScanForSpawnables(Scene scene)
        {
            if (Application.isPlaying)
                return; // We shouldn't be scanning on play...

            spawnableObjects = spawnableObjects.Distinct().ToList();
            spawnableObjects.RemoveAll(x => !x);

#if UNITY_EDITOR
            string[] objects = UnityEditor.AssetDatabase.GetDependencies(scene.path, true);
            foreach (string path in objects)
            {
                GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset)
                    continue;

                if (!asset.GetComponent<NetworkObject>())
                    continue;

                if (!spawnableObjects.Contains(asset))
                    spawnableObjects.Add(asset);
            }
#endif
        }

        public GameObject GetEmbeddedPrefabWithName(string name)
        {
            InitSpawnablePrefabsCache();
            return _embeddedNamedPrefabs.TryGetValue(name, out GameObject prefab) ? prefab : null;
        }

        public GameObject GetEmbeddedPrefabByID(int id)
        {
            InitSpawnablePrefabsCache();
            return _embeddedIdPrefabs.TryGetValue(id, out GameObject prefab) ? prefab : null;
        }

        public int GetEmbeddedPrefabID(GameObject prefab)
        {
            InitSpawnablePrefabsCache();

            if (_embeddedPrefabIds.TryGetValue(prefab, out int id))
                return id;

            return -1;
        }

        private void InitSpawnablePrefabsCache()
        {
            if (_embeddedPrefabIds != null)
                return;

            if (_defaultPlayerPrefab && !spawnableObjects.Contains(_defaultPlayerPrefab))
                spawnableObjects.Add(_defaultPlayerPrefab);

            GameObject defaultPlayer = Resources.Load<GameObject>(MetaverseConstants.Resources.DefaultPlayer);
            if (defaultPlayer && spawnableObjects.All(x => x.name != defaultPlayer.name))
                spawnableObjects.Add(defaultPlayer);

            foreach (PlayerGroup playerGroup in _playerGroupOptions.PlayerGroups)
                if (playerGroup.playerPrefab != null &&
                    !spawnableObjects.Contains(playerGroup.playerPrefab))
                    spawnableObjects.Add(playerGroup.playerPrefab);

            _embeddedPrefabIds = spawnableObjects.ToDictionary(x => x, y => spawnableObjects.IndexOf(y));
            _embeddedIdPrefabs = _embeddedPrefabIds.ToDictionary(x => x.Value, y => y.Key);
            _embeddedNamedPrefabs = spawnableObjects.GroupBy(x => x.name).Select(x => x.FirstOrDefault()).ToDictionary(x => x.name, y => y);
        }

        /// <summary>
        /// Registers a spawnable prefab in a lookup table.
        /// </summary>
        /// <param name="prefab">The prefab to register.</param>
        public void RegisterSpawnable(SpawnablePrefab prefab)
        {
            if (prefab.ID == null)
                return;

            _spawnablePrefabs ??= new Dictionary<(Guid, Guid), GameObject>();
            _spawnablePrefabKeyMap ??= new Dictionary<Guid, (Guid, Guid)>();

            (Guid resourceID, Guid metaPrefabID) key = (resourceID: prefab.ID.Value, metaPrefabID: prefab.SourcePrefabID.GetValueOrDefault());
            if (_spawnablePrefabs.TryGetValue(key, out GameObject pf))
                return;

            _spawnablePrefabKeyMap[prefab.ID.Value] = key;
            _spawnablePrefabs[key] = prefab.gameObject;
            
            if (_spawnableCallbacks != null && _spawnableCallbacks.TryGetValue(prefab.ID.Value, out List<SpawnableResourceCallback> callbacks))
            {
                foreach (SpawnableResourceCallback cb in callbacks)
                    try { cb?.Invoke(key.resourceID, key.metaPrefabID, prefab.gameObject); } catch(Exception e) { Debug.LogException(e); };
                _spawnableCallbacks.Remove(prefab.ID.Value);
                if (_spawnableCallbacks.Count == 0)
                    _spawnableCallbacks = null;
            }
        }

        /// <summary>
        /// Unregisters a spawnable prefab from the lookup table.
        /// </summary>
        /// <param name="id">The ID of the spawnable prefab.</param>
        public void UnregisterSpawnable(Guid id)
        {
            if (_spawnablePrefabKeyMap != null && _spawnablePrefabKeyMap.TryGetValue(id, out (Guid, Guid) key))
            {
                if (_spawnablePrefabs?.Remove(key) == true)
                {
                    _spawnablePrefabKeyMap.Remove(id);
                    _spawnableCallbacks?.Remove(id);
                    if (_spawnableCallbacks.Count == 0)
                        _spawnableCallbacks = null;
                }
            }
        }

        public void RegisterSpawnableCallback(Guid id, SpawnableResourceCallback callback)
        {
            if (_spawnablePrefabs != null && _spawnablePrefabKeyMap != null && _spawnablePrefabKeyMap.TryGetValue(id, out (Guid, Guid) key) && _spawnablePrefabs.TryGetValue(key, out GameObject go) && go)
            {
                callback?.Invoke(key.Item1, key.Item2, go);
                return;
            }

            _spawnableCallbacks ??= new Dictionary<Guid, List<SpawnableResourceCallback>>();
            if (!_spawnableCallbacks.TryGetValue(id, out List<SpawnableResourceCallback> actions))
                _spawnableCallbacks[id] = actions = new List<SpawnableResourceCallback>();
            if (!actions.Contains(callback))
                actions.Add(callback);
        }

        public void UnregisterSpawnableCallback(Guid id, SpawnableResourceCallback callback)
        {
            if (_spawnableCallbacks == null)
                return;

            if (_spawnableCallbacks.TryGetValue(id, out List<SpawnableResourceCallback> actions))
            {
                actions.Remove(callback);
                if (actions.Count == 0)
                {
                    _spawnableCallbacks.Remove(id);
                    if (_spawnableCallbacks.Count == 0)
                        _spawnableCallbacks = null;
                }
            }
        }

        public GameObject GetSpawnablePrefab(Guid id)
        {
            if (_spawnablePrefabs == null)
                return null;

            if (_spawnablePrefabKeyMap.TryGetValue(id, out (Guid, Guid) key))
            {
                if (_spawnablePrefabs.TryGetValue(key, out GameObject go))
                    return go;
            }

            return null;
        }
    }
}