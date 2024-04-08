using MetaverseCloudEngine.Unity.Components;
using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Services.Abstract
{
    public interface IMetaSpaceNetworkOptions
    {
        int TotalMaxPlayers { get; }
    }

    public delegate void SpawnableResourceCallback(Guid spawnableId, Guid? metaPrefabId, GameObject prefab);
    
    public interface IMetaSpaceResources
    {
        // There are 2 resource types:
        // - Spawnable Prefabs: These are prefabs that can be spawned by the network irrespective of the location (i.e. they don't need to be registered in the meta space).
        // - Embedded Prefabs: These are prefabs that are registered with the meta space asset upon upload. They can be loaded much quicker and use a simple integer ID to reference them.

        void RegisterSpawnable(SpawnablePrefab prefab);
        void UnregisterSpawnable(Guid id);
        void RegisterSpawnableCallback(Guid id, SpawnableResourceCallback callback);
        void UnregisterSpawnableCallback(Guid id, SpawnableResourceCallback callback);
        GameObject GetSpawnablePrefab(Guid id);

        GameObject GetEmbeddedPrefabWithName(string name);
        GameObject GetEmbeddedPrefabByID(int id);
        int GetEmbeddedPrefabID(GameObject prefab);
    }
}