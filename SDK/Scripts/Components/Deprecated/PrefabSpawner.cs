using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using System;

namespace MetaverseCloudEngine.Unity.Components
{
    [Obsolete("You can continue to use this, however we recommend you use '" + nameof(UnityPrefabSpawner) + "' instead.")]
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class PrefabSpawner : MetaSpaceBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform parent;
        [SerializeField] private bool requireNetworkOwnership;
        [SerializeField] private bool spawnOnStart = true;

        public UnityEvent<GameObject> onSpawned = new();

        private NetworkObject _networkObject;
        private IMetaSpaceNetworkingService _networkingService;

        private IMetaSpaceNetworkingService NetworkingService => _networkingService ??= MetaSpace ? MetaSpace.GetService<IMetaSpaceNetworkingService>() : null;

        private NetworkObject NetworkObject {
            get {
                if (!_networkObject) _networkObject = GetComponent<NetworkObject>();
                return _networkObject;
            }
        }

        public GameObject LastSpawnedObject { get; private set; }
        public GameObject Prefab
        {
            get => prefab;
            set => prefab = value;
        }

        public Transform Parent
        {
            get => parent;
            set => parent = value;
        }
        
        private void Reset()
        {
            if (!parent)
                parent = transform;
        }

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        public void Spawn(GameObject obj)
        {
            if (!this) return;
            if (!isActiveAndEnabled) return;
            if (!obj) return;

            if (MetaSpace && !MetaSpace.IsInitialized)
            {
                MetaSpace.Initialized += DoWork;
                return;
            }
            
            DoWork();
            return;

            void DoWork()
            {
                if (MetaSpace)
                    MetaSpace.Initialized -= DoWork;

                if (!HasSpawnAuthority())
                    return;

                var tr = transform;
                if (obj.GetComponent<NetworkObject>() && MetaSpace)
                {
                    if (NetworkingService != null)
                    {
                        NetworkingService.SpawnGameObject(obj, no =>
                        {
                            if (!this)
                            {
                                no.IsStale = true;
                                return;
                            }
                            
                            LastSpawnedObject = no.GameObject;
                            if (parent && LastSpawnedObject && IsParentValid())
                                LastSpawnedObject.transform.SetParent(parent);
                            onSpawned?.Invoke(LastSpawnedObject);
                            
                        }, tr.position, tr.rotation, false);

                    }
                    else
                    {
                        MetaverseProgram.Logger.LogError("Failed to spawn network object because the meta space has not yet been initialized.");
                    }
                }
                else
                {
                    LastSpawnedObject = Instantiate(obj, tr.position, tr.rotation);
                    if (parent && LastSpawnedObject && IsParentValid())
                        LastSpawnedObject.transform.parent = parent;
                    onSpawned?.Invoke(LastSpawnedObject);
                }
            }
        }

        public void Spawn()
        {
            Spawn(Prefab);
        }

        public void DestroyLastSpawnedObject()
        {
            if (LastSpawnedObject)
            {
                Destroy(LastSpawnedObject);
                LastSpawnedObject = null;
            }
        }

        public bool HasSpawnAuthority()
        {
            if (!this)
                // This object is marked for deletion.
                return false;

            if (!requireNetworkOwnership)
                // We're good to go!
                return true;

            if (NetworkingService == null)
                // There's no network service but we require
                // network ownership.
                return false;

            if (NetworkObject)
                // We must have authority to do this.
                return NetworkObject.IsStateAuthority;

            if (!NetworkingService.IsReady)
                // The networking service is not ready.
                return false;

            if (NetworkingService.IsHost)
                // Were not the host.
                return true;

            return false;
        }

        private bool IsParentValid()
        {
            if (!parent) return false; // If there's no parent then no this is not valid.

            NetworkObject isNetworkObj = LastSpawnedObject ? LastSpawnedObject.GetComponent<NetworkObject>() : null;
            if (!isNetworkObj) return true; // If we don't even have networking then yes this is fine.

            return parent.GetComponent<NetworkTransform>() || // The parent has a network transform.
                   parent.GetComponent<NetworkObject>();
        }
    }
}