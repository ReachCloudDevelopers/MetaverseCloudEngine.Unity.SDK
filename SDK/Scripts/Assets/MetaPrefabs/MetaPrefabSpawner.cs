using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Assets.Attributes;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using NetworkObject = MetaverseCloudEngine.Unity.Networking.Components.NetworkObject;
using MetaverseCloudEngine.Unity.Rendering;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;

// ReSharper disable Unity.InefficientPropertyAccess
namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    /// <summary>
    /// Uses the <see cref="MetaPrefabLoadingAPI"/> to load a meta prefab and spawn it.
    /// This is useful for spawning prefabs that are not in the scene, but are stored on the server.
    /// </summary>
    [HideMonoScript]
    [DeclareFoldoutGroup("Spawn Options")]
    [DeclareFoldoutGroup("Retry Options")]
    [DeclareFoldoutGroup("Editor")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Metaverse Assets/Prefabs/Meta Prefab Spawner")]
    [HierarchyIcon("Download-Available@2x")]
    public partial class MetaPrefabSpawner : MetaSpaceBehaviour, IAssetReference, IMeasureCameraDistance
    {
        /// <summary>
        /// A list of IDs for meta prefabs that are currently being cached (to prevent redundant web requests).
        /// </summary>
        private static readonly List<Guid> CurrentlyCachingIDs = new();

        private const string SpawnerCachePrefix = "mpspawner_";
        private const string MetaPrefabDtoCachePrefix = "mpdto_";

        /// <summary>
        /// Front-end unity events for the meta prefab spawner.
        /// </summary>
        [Serializable]
        public class MetaPrefabSpawnerEvents
        {
            [Tooltip("Invoked when the prefab starts loading.")]
            public UnityEvent onStartedLoading = new();
            [Tooltip("Invoked when the prefab finishes loading.")]
            public UnityEvent onFinishedLoading = new();
            [Tooltip("Invoked when the download progress has changed.")]
            public UnityEvent<float> onProgress = new();
            [Tooltip("Invoked when the prefab spawns.")]
            public UnityEvent<GameObject> onSpawned = new();
            [Tooltip("Invoked when the loading or spawning fails.")]
            public UnityEvent<string> onFailed = new();
            [Tooltip("Invoked when the last spawned object is destroyed.")]
            public UnityEvent onLastSpawnedObjectDestroyed = new();
        }

        [Tooltip("The prefab that will be spawned.")]
        [MetaPrefabIdProperty] public string prefab;

        [Tooltip("Check this if you want to spawn the meta prefab when this spawner is loaded into the scene.")]
        [Group("Spawn Options")] public bool spawnOnStart;
        [Tooltip("If true, allows this spawner to spawn another object even if one is already spawned and active.")]
        [Group("Spawn Options")] public bool concurrent;
        [Tooltip("Check this if when this object is destroyed, the meta prefab that was spawned should also be destroyed.")]
        [Group("Spawn Options")] public bool syncDestroy = true;
        [Tooltip("Check this if, in order to load the prefab, you must be the host/server or the state authority of the network object on this game object.")]
        [Group("Spawn Options")] public bool requireStateAuthority;
        [ShowIf(nameof(requireStateAuthority))]
        [InfoBox("You don't need to assign the network object if the state authority should be the host/server.")]
        [Group("Spawn Options")]
        [SerializeField] private NetworkObject networkObject;

        [Tooltip("(Optional) Spawns the meta prefab object as a child of this transform. If the meta prefab is a Network Object, the 'parent' will need to have a Network Transform component attached.")]
        [Group("Spawn Options")] public Transform parent;
        [Tooltip("(Optional) Spawns these objects as a child of the spawned prefab.")]
        [Group("Spawn Options")] public SpawnablePrefab[] spawnAddons;

        [Tooltip("The maximum number of times to retry download after a failure.")]
        [Group("Retry Options")] public int retryAttempts = 2;
        [Tooltip("The time, in seconds, to wait before performing another retry.")]
        [Group("Retry Options")] public float retryCooldown = 0.75f;

        [Tooltip("Check this if you'd like to preview the meta prefab in the editor when this spawner is selected in the hierarchy.")]
        [InfoBox("Previews may take some time to load, depending on the size of the meta prefab.")]
        public bool previewInEditor;

        [Space]
        public MetaPrefabSpawnerEvents events;

        private bool _failed;
        private bool _isDestroyed;
        private int _numberOfRetries;
        private IMetaSpaceNetworkingService _networkingService;
        private CancellationTokenSource _metaPrefabSpawnerCancellation = new();

        private static readonly Dictionary<Guid, PrefabDto> CachedPrefabDtos = new ();

        #region Deprecated

        [HideInInspector, Obsolete("Please use '" + nameof(requireStateAuthority) + "' instead.")]
        public bool requireNetworkOwnership;
        [HideInInspector, Obsolete("Please use '" + nameof(spawnOnStart) + "' instead.")]
        public bool loadOnStart;

        #endregion

        /// <summary>
        /// Invoked when the prefab is spawned.
        /// </summary>
        public event Action<GameObject> Spawned;

        /// <summary>
        /// Invoked when a <see cref="MetaPrefabSpawner"/> spawns a new prefab.
        /// </summary>
        public static event Action<MetaPrefabSpawner, GameObject> SpawnerPrefabSpawned;

        /// <summary>
        /// The prefab that was spawned last.
        /// </summary>
        public GameObject SpawnedPrefab { get; private set; }

        /// <summary>
        /// The ID of the prefab to spawn.
        /// </summary>
        public Guid? ID => Guid.TryParse(prefab, out var id) ? id : null;

        /// <summary>
        /// True if the prefab is currently being loaded/spawned.
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// The unique cross-device ID of this particular spawner.
        /// </summary>
        public Guid? SpawnerID { get; private set; }

        /// <summary>
        /// Generic cross-device data that this spawner contains.
        /// </summary>
        public object SpawnerData { get; private set; }

        /// <summary>
        /// The cached prefab data transfer object.
        /// </summary>
        public PrefabDto CachedDto { get; private set; }

        /// <summary>
        /// Events for this spawner.
        /// </summary>
        public MetaPrefabSpawnerEvents Events => events;

        /// <summary>
        /// Requirements necessary to load this spawner.
        /// </summary>
        private ObjectLoadRange LoadRequirements { get; set; }

        /// <summary>
        /// The networking service current active in the meta space (if any).
        /// </summary>
        private IMetaSpaceNetworkingService NetworkingService => _networkingService ??= MetaSpace ? MetaSpace.GetService<IMetaSpaceNetworkingService>() : null;

        /// <summary>
        /// The network object attached to this spawner.
        /// </summary>
        private NetworkObject NetworkObject {
            get {
                if (!networkObject) networkObject = GetComponent<NetworkObject>();
                return networkObject;
            }
        }

        public bool AllowUnlistedPrefabsToSpawn { get; set; } = true;

        public bool PreferPreReleaseVersion { get; set; } = true;

        #region Unity Events

        private void Reset()
        {
            SetDefaultInspectorValues();
        }

        private void OnValidate()
        {
            UpgradeFields();
            EditorValidate();
        }

        protected override void Awake()
        {
            UpgradeFields();

            base.Awake();

            Initialize();
        }

        private void Start()
        {
            if (TryIdentifySpawnedNetworkObject())
                return;

            if (LoadRequirements != null && ID != null)
            {
                CameraDistanceManager.Instance.AddMeasurer(this);
                return;
            }

            SpawnOnStart();
        }

        private void OnDisable()
        {
            CancelLoad();
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;

            _isDestroyed = true;

            base.OnDestroy();

            Dispose();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Spawns the prefab.
        /// </summary>
        public void Spawn()
        {
            if (_failed) return;
            if (IsLoading) return;
            if (!isActiveAndEnabled) return;
            IsLoading = true;
            MetaverseProgram.OnInitialized(() =>
            {
                if (!this)
                    return;

                CancelLoad();
                if (!isActiveAndEnabled)
                {
                    IsLoading = false;
                    return;
                }

                UniTask.Create(async () => await SpawnAsync(_metaPrefabSpawnerCancellation.Token));
            });
        }

        /// <summary>
        /// Destroys the last spawned object.
        /// </summary>
        public void DestroyLastSpawnedObject()
        {
            if (SpawnedPrefab)
                Destroy(SpawnedPrefab);
        }

        /// <summary>
        /// Begin loading this object.
        /// </summary>
        [Obsolete("Please use '" + nameof(Spawn) + "()' instead.")]
        public void Load()
        {
            Spawn();
        }

        /// <summary>
        /// Checks whether this object can be spawned by this local client.
        /// If <see cref="requireNetworkOwnership"/> is true, it is best to
        /// call <see cref="WaitForNetworkInitialization"/> before calling this
        /// function.
        /// </summary>
        /// <returns></returns>
        public bool HasSpawnAuthority()
        {
            if (!this)
                // This object is marked for deletion.
                return false;

            if (_metaPrefabSpawnerCancellation.IsCancellationRequested)
                // The spawn was cancelled.
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

        /// <summary>
        /// Assigns the <see cref="CachedDto"/> value. This is only valid if within a meta space.
        /// </summary>
        /// <returns></returns>
        public async Task CacheMetaPrefabDtoAsync(CancellationToken cancellationToken = default)
        {
            if (ID == null)
                return;

            var id = ID.Value;
            await UniTask.WaitUntil(() => !CurrentlyCachingIDs.Contains(id), cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                if (CachedPrefabDtos.TryGetValue(id, out var cachedDto))
                {
                    CachedDto = cachedDto;
                    prefab = CachedDto?.Id.ToString();
                }
                else if (MetaSpace && MetaSpace.TryGetCachedValue(MetaPrefabDtoCachePrefix + id, out var cachedDtoObj))
                {
                    CachedDto = cachedDtoObj as PrefabDto;
                    prefab = CachedDto?.Id.ToString();
                }
                else
                {
                    CurrentlyCachingIDs.Add(id);

                    var response = await MetaverseProgram.ApiClient.Prefabs.FindAsync(id);
                    if (response.Succeeded)
                    {
                        var resultAsync = await response.GetResultAsync();
                        CachedDto = resultAsync;

                        if (PreferPreReleaseVersion &&
                            resultAsync.ReviewVersionId is not null && 
                            resultAsync.HasWriteAccess == true)
                        {
                            var isInPrivateSpace = 
                                !MetaSpace || 
                                MetaSpace.NetworkingService.IsOfflineMode || 
                                MetaSpace.NetworkingService.Private;
                            
                            if (isInPrivateSpace)
                            {
                                response = await MetaverseProgram.ApiClient.Prefabs.FindAsync(resultAsync.ReviewVersionId.Value);
                                if (response.Succeeded)
                                {
                                    resultAsync = await response.GetResultAsync();
                                    if (resultAsync.Platforms.Count > 0 && resultAsync.Platforms.GetDocumentForCurrentPlatform() is not null)
                                    {
                                        prefab = resultAsync.Id.ToString();
                                        CachedDto.Id = resultAsync.Id;
                                        CachedDto.Platforms = resultAsync.Platforms;

                                        if (MetaSpace) MetaSpace.SetCachedValue(MetaPrefabDtoCachePrefix + CachedDto.Id, CachedDto);
                                        else CachedPrefabDtos[CachedDto.Id] = CachedDto;
                                    }
                                }
                            }
                        }
                    }

                    if (MetaSpace) MetaSpace.SetCachedValue(MetaPrefabDtoCachePrefix + id, CachedDto);
                    else CachedPrefabDtos[id] = CachedDto;
                }
            }
            finally
            {
                CurrentlyCachingIDs.Remove(id);
            }
        }

        /// <summary>
        /// If we're in a meta space or part of a network scene, this function waits
        /// until both the meta space and the network service are ready.
        /// </summary>
        /// <param name="cancellationToken">To cancel the operation.</param>
        public async Task WaitForNetworkInitialization(CancellationToken cancellationToken = default)
        {
            if (MetaSpaces.MetaSpace.Instance &&
                gameObject.scene == MetaSpaces.MetaSpace.Instance.gameObject.scene &&
                NetworkingService is null or { IsReady: false })
                await UniTask.WaitUntil(() => NetworkingService is { IsReady: true }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Create a new <see cref="MetaPrefabSpawner"/> with parameters.
        /// </summary>
        /// <param name="prefabId">The GUID of the prefab to spawn.</param>
        /// <param name="position">The position to spawn the prefab.</param>
        /// <param name="rotation">The rotation to spawn the prefab.</param>
        /// <param name="prefabParent">The parent to use for the prefab being spawned.</param>
        /// <param name="spawnerParent">The parent to use for the <see cref="MetaPrefabSpawner"/> that is created by this method.</param>
        /// <param name="loadRequirements">Requirements necessary to load this prefab. NOTE: If not specified, this will be pulled from the <see cref="PrefabDto"/>.</param>
        /// <param name="loadOnStart">Whether or not to load this prefab when the Unity Start() event is invoked (and if spawn requirements are met).</param>
        /// <param name="requireStateAuthority">Whether spawning this object requires network authority.</param>
        /// <param name="spawnerID">The cross-device unique ID of this spawner.</param>
        /// <param name="spawnerData">Cross-device custom data to put within the spawner.</param>
        /// <returns>The created spawner.</returns>
        public static MetaPrefabSpawner CreateSpawner(
            Guid prefabId,
            Vector3 position,
            Quaternion rotation,
            Transform prefabParent = null,
            Transform spawnerParent = null,
            ObjectLoadRange loadRequirements = null,
            bool loadOnStart = true,
            bool requireStateAuthority = false,
            Guid? spawnerID = null,
            object spawnerData = null)
        {
            var mps = new GameObject(prefabId.ToString()).AddComponent<MetaPrefabSpawner>();

            var tr = mps.transform;
            tr.SetParent(spawnerParent);
            tr.SetPositionAndRotation(position, rotation);
            tr.localScale = Vector3.one;

            mps.parent = prefabParent;
            mps.prefab = prefabId.ToString();
            mps.requireStateAuthority = requireStateAuthority;
            mps.spawnOnStart = loadOnStart;
            mps.SpawnerID = spawnerID;
            mps.SpawnerData = spawnerData;
            mps.LoadRequirements = loadRequirements;
            mps.retryAttempts = 0;

            if (spawnerID != null)
                CacheSpawner(spawnerID.Value, mps);

            return mps;
        }

        public static MetaPrefabSpawner FindBySpawnerID(Guid spawnerID)
        {
            if (!MetaSpaces.MetaSpace.Instance) return null;
            var val = MetaSpaces.MetaSpace.Instance.TryGetCachedValue(SpawnerCachePrefix + spawnerID, out var sp) ? (MetaPrefabSpawner)sp : null;
            return val ? val : null;
        }

        #endregion

        #region Private Methods

        private void UpgradeFields()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (loadOnStart)
            {
                spawnOnStart = loadOnStart;
                loadOnStart = false;
            }

            if (requireNetworkOwnership)
            {
                requireStateAuthority = requireNetworkOwnership;
                requireNetworkOwnership = false;
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private void Initialize()
        {
            events ??= new MetaPrefabSpawnerEvents();
            events.onFailed.AddListener(OnLoadFailed);
        }

        private void SetDefaultInspectorValues()
        {
            spawnOnStart = true;
            parent = transform;
            networkObject = GetComponentInParent<NetworkObject>(true);
        }

        private void EditorValidate()
        {
            if (Application.isPlaying)
                return;

            ValidatePrefabID();
        }

        private void Dispose()
        {
            CancelLoad();

            if (SpawnedPrefab && syncDestroy)
                Destroy(SpawnedPrefab);

            if (networkObject)
                networkObject.Initialized -= OnNetworkInitialized;

            OnDestroyInternal();
        }

        private static void CacheSpawner(Guid spawnerID, MetaPrefabSpawner spawner)
        {
            if (!spawner)
                return;
            if (MetaSpaces.MetaSpace.Instance)
                MetaSpaces.MetaSpace.Instance.SetCachedValue(SpawnerCachePrefix + spawnerID, spawner);
            spawner.SpawnerID = spawnerID;
        }

        private void SpawnOnStart()
        {
            if (!spawnOnStart)
                return;

            if (NetworkObject)
            {
                if (NetworkObject.IsInitialized)
                    Spawn();
                else
                    NetworkObject.Initialized += OnNetworkInitialized;
            }
            else
            {
                Spawn();
            }
        }

        private void CancelLoad()
        {
            _metaPrefabSpawnerCancellation?.Cancel();
            _metaPrefabSpawnerCancellation = new CancellationTokenSource();
        }

        private void RetryDownload()
        {
            if (retryAttempts == 0)
            {
                _failed = true;
                return;
            }

            _numberOfRetries++; // Increase the number of retries.

            if (_numberOfRetries < retryAttempts)
                MetaverseDispatcher.WaitForSeconds(retryCooldown, () =>
                {
                    // Make sure we can spawn.
                    if (HasSpawnAuthority()) Spawn();
                });
            else
            {
                _numberOfRetries = 0;
                _failed = true;
            }
        }

        private void OnLoadFailed(object error)
        {
            RetryDownload();
        }

        private void OnNetworkInitialized()
        {
            networkObject.Initialized -= OnNetworkInitialized;

            if (requireStateAuthority)
            {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                networkObject.InternalController.NetworkView.LocalStateAuthority += OnAuthority;
#endif
            }

            // Load when the network object is finished initializing. 
            // We'll consider that our "start" method when online.
            if (spawnOnStart)
                Spawn();
        }

        private void OnAuthority()
        {
            var isThisObjectWaitingToBeSpawned =
                spawnOnStart &&
                requireStateAuthority &&
                NetworkingService.IsHost &&
                SpawnerID != null &&
                !SpawnedPrefab;

            if (isThisObjectWaitingToBeSpawned)
            {
                Spawn();
            }
        }

        private async Task SpawnAsync(CancellationToken cancellationToken = default)
        {
            if (MetaverseProgram.IsQuitting)
                return;

            const string prefabIdIsInvalidError = "Prefab ID is invalid.";
            const string alreadySpawnedError = "Already spawned.";
            const string unlistedContentError = "You cannot spawn unlisted content.";

            ValidatePrefabID();

            if (prefab == null)
            {
                OnFailed(prefabIdIsInvalidError);
                return;
            }

            if (SpawnedPrefab && !concurrent)
            {
                OnFailed(alreadySpawnedError);
                return;
            }

            // Ensure that the scene is initialized.
            await WaitForNetworkInitialization(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                FailAndDontRetry();
                return;
            }

            // Check if we have authority to spawn this prefab.
            if (!HasSpawnAuthority())
            {
                FailAndDontRetry();
                return;
            }

            // Cache the prefab data transfer object in the meta space.
            await CacheMetaPrefabDtoAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                FailAndDontRetry();
                return;
            }

            if (!AllowUnlistedPrefabsToSpawn)
            {
                if (CachedDto is null)
                {
                    FailAndDontRetry();
                    return;
                }

                if (!CachedDto.Listings.HasFlag(AssetListings.Main) &&
                    !CachedDto.Listings.HasFlag(AssetListings.Blockchain))
                {
                    FailAndDontRetry(unlistedContentError);
                    return;
                }
            }

            if (CachedDto is not null && CachedDto.IsReviewVersion && CachedDto.HasWriteAccess == false)
            {
                // Don't want to spawn pending review content on clients
                // that don't have write access.
                FailAndDontRetry();
                return;
            }

            // If the object has load requirements that are out of range,
            // we want to just wait until the load requirements load the
            // object.
            if (TryFetchLoadRequirements())
            {
                Finish();
                return;
            }

            if (!this)
                return;

            if (ID is null)
            {
                FailAndDontRetry();
                return;
            }

            events.onStartedLoading?.Invoke();

            MetaPrefabLoadingAPI.LoadPrefab(
                ID!.Value,
                // System level objects will reference the DontDestroyOnLoad scene. They would require a 
                // full app refresh to remove from memory.
                MetaSpace ? gameObject.scene : MVUtils.GetDontDestroyOnLoadScene(),
                loadedPrefab =>
                {
                    OnPrefabLoaded(loadedPrefab, cancellationToken);
                },
                progress: progress =>
                {
                    if (!this) return;
                    events.onProgress?.Invoke(progress);
                },
                failed: error =>
                {
                    if (!this) return;
                    OnFailed(error);
                },
                cachedDto: CachedDto,
                cancellationToken: cancellationToken);
        }

        private void OnPrefabLoaded(GameObject loadedPrefab, CancellationToken cancellationToken)
        {
            if (!this)
                // This object doesn't exist anymore.
                return;

            if (!loadedPrefab)
            {
                // The prefab has no content.
                FailAndDontRetry();
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                FailAndDontRetry();
                return;
            }

            if (MetaverseProgram.IsQuitting)
                return;

            // Reset the retries.
            _numberOfRetries = 0;

            if (NetworkingService != null && loadedPrefab.GetComponent<NetworkObject>())
            {
                object[] instData = null;
                if (SpawnerID != null)
                {
                    instData = new object[2];
                    if (SpawnerID != null) instData[0] = SpawnerID.Value;
                    if (SpawnerData != null) instData[1] = SpawnerData;
                }

                NetworkingService.SpawnGameObject(
                    loadedPrefab,
                    no =>
                    {
                        if (!this)
                        {
                            no.IsStale = true;
                            return;
                        }
                        OnSpawned(no.GameObject);
                    },
                    transform.position,
                    transform.rotation,
                    serverOwned: false,
                    channel: NetworkObject ? NetworkObject.Channel : (byte)0,
                    instData);
                return;
            }

            var instance = Instantiate(loadedPrefab, transform.position, transform.rotation);
            OnSpawned(instance);
        }

        private void FailAndDontRetry(object error = null)
        {
            _numberOfRetries = retryAttempts;
            OnFailed(error);
        }

        private bool TryFetchLoadRequirements()
        {
            if (!MetaSpace || LoadRequirements != null)
                return false;

            if (CachedDto != null)
            {
                LoadRequirements = new ObjectLoadRange(CachedDto.PrefabLoadDistance, CachedDto.PrefabUnloadDistance);
                if (CachedDto.PrefabLoadDistance > 0 || CachedDto.PrefabUnloadDistance > 0)
                    CameraDistanceManager.Instance.AddMeasurer(this);
            }

            return LoadRequirements is { loadDistance: > 0 };
        }

        private void OnSpawned(GameObject instance)
        {
            if (!instance)
            {
                OnFailed("Instance was null");
                return;
            }

            if (SpawnedPrefab == instance)
                return;

            SpawnedPrefab = instance;
            SpawnedPrefab.OnDestroy(cb =>
            {
                if (!this) return;
                if (!cb) return;
                if (cb.gameObject != SpawnedPrefab) return;
                if (_isDestroyed) return;
                SpawnedPrefab = null;
                events.onLastSpawnedObjectDestroyed?.Invoke();
            });
            
            var mp = instance.GetComponent<MetaPrefab>();
            if (mp) mp.Spawner = this;

            if (IsParentValid())
            {
                var instanceT = instance.transform;
                var originalScale = instanceT.localScale;
                instanceT.SetParent(parent);
                instanceT.SetPositionAndRotation(transform.position, transform.rotation);
                instanceT.localScale = originalScale;
            }

            if (spawnAddons is { Length: > 0 })
            {
                var netService = MetaSpace.GetService<IMetaSpaceNetworkingService>();
                foreach (var addon in spawnAddons)
                {
                    if (!addon) continue;
                    if (addon.gameObject.GetComponent<NetworkObject>() && netService is not null)
                    {
                        netService
                            .SpawnGameObject(addon.gameObject, no =>
                            {
                                if (!this || !instance)
                                {
                                    no.IsStale = true;
                                    return;
                                }
                                ApplyParent(no.GameObject);
                                
                            }, Vector3.zero, Quaternion.identity, false);
                    }
                    else
                    {
                        ApplyParent(Instantiate(addon.gameObject, Vector3.zero, Quaternion.identity));
                    }

                    continue;
                    
                    void ApplyParent(GameObject obj)
                    {
                        if (!obj) return;
                        obj.transform.SetParent(instance.transform);
                        obj.transform.ResetLocalTransform();
                    }
                }
            }

            NotifySpawned();
            return;

            void NotifySpawned()
            {
                IsLoading = false;
                Spawned?.Invoke(instance);
                SpawnerPrefabSpawned?.Invoke(this, instance);
                events.onSpawned?.Invoke(instance);
                events.onFinishedLoading?.Invoke();
            }
        }

        private bool IsParentValid()
        {
            if (!parent) return false; // If there's no parent then no this is not valid.

            var isNetworkObj = SpawnedPrefab ? SpawnedPrefab.GetComponent<NetworkObject>() : null;
            if (!isNetworkObj) return true; // If we don't even have networking then yes this is fine.

            if (parent == transform && SpawnerID != null) return true; // If we have a way to identify this spawner, this is fine.

            return parent.GetComponent<NetworkTransform>() || // The parent has a network transform.
                   parent.GetComponent<NetworkObject>() || // The parent is a network object.
                   parent.GetComponent<LandPlot>(); // The parent is a land plot.
        }

        private void OnFailed(object error = null)
        {
            if (error != null) MetaverseProgram.Logger?.LogWarning(error);
            IsLoading = false;
            events.onFinishedLoading?.Invoke();
            events.onFailed?.Invoke(error?.ToString() ?? string.Empty);
        }

        private void Finish()
        {
            IsLoading = false;
            events.onFinishedLoading?.Invoke();
        }

        private void ValidatePrefabID()
        {
            if (!Guid.TryParse(prefab, out _))
                prefab = null;
        }

        private bool TryIdentifySpawnedNetworkObject()
        {
            if (SpawnerID == null || !requireStateAuthority)
                return false;

            var success = false;
            TryIdentifySpawnedNetworkObjectInternal(ref success);
            return success;
        }

        #endregion

        #region Partial Methods

        partial void TryIdentifySpawnedNetworkObjectInternal(ref bool success);

        partial void OnDestroyInternal();

        #endregion

        #region IMeasureCameraDistance

        Vector3 IMeasureCameraDistance.CameraMeasurementPosition => this ? transform.position : Vector3.zero;

        void IMeasureCameraDistance.OnCameraDistance(Camera cam, float sqrDistance)
        {
            if (_failed) return;

            var distanceToCamera = Mathf.Sqrt(sqrDistance);
            if (!SpawnedPrefab && !IsLoading && LoadRequirements.ShouldLoad(distanceToCamera))
            {
                Spawn();
            }
            else if ((SpawnedPrefab || IsLoading) && LoadRequirements.ShouldUnload(distanceToCamera))
            {
                if (IsLoading)
                    CancelLoad();
                else if (SpawnedPrefab)
                {
                    var netObj = SpawnedPrefab.GetComponent<NetworkObject>();
                    if (!netObj)
                        Destroy(SpawnedPrefab);
                    else
                        CameraDistanceManager.Instance.RemoveMeasurer(this); // Network objects should not get unloaded like this.
                }
            }
        }

        #endregion
    }
}