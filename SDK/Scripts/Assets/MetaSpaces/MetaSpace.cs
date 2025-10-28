using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Audio.Poco;
using MetaverseCloudEngine.Unity.Audio.Abstract;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces.Abstract;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Services.Implementation;
using MetaverseCloudEngine.Unity.Networking.Impl;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Video.Abstract;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using TriInspectorMVCE;
using MetaverseCloudEngine.Unity.Web.Abstract;
using MetaverseCloudEngine.Unity.Web.Implementation;
using Unity.VisualScripting;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    /// <summary>
    /// Meta Spaces are a primary concept in the Metaverse Cloud Engine. They are
    /// how a user interacts with the world and other users. Think of a Meta Space
    /// like a scene in Unity. However, only 1 Meta Space can be active at a time.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [DefaultExecutionOrder(ExecutionOrder.PostInitialization)]
    [DeclareBoxGroup("MetaSpace Options")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Metaverse Assets/Spaces/Meta Space")]
    [HelpURL("https://reach-cloud.gitbook.io/reach-explorer-documentation/docs/development-guide/unity-engine-sdk/components/assets/meta-space")]
    public partial class MetaSpace : Asset<MetaSpaceMetadata>
    {
        #region Inspector

        [Group("MetaSpace Options")]
        [SerializeField]
        [Tooltip("Options related to how the Meta Space behaves at runtime.")]
        private MetaSpaceRuntimeOptions runtime = new();
        [Group("MetaSpace Options")]
        [SerializeField]
		[Tooltip("Options related to how players are spawned in the Meta Space.")]
        private MetaSpacePlayerSpawnOptions playerSpawn = new();
        [Group("MetaSpace Options")]
        [SerializeField]
		[Tooltip("Options related to player groups in the Meta Space.")]
        private MetaSpacePlayerGroupOptions playerGroups = new();
        [Group("MetaSpace Options")]
        [SerializeField]
		[Tooltip("Options related to networking in the Meta Space.")]
        private MetaSpaceNetworkOptions network = new();
        [Group("MetaSpace Options")]
        [SerializeField]
		[Tooltip("Options related to integrating the Meta Space with other systems.")]
        private MetaSpaceIntegrationOptions integrations = new();

        [SerializeField]
		[HideInInspector] private bool useDeprecatedQualityApi = true;

        #endregion

        #region Fields

        private readonly Dictionary<Type, IMetaSpaceService> _metaSpaceServices = new();

#if UNITY_EDITOR
        private static IMetaSpaceSceneSetup[] _sceneSetupClasses;
#endif

        private readonly Dictionary<object, object> _sceneCache = new();
        private readonly HashSet<Guid> _preAllocatedPrefabIds = new();
        private readonly List<Action> _initializationFailureActions = new();

        private bool _started;
        private static MetaSpace _instance;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently loaded meta space data.
        /// </summary>
        public static MetaSpaceDto CurrentlyLoadedMetaSpaceDto {
            get {
                MetaSpaceDto dto = null;
                GetLoadedSpaceDataInternal(ref dto);
                return dto;
            }
        }

        /// <summary>
        /// Options related to how the Meta Space behaves at runtime.
        /// </summary>
        public MetaSpaceRuntimeOptions RuntimeOptions => runtime;

        /// <summary>
        /// Options related to how players are spawned in the Meta Space.
        /// </summary>
        public MetaSpacePlayerSpawnOptions PlayerSpawnOptions => playerSpawn;

        /// <summary>
        /// Options related to networking in the Meta Space.
        /// </summary>
        public MetaSpaceNetworkOptions NetworkOptions => network;

        /// <summary>
        /// Options related to player groups in the Meta Space.
        /// </summary>
        public MetaSpacePlayerGroupOptions PlayerGroupOptions => playerGroups;

        /// <summary>
        /// Gets the external API service for the Meta Space.
        /// </summary>
        public IMetaSpaceExternalApiService ExternalApiService => GetService<IMetaSpaceExternalApiService>();

        /// <summary>
        /// The current instance of the <see cref="MetaSpace"/> component.
        /// </summary>
        public static MetaSpace Instance {
            get {
                if (!_instance)
                    _instance = FindObjectOfType<MetaSpace>(true);
                return _instance;
            }
            private set => _instance = value;
        }

        /// <summary>
        /// Whether the Meta Space has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Event that is raised when the Meta Space is initialized.
        /// </summary>
        public event Action Initialized;

        /// <summary>
        /// Whether the Meta Space's services have been registered.
        /// </summary>
        public bool RegisteredServices { get; private set; }

        /// <summary>
        /// Event that is raised when the Meta Space's services are registered.
        /// </summary>
        public event Action ServicesRegistered;

        /// <summary>
        /// Whether the Meta Space is currently loading prefabs.
        /// </summary>
        public bool LoadingPrefabs { get; private set; }

        /// <summary>
        /// Whether the Meta Space has finished loading prefabs.
        /// </summary>
        public bool LoadedPrefabs { get; private set; }

        /// <summary>
        /// Event that is raised when the Meta Space starts loading prefabs.
        /// </summary>
        public event Action LoadingPrefabsStarted;

        /// <summary>
        /// Event that is raised when the Meta Space finishes loading prefabs.
        /// </summary>
        public event Action LoadingPrefabsCompleted;
        
        /// <summary>
        /// The service that provides multiplayer networking functionality.
        /// </summary>
        public IMetaSpaceNetworkingService NetworkingService => GetService<IMetaSpaceNetworkingService>();
        
        /// <summary>
        /// The service that provides management of player groups, similar to teams in pvp games.
        /// </summary>
        public IPlayerGroupsService PlayerGroupsService => GetService<IPlayerGroupsService>();
        
        /// <summary>
        /// The service that provides management of the state of the Meta Space (started, not started, etc).
        /// </summary>
        public IMetaSpaceStateService StateService => GetService<IMetaSpaceStateService>();
        
        /// <summary>
        /// The service that provides management of player spawning.
        /// </summary>
        public IPlayerSpawnService PlayerSpawnService => GetService<IPlayerSpawnService>();
        
        /// <summary>
        /// The service that provides management of the microphone.
        /// </summary>
        public IMicrophoneService MicrophoneService => GetService<IMicrophoneService>();
        
        /// <summary>
        /// The service that provides management of the video camera.
        /// </summary>
        public IVideoCameraService VideoCameraService => GetService<IVideoCameraService>();
        
        #endregion

        #region Unity Events

        protected override void Awake()
        {
            if (!Application.isPlaying) return;
#if PLAYMAKER
            if (PlayMakerGUI.Instance)
                PlayMakerGUI.Instance.controlMouseCursor = false;
#endif
            Instance = this;
            AwakeInternal();
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            base.OnDestroy();
            
            ResetApplicationRuntimeSettings();
            DisposeServices();
            OnDestroyInternal();

            if (!IsInitialized)
                foreach (var act in _initializationFailureActions)
                    act?.Invoke();
            
            foreach (var pId in _preAllocatedPrefabIds)
                MetaPrefabLoadingAPI.UnRegisterPrefabInstance(pId);
        }

        partial void OnDestroyInternal();

        protected override void Reset()
        {
            base.Reset();

            playerGroups.Validate();
            integrations.Validate();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            playerGroups.Validate();
            integrations.Validate();

            if (!Application.isPlaying)
                useDeprecatedQualityApi = false;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            _started = true;
        }

        private void OnDisable()
        {
            enabled = true;
        }

        #endregion

        #region Public Methods

        public void SetCachedValue(object key, object value)
        {
            _sceneCache[key] = value;
        }

        public bool TryGetCachedValue(object key, out object value)
        {
            return _sceneCache.TryGetValue(key, out value);
        }

        public void RemoveFromCache(object key)
        {
            _sceneCache.Remove(key);
        }

        public T GetService<T>() where T : IMetaSpaceService => _metaSpaceServices.TryGetValue(typeof(T), out var s) ? (T)s : default;

        public object GetService(Type t) => _metaSpaceServices.TryGetValue(t, out var s) ? s : default;

        public static void OnReady(Action action, Action onFailed = null)
        {
            OnReady(_ => action?.Invoke());
        }

        public static void OnReady(Action<MetaSpace> action, Action onFailed = null)
        {
            try
            {
                if (Instance is null)
                {
                    onFailed?.Invoke();
                    return;
                }

                if (Instance.IsInitialized)
                {
                    action?.Invoke(Instance);
                    return;
                }

                Instance.Initialized += () => MetaverseDispatcher.AtEndOfFrame(() => action?.Invoke(Instance));
                Instance._initializationFailureActions.Add(onFailed);
            }
            catch (Exception e)
            {
                if (e.GetBaseException() is NullReferenceException nrf)
                {
                    if (!Instance)
                    {
                        try { onFailed?.Invoke(); } catch (Exception e2) { /* ignored */ }
                        return;
                    }
                }
                
                MetaverseProgram.Logger.LogError(
                    $"[METASPACE] Failed to register action for MetaSpace.OnReady: {e.GetBaseException()}");
            }
        }

        /// <summary>
        /// Checks the API to see if there's an update available to the currently loaded meta space.
        /// </summary>
        /// <param name="updateAvailable">Invoked if there's an available update.</param>
        /// <param name="upToDate">Invoked when the MetaSpace is up to date.</param>
        /// <param name="onError">Invoked when there was an error fetching the meta space status.</param>
        public void CheckForUpdate(Action<MetaSpaceDto> updateAvailable, Action upToDate = null, Action<object> onError = null)
        {
            OnReady(() =>
            {
                if (CurrentlyLoadedMetaSpaceDto is null)
                {
                    onError?.Invoke("MetaSpace has not fully loaded.");
                    return;
                }
                MetaverseProgram.ApiClient
                    .MetaSpaces.FindAsync(CurrentlyLoadedMetaSpaceDto.Id)
                    .ResponseThen(r =>
                    {
                        if (r.UpdatedDate is not null && 
                            r.UpdatedDate > (
                                CurrentlyLoadedMetaSpaceDto.UpdatedDate ??
                                CurrentlyLoadedMetaSpaceDto.CreatedDate))
                        {
                            updateAvailable?.Invoke(r);
                            return;
                        }
                        upToDate?.Invoke();
                    }, e => onError?.Invoke(e));
                
            }, () => onError?.Invoke("MetaSpace failed to initialize."));
        }

        #endregion

        #region Private Methods

        private static void ResetApplicationRuntimeSettings()
        {
            MetaverseCursorAPI.UnlockCursor();
            Physics.gravity = MetaverseConstants.Physics.DefaultGravity;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = MetaverseConstants.Physics.DefaultFixedDeltaTime;
            MVUtils.SafelyAdjustXRResolutionScale(MetaverseConstants.XR.DefaultXRResolutionScale);
        }

        private IEnumerator InitializeRoutine()
        {
            yield return new WaitUntil(() => _started);

            InitializeServiceOptions();
            RegisterServices();

            LoadingPrefabs = true;
            try
            {
                LoadingPrefabsStarted?.Invoke();
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError($"[METASPACE] {e}");
            }

            yield return PreloadMetaPrefabsRoutine();

            LoadingPrefabs = false;
            LoadedPrefabs = true;
            try
            {
                LoadingPrefabsCompleted?.Invoke();
            }
            catch (Exception e) 
            { 
                MetaverseProgram.Logger.LogError($"[METASPACE] {e}");
            }

            InitializeServices();
            InitializeSceneInternal();

            IsInitialized = true;
            _initializationFailureActions.Clear();
            Initialized?.Invoke();
        }

        private IEnumerator PreloadMetaPrefabsRoutine()
        {
            var networking = GetService<IMetaSpaceNetworkingService>();
            MetaverseProgram.Logger.Log("[METASPACE] Establishing connection before preloading...");
            if (networking is not null)
                yield return new WaitUntil(() => networking.IsReady);
            MetaverseProgram.Logger.Log("[METASPACE] Beginning to preload prefabs...");
            
            var preloadedPrefabs = new List<Guid>();
            var prefabIds = ScanEntireSceneForPreLoadableMetaPrefabs(networking);
            var totalPrefabsToLoad = prefabIds.Length;

            const float pingInterval = 1f;
            foreach (var prefabId in prefabIds)
                LoadPrefab(prefabId);

            while (preloadedPrefabs.Count != totalPrefabsToLoad)
                yield return new WaitForSeconds(pingInterval);

            var loadingSpawners = FindObjectsOfType<MetaPrefabSpawner>()
                .Where(x => x.IsLoading)
                .ToArray();

            while (loadingSpawners.Any(x => x.IsLoading))
                yield return new WaitForSeconds(pingInterval);

            MetaverseProgram.Logger.Log($"[METASPACE] Finished pre-loading {preloadedPrefabs.Count} prefab(s).");
            yield break;

            void LoadPrefab(Guid pId, int retries = 3)
            {
                MetaPrefabLoadingAPI.LoadPrefab(pId, gameObject.scene, prefab =>
                {
                    if (!prefab)
                    {
                        preloadedPrefabs.Add(pId);
                        return;
                    }

                    if (_preAllocatedPrefabIds.Contains(pId))
                        MetaPrefabLoadingAPI.RegisterPrefabInstance(pId);
                    
                    var subPrefabIds = prefab.GetComponentsInChildren<MetaPrefabSpawner>()
                        .Where(x => x)
                        .SelectMany(x => x.gameObject.GetMetaPrefabSpawnerIds())
                        .Where(x => !preloadedPrefabs.Contains(x) && x != pId)
                        .Distinct()
                        .ToArray();

                    totalPrefabsToLoad += subPrefabIds.Length;
                    preloadedPrefabs.Add(pId);
                    foreach (var subPrefabId in subPrefabIds)
                        LoadPrefab(subPrefabId);

                }, failed: _ =>
                {
                    if (retries <= 0)
                    {
                        preloadedPrefabs.Add(pId);
                        return;
                    }

                    LoadPrefab(pId, retries - 1);
                });
            }
        }

        private Guid[] ScanEntireSceneForPreLoadableMetaPrefabs(IMetaSpaceNetworkingService networkService)
        {
            var implemented = false;
            var loadOnStartPrefabs = new List<MetaPrefabToLoadOnStart>();

            // Gets all the prefabs real asset metadata.
            GetLoadOnStartingPrefabsFromMetadataInternal(ref implemented, ref loadOnStartPrefabs);
            if (!implemented)
            {
                // Get all the prefabs from local if this is the SDK.
                loadOnStartPrefabs = MetaData.LoadOnStartPrefabs
                    .Where(x => Guid.TryParse(x.prefab, out _))
                    .ToList();
            }
            else
            {
                if (networkService is { IsHost: false }) // Remove all the prefabs we aren't allowed to load.
                    loadOnStartPrefabs.RemoveAll(x => x.spawnAuthority == MetaPrefabToLoadOnStart.SpawnMode.MasterClient);
            }

            var loadOnStartSpawners =
                loadOnStartPrefabs.Where(x => Guid.TryParse(x.prefab, out _) && !x.disabled).Select(loadOnStartPrefab =>
                {
                    var spawner = MetaPrefabSpawner.CreateSpawner(
                        Guid.Parse(loadOnStartPrefab.prefab),
                        position: Vector3.zero,
                        rotation: Quaternion.identity,
                        requireStateAuthority: loadOnStartPrefab.spawnAuthority == MetaPrefabToLoadOnStart.SpawnMode.MasterClient,
                        loadOnStart: loadOnStartPrefab.spawnAuthority != MetaPrefabToLoadOnStart.SpawnMode.PreloadOnly);
                    spawner.retryAttempts = 999;
                    return spawner;
                }).ToList();
            
            _preAllocatedPrefabIds.AddRange(loadOnStartSpawners.Select(x => x.ID!.Value));

            return FindObjectsOfType<MetaPrefabSpawner>().SelectMany(y => y.gameObject.GetMetaPrefabSpawners())
                .SelectMany(x => x.gameObject.GetMetaPrefabSpawners())
                .Concat(network.EmbeddedResources.SelectMany(x => x.GetMetaPrefabSpawners()))
                .Concat(playerSpawn.DefaultPlayerPrefab.GetMetaPrefabSpawners())
                .Concat(playerGroups.PlayerGroups.SelectMany(x => x.playerAvatars.Where(a => a.avatarPrefab).SelectMany(a => a.avatarPrefab.gameObject.GetMetaPrefabSpawners())))
                .Concat(loadOnStartSpawners)
                .Where(x => x.ID.HasValue)
                .Select(x => x.ID.Value)
                .Distinct()
                .ToArray();
        }

        private void InitializeServiceOptions()
        {
            network.Initialize(playerSpawn.DefaultPlayerPrefab, playerGroups);
            playerGroups.Initialize();
        }

        private void AddService<T>(T t) where T : IMetaSpaceService => _metaSpaceServices[typeof(T)] = t;

        private void RegisterServices()
        {
            var logger = new UnityDebugLogger();

            AddService<IMetaSpaceNetworkingService>(
                new MetaSpaceNetworkingService(network, network, logger));

            AddService<IPlayerGroupsService>(new PlayerGroupService(
                GetService<IMetaSpaceNetworkingService>(),
                PlayerGroupOptions,
                logger));

            AddService<IMetaSpaceStateService>(new MetaSpaceStateService(
                GetService<IMetaSpaceNetworkingService>(),
                GetService<IPlayerGroupsService>(),
                RuntimeOptions));

            AddService<IPlayerSpawnService>(new PlayerSpawnService(
                GetService<IMetaSpaceNetworkingService>(),
                GetService<IMetaSpaceStateService>(),
                GetService<IPlayerGroupsService>(),
                PlayerSpawnOptions,
                NetworkOptions,
                logger));

            if (NetworkOptions.TotalMaxPlayers != 1)
            {
                var commService = new InternalCommunicationsService();
                AddService<IMicrophoneService>(commService);
                AddService<IVideoCameraService>(commService);
            }

            AddService<IMetaSpaceExternalApiService>(
                new MetaSpaceExternalApiService());

            ServicesRegistered?.Invoke();
            RegisteredServices = true;
        }

        private void InitializeServices()
        {
            foreach (var service in _metaSpaceServices.Values.Where(service => service != null))
                service.Initialize();
        }

        private void DisposeServices()
        {
            foreach (var service in _metaSpaceServices.Values.Where(service => service != null))
                service.Dispose();
            _metaSpaceServices.Clear();
        }

#if UNITY_EDITOR

        private static MetaSpace _editorMetaSpaceReference;

        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorInit()
        {
            _sceneSetupClasses = MVUtils.CreateClassInstancesOfType<IMetaSpaceSceneSetup>();
            UnityEditor.EditorApplication.hierarchyChanged += SetupScene;
        }

        [UnityEditor.MenuItem(MetaverseConstants.MenuItems.GameObjectMenuRootPath + "Meta Space")]
        private static void CreateMetaspace()
        {
            var existingMetaSpace = FindObjectOfType<MetaSpace>();
            if (existingMetaSpace)
            {
                UnityEditor.Selection.activeGameObject = existingMetaSpace.gameObject;
                return;
            }

            try
            {
                var resource = Resources.LoadAsync<GameObject>(MetaverseConstants.Resources.MetaSpace);
                resource.completed += _ =>
                {
                    if (resource.asset == null || resource.asset is not GameObject go || !go.TryGetComponent(out MetaSpace metaSpace))
                    {
                        GenerateStandardMetaSpace();
                        return;
                    }

                    metaSpace = ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(metaSpace.gameObject)).GetMetaSpace();
                    UnityEditor.Selection.activeGameObject = metaSpace.gameObject;
                };
            }
            catch
            {
                GenerateStandardMetaSpace();
            }
        }

        private static void GenerateStandardMetaSpace()
        {
            var metaSpace = new GameObject("Meta Space").AddComponent<MetaSpace>();
            UnityEditor.Selection.activeGameObject = metaSpace.gameObject;
        }

        private bool CanSetupScene()
        {
            var go = gameObject;
            return !Application.isPlaying &&
                   !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode &&
                   !UnityEditor.BuildPipeline.isBuildingPlayer &&
                   !UnityEditor.EditorApplication.isCompiling &&
                   go.scene.IsValid() &&
                   go.scene.isLoaded &&
                   !UnityEditor.EditorUtility.IsPersistent(go) &&
                   !UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go) &&
                   !UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        }

        private static void SetupScene()
        {
            if (UnityEditor.BuildPipeline.isBuildingPlayer ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!_editorMetaSpaceReference) _editorMetaSpaceReference = FindObjectOfType<MetaSpace>();
            if (!_editorMetaSpaceReference) return;
            if (!_editorMetaSpaceReference.CanSetupScene()) return;

            var mainCameras = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (var cam in mainCameras)
            {
                if (!cam || cam.scene != _editorMetaSpaceReference.gameObject.scene) continue;
                var didChange = false;
                foreach (var c in _sceneSetupClasses)
                {
                    if (cam && cam.TryGetComponent(out Camera camera))
                        didChange |= c.SetupMainCamera(camera);
                }
                if (didChange)
                {
                    UnityEditor.EditorUtility.SetDirty(cam);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                }
            }
        }
#endif

        #endregion

        #region Protected Methods

        protected override void OnMetaverseBehaviourInitialize(MetaverseRuntimeServices services)
        {
            if (!this) return;
            StartCoroutine(InitializeRoutine());
        }

        #endregion

        #region Partial Methods

        static partial void GetLoadedSpaceDataInternal(ref MetaSpaceDto dto);

        partial void GetLoadOnStartingPrefabsFromMetadataInternal(ref bool implemented, ref List<MetaPrefabToLoadOnStart> loadOnStartPrefabs);

        partial void InitializeSceneInternal();

        partial void AwakeInternal();

        #endregion
    }
}