using UnityEngine;
using UnityEngine.Events;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using System;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    public class UnityPrefabSpawner : MetaSpaceBehaviour
    {
        #region Inspector

        [Required] [SerializeField] private SpawnablePrefab prefab;
        [Required] [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform parent;
        [SerializeField] private bool requireStateAuthority;
        [SerializeField] private bool spawnOnStart = true;

        public UnityEvent<GameObject> onSpawned = new();
        public UnityEvent onFailed = new();

        #region Deprecated

        [SerializeField, HideInInspector, Obsolete("Please use '" + nameof(requireStateAuthority) + "' instead.")]
        private bool requireNetworkOwnership;

        #endregion

        #endregion

        #region Fields

        private bool _hasStarted;
        private NetworkObject _networkObject;
        private IMetaSpaceNetworkingService _networkingService;

        #endregion

        #region Properties

        private IMetaSpaceNetworkingService NetworkingService => _networkingService ??= MetaSpace ? MetaSpace.GetService<IMetaSpaceNetworkingService>() : null;

        private NetworkObject NetworkObject {
            get {
                if (!_networkObject) _networkObject = GetComponentInParent<NetworkObject>(true);
                return _networkObject;
            }
        }

        public bool SpawnOnStart { get => spawnOnStart; set => spawnOnStart = value; }

        public bool RequireStateAuthority { get => requireStateAuthority; set => requireStateAuthority = value; }

        public GameObject LastSpawnedObject { get; private set; }

        public GameObject Prefab {
            get => prefab ? prefab.gameObject : null;
            set => prefab = value && value.TryGetComponent(out SpawnablePrefab s) ? s : null;
        }

        public Transform Parent {
            get => parent;
            set => parent = value;
        }

        public Transform SpawnPoint {
            get => spawnPoint;
            set => spawnPoint = value;
        }

        #region Deprecated

        [Obsolete("Please use '" + nameof(RequireStateAuthority) + "' instead.")]
        public bool RequireNetworkOwnership { get => requireStateAuthority; set => requireStateAuthority = value; }

        #endregion

        #endregion

        #region Unity Events

        private void Reset()
        {
            UpgradeFields();

            if (!parent)
                parent = transform;

            if (!spawnPoint)
                spawnPoint = transform;
        }

        private void Start()
        {
            UpgradeFields();

            _hasStarted = true;

            if (!spawnPoint)
                spawnPoint = transform;

            if (spawnOnStart)
                Spawn();
        }

        #endregion

        #region Public Methods

        public void Spawn(GameObject pf)
        {
            UpgradeFields();

            if (!_hasStarted && spawnOnStart) return;
            if (!isActiveAndEnabled) return;
            if (!pf)
            {
                onFailed?.Invoke();
                return;
            }

            if (MetaSpace && !MetaSpace.IsInitialized)
            {
                MetaSpace.Initialized += SpawnNow;
                return;
            }

            SpawnNow();
            return;

            void SpawnNow()
            {
                if (MetaSpace)
                    MetaSpace.Initialized -= SpawnNow;

                if (!HasSpawnAuthority())
                {
                    onFailed?.Invoke();
                    return;
                }

                var spawn = spawnPoint ? spawnPoint : transform;
                if (pf.GetComponent<NetworkObject>() && MetaSpace)
                {
                    if (NetworkingService is not null)
                    {
                        NetworkingService.SpawnGameObject(pf, no =>
                        {
                            if (!this || !isActiveAndEnabled)
                            {
                                no.IsStale = true;
                                onFailed?.Invoke();
                                return;
                            }
                            
                            LastSpawnedObject = no.GameObject;
                            if (parent && LastSpawnedObject && IsParentValid())
                                LastSpawnedObject.transform.parent = parent;
                            onSpawned?.Invoke(LastSpawnedObject);
                            
                        }, spawn.position, spawn.rotation, false);
                    }
                    else
                    {
                        MetaverseProgram.Logger.LogError("Failed to spawn network object because the meta space has not yet been initialized.");
                        onFailed?.Invoke();
                    }
                }
                else
                {
                    LastSpawnedObject = Instantiate(pf, spawn.position, spawn.rotation);
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

            if (!requireStateAuthority)
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

        #endregion

        #region Private Methods

        private void UpgradeFields()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (requireNetworkOwnership)
            {
                requireStateAuthority = requireNetworkOwnership;
                requireNetworkOwnership = false;
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private bool IsParentValid()
        {
            if (!parent) return false; // If there's no parent then no this is not valid.

            NetworkObject isNetworkObj = LastSpawnedObject ? LastSpawnedObject.GetComponent<NetworkObject>() : null;
            if (!isNetworkObj) return true; // If we don't even have networking then yes this is fine.

            return parent.GetComponent<NetworkTransform>() || // The parent has a network transform.
                   parent.GetComponent<NetworkObject>();
        }

        #endregion
    }
}