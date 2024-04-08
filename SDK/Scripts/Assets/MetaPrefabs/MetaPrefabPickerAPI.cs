using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    public partial class MetaPrefabPickerAPI : BaseAssetPickerAPI<PrefabDto, MetaPrefabPickerQueryParams>
    {
        public MetaPrefabSpawnOption spawnOptions;

        [NonSerialized]
        public List<GameObject> ActiveInstances = new();

        public override void Show()
        {
            if (MetaSpace.Instance)
                MetaSpace.OnReady(space =>
                {
                    if (Guid.TryParse(spawnOptions.offlineModePrefab, out Guid prefabId) &&
                        space.GetService<IMetaSpaceNetworkingService>().IsOfflineMode &&
                        TrySpawn(prefabId))
                        return;

                    base.Show();
                });
            else
            {
                base.Show();
            }
        }

        private bool TrySpawn(Guid prefabId)
        {
            if (!spawnOptions.spawnPickedObject)
            {
                spawnOptions.onSpawnFailed?.Invoke();
                return false;
            }

            if (ActiveInstances.Any(x => !x))
                ActiveInstances = ActiveInstances.Where(x => x).ToList();

            if (!spawnOptions.destroyPreviousInstance)
            {
                if (spawnOptions.maxActiveInstances > 0 && ActiveInstances.Count(x => x) >= spawnOptions.maxActiveInstances)
                {
                    spawnOptions.onSpawnFailed?.Invoke();
                    return false;
                }
            }

            var spawnPoint =
                spawnOptions.spawnCheckPoints
                     .Where(x => x.checkSpawnAreaForObjects)
                     .FirstOrDefault(CanSpawn) ??
                 spawnOptions.spawnCheckPoints
                     .FirstOrDefault(x => !x.checkSpawnAreaForObjects);

            if (spawnPoint is null)
            {
                spawnOptions.onSpawnFailed?.Invoke();
                return true;
            }

            if (spawnOptions.destroyPreviousInstance)
            {
                var prevInst = ActiveInstances.FirstOrDefault(x => x);
                if (prevInst)
                    Destroy(prevInst);
            }

            var spawner = MetaPrefabSpawner.CreateSpawner(
                prefabId,
                spawnPoint.spawnPoint.position,
                spawnPoint.spawnPoint.rotation,
                spawnerParent: transform,
                requireStateAuthority: false,
                loadOnStart: false);

            spawner.retryAttempts = 0;
            spawner.events.onFinishedLoading.AddListener(() =>
            {
                spawner.syncDestroy = false;
                if (spawner.SpawnedPrefab)
                {
                    spawnOptions.onSpawned?.Invoke(spawner.SpawnedPrefab);
                    ActiveInstances.Add(spawner.SpawnedPrefab);
                }
                else spawnOptions.onSpawnFailed?.Invoke();
                
                Destroy(spawner);
            });
            
            spawner.Spawn();
            return true;
        }

        private static bool CanSpawn(MetaPrefabSpawnOptionPoint point)
        {
            if (!point.checkSpawnAreaForObjects) return true;
            if (!point.spawnPoint) return false;
            var origin = point.checkArea ? point.checkArea.position : point.spawnPoint.position;
            return !Physics.CheckSphere(origin, point.checkRadius, point.checkLayers);
        }
    }
}