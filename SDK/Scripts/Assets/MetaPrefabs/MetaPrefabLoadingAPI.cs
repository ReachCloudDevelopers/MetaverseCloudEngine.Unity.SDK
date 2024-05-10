using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using MetaverseCloudEngine.ApiClient.Abstract;
using MetaverseCloudEngine.ApiClient.Options;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Web.Implementation;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Common.Models.QueryParams;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    /// <summary>
    /// This class is responsible for loading meta prefabs from the API into the device's memory, allowing you to instantiate them at runtime. This
    /// class should also operate in the Unity Editor.
    /// It acts similar to the Unity Addressable system, but is more lightweight and does not require a separate build step.
    /// It integrates directly with the Metaverse Cloud Engine API, and will automatically download the meta prefab's bundle if it is not already
    /// downloaded.
    /// </summary>
    /// <remarks>
    /// You can also use the <see cref="MetaPrefabSpawner"/> component to spawn meta prefabs at runtime without writing any code.
    /// </remarks>
    /// <example>
    /// <code>
    /// public class LoadMetaPrefabExample : MonoBehaviour
    /// {
    ///     [MetaPrefabId]
    ///     public string prefabToLoad;
    /// 
    ///     private GameObject loadedPrefab;
    /// 
    ///     private void Start()
    ///     {
    ///         var metaPrefabId = Guid.Parse(prefabToLoad);
    ///         MetaPrefabLoadingAPI.LoadPrefab(metaPrefabId, gameObject.scene, prefab => loadedPrefab = prefab);
    ///     }
    /// 
    ///     private void Update()
    ///     {
    ///         if (Input.GetKeyDown(KeyCode.Space))
    ///         {
    ///             if (loadedPrefab == null)
    ///             {
    ///                 Debug.LogError("Prefab not loaded yet!");
    ///                 return;
    ///             }
    /// 
    ///             var metaPrefab = Instantiate(loadedPrefab);
    ///             // Do something with the meta prefab...
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public static class MetaPrefabLoadingAPI
    {
        private static readonly Dictionary<Guid, int> ObjectPoolInstanceCount = new();
        private static readonly HashSet<Guid> PrefabsToExpire = new();

        private static Dictionary<Scene, GameObject> _objectPoolContainers = new();
        private static Dictionary<Guid, GameObject> _objectPool = new();
        private static Dictionary<Guid, Guid> _childIdsToSourcePrefabIds = new();

        private static readonly Dictionary<Guid, bool> LoadRequestGameStateCache = new();
        private static readonly HashSet<Guid> DownloadingBundles = new();
        private static readonly HashSet<Guid> QueuedRequests = new();
        private static CancellationTokenSource _playModeCancellationToken = new();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            _playModeCancellationToken = new CancellationTokenSource();
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnCompilationStarted;
        }

        private static void OnCompilationStarted()
        {
            // Clear the pool upon compilation so that
            // we don't have any objects floating around
            // in the scene.
            ClearPool(true);
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Cleanup the pool upon exiting play mode / entering play mode.
            if (state != UnityEditor.PlayModeStateChange.ExitingEditMode &&
                state != UnityEditor.PlayModeStateChange.ExitingPlayMode)
                return;

            ClearPool(false);
            _playModeCancellationToken.Cancel();
            _playModeCancellationToken = new CancellationTokenSource();
        }
#endif

        /// <summary>
        /// Load a meta prefab from the API into the device's memory, allowing you to instantiate it at runtime. This function should also operate in
        /// the Unity Editor.
        /// </summary>
        /// <param name="id">The ID of the meta prefab to load.</param>
        /// <param name="scene">The scene that we want to spawn the meta prefab in.</param>
        public static void LoadPrefab(Guid id, Scene scene)
            => LoadPrefab(id, scene, null);

        /// <summary>
        /// Load a meta prefab from the API into the device's memory, allowing you to instantiate it at runtime. This function should also operate in
        /// the Unity Editor.
        /// </summary>
        /// <param name="id">The ID of the meta prefab to load.</param>
        /// <param name="scene">The scene that we want to spawn the meta prefab in.</param>
        /// <param name="loaded">Invoked when the meta prefab has finished loading.</param>
        /// <param name="cachedDto">Pass this to prevent another DTO lookup for the prefab.</param>
        /// <param name="progress">A progress tracking event that is invoked when the download progress has changed.</param>
        /// <param name="failed">Invoked when the prefab fails to load.</param>
        /// <param name="cancellationToken">An optional cancellation token to cancel the download.</param>
        /// <param name="gameStateLoadRequestId">This is used in editor to fix issues when loading prefabs and switching between play mode and edit mode. This can be ignored at runtime.</param>
        public static void LoadPrefab(
            Guid id,
            Scene scene,
            Action<GameObject> loaded,
            PrefabDto cachedDto = null,
            Action<float> progress = null,
            Action<object> failed = null,
            CancellationToken cancellationToken = default,
            Guid? gameStateLoadRequestId = null)
        {
            if (MetaverseProgram.IsQuitting)
            {
                failed?.Invoke("Application is quitting.");
                return;
            }

            cancellationToken = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, _playModeCancellationToken.Token).Token;

            if (CheckStale(scene, cancellationToken, gameStateLoadRequestId, failed) || DePool(id, loaded))
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                gameStateLoadRequestId ??= Guid.NewGuid();
                if (!LoadRequestGameStateCache.TryGetValue(gameStateLoadRequestId.Value, out _))
                    LoadRequestGameStateCache[gameStateLoadRequestId.Value] =
                        UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
            }
#endif

            MetaverseProgram.OnInitialized(() =>
            {
                if (CheckStale(scene, cancellationToken, gameStateLoadRequestId, failed))
                    return;

                if (QueueRequest(id, id, ref loaded, ref failed))
                    return;

                if (cachedDto != null && cachedDto.Id == id)
                {
                    OnGetTargetDto(cachedDto);
                }
                else
                {
                    MetaverseProgram.ApiClient.Prefabs.FindAsync(id) // Start by getting the prefab data.
                        .ResponseThen(OnGetTargetDto, failed, cancellationToken);
                }

                return;

                void OnGetTargetDto(PrefabDto targetPrefabDto)
                {
                    if (CheckStale(scene, cancellationToken, gameStateLoadRequestId, failed))
                        return;

                    CheckSecure(targetPrefabDto, () =>
                    {
                        if (targetPrefabDto.SourcePrefabId !=
                            null) // If this is a child prefab we want to query the source
                        {
                            var sourcePrefabId = targetPrefabDto.SourcePrefabId.GetValueOrDefault();
                            if (QueueRequest(sourcePrefabId, id, ref loaded, ref failed))
                                return;

                            // Try to de-pool before downloading to prevent querying the API.
                            var sourceId = targetPrefabDto.SourcePrefabId.GetValueOrDefault();
                            if (DePoolChildPrefab(id, sourceId, loaded, failed))
                                return;

                            if (cachedDto != null && cachedDto.Id == sourceId)
                                BeginDownload(cachedDto);
                            else
                            {
                                MetaverseProgram.ApiClient.Prefabs
                                    .FindAsync(sourceId)
                                    .ResponseThen(BeginDownload, failed,
                                        cancellationToken); // Begin download and pass in the source data.
                            }

                            return;
                        }

                        BeginDownload(targetPrefabDto); // Immediately begin download since this is a source prefab.
                    }, failed);
                }

                // Begin the download.
                void BeginDownload(PrefabDto prefabDto)
                {
                    if (CheckStale(scene, cancellationToken, gameStateLoadRequestId, failed))
                        return;

                    // It's possible that by the time this is called, 
                    // the object was pooled, so we want to check again.
                    if (DePoolChildPrefab(id, prefabDto.Id, loaded, failed))
                        return;

                    if (prefabDto.SourcePrefabId != null)
                    {
                        // This is a child prefab, it should not be downloaded directly.
                        failed?.Invoke(
                            $"Prefab '{prefabDto.Name} ({prefabDto.Id.ToString()[..4]}...)' is a child prefab and cannot be downloaded directly.");
                        return;
                    }

                    // Try to queue the download in case there are multiple happening here at the same time...
                    var isSubPrefab = prefabDto.Id != id;
                    if (QueueDownload(id) || QueueDownload(prefabDto.Id))
                        return;

                    var assetPlatformDocumentDto = prefabDto.Platforms.GetDocumentForCurrentPlatform();
                    if (assetPlatformDocumentDto == null)
                    {
                        failed?.Invoke(
                            $"Prefab '{prefabDto.Name} ({prefabDto.Id.ToString()[..4]}...)' is not supported on this platform.");
                        return;
                    }

                    // Add both source and child IDs to downloading list.
                    DownloadingBundles.Add(id);
                    if (isSubPrefab) DownloadingBundles.Add(prefabDto.Id);

                    // Now that we've added these to the downloading
                    // list, we want to make sure if there's
                    // a failure, that we remove them.
                    var originalFailedEvent = failed;
                    failed = e =>
                    {
                        DownloadingBundles.Remove(id);
                        if (isSubPrefab) DownloadingBundles.Remove(prefabDto.Id);
                        originalFailedEvent?.Invoke(e);
                    };

                    var platform = MetaverseProgram.GetCurrentPlatform(false);
                    var downloadOptions = new AssetPlatformDownloadOptions(prefabDto.Id, platform)
                    {
                        AssetDto = prefabDto,
                    };

                    MetaverseProgram.ApiClient.Prefabs
                        .DownloadPlatformAsync(downloadOptions, progress != null ? new Progress<float>(progress) : null,
                            cancellationToken)
                        .ResponseThen(
                            response =>
                            {
                                if (response.PlatformData is null)
                                {
                                    failed?.Invoke("Failed to read platform data.");
                                    return;
                                }

                                if (CheckStale(scene, cancellationToken, gameStateLoadRequestId, failed))
                                {
                                    response.PlatformData?.UnloadAll();
                                    return;
                                }

                                var documentForCurrentPlatform =
                                    response.AssetMetaData?.Platforms?.GetDocumentForCurrentPlatform();
                                if (documentForCurrentPlatform is null)
                                {
                                    failed?.Invoke("No platforms");
                                    response.PlatformData?.UnloadAll();
                                    return;
                                }

                                response.PlatformData.LoadAsset<GameObject>(
                                    documentForCurrentPlatform.MainAsset,
                                    gameObject => OnDownloadSuccess(scene, gameObject, prefabDto, response.PlatformData,
                                        id, loaded, failed, cancellationToken, gameStateLoadRequestId),
                                    failed,
                                    cancellationToken: cancellationToken);
                            },
                            failed,
                            cancellationToken);
                }
            });
            return;

            // This function queues the download if we are already downloading the same bundle. 
            // This is to prevent the same bundle from being downloaded multiple times.
            bool QueueDownload(Guid sourceId)
            {
                if (!DownloadingBundles.Contains(sourceId))
                    return false; // We are not downloading the bundle, so we can continue.

                // We are already downloading the bundle, so we need to wait until it is finished.
                RateLimitAndRetry(sourceId, id);

                // We are waiting for the download to finish, so we can return true.
                return true;
            }

            bool QueueRequest(Guid sourceId, Guid targetId, ref Action<GameObject> loadedEvent,
                ref Action<object> failedEvent)
            {
                if (QueuedRequests.Add(sourceId))
                {
                    var originalLoadedEvent = loadedEvent;
                    var originalFailedEvent = failedEvent;
                    loadedEvent = g =>
                    {
                        QueuedRequests.Remove(sourceId);
                        QueuedRequests.Remove(targetId);
                        originalLoadedEvent?.Invoke(g);
                    };
                    failedEvent = e =>
                    {
                        QueuedRequests.Remove(sourceId);
                        QueuedRequests.Remove(targetId);
                        originalFailedEvent?.Invoke(e);
                    };
                    return false;
                }

                // We are already loading this prefab, so we need to wait until it is finished.
                RateLimitAndRetry(sourceId, targetId);
                return true;
            }

            void RateLimitAndRetry(Guid sourceId, Guid targetId)
            {
                if (!Application.isPlaying)
                {
                    MetaverseDispatcher.WaitUntil(() =>
                            !DownloadingBundles.Contains(sourceId) &&
                            !QueuedRequests.Contains(sourceId),
                        () =>
                        {
                            LoadPrefab(
                                targetId,
                                scene,
                                loaded,
                                cachedDto,
                                progress,
                                failed,
                                cancellationToken,
                                gameStateLoadRequestId);
                        });
                    return;
                }

                MetaverseDispatcher.WaitForSeconds(1f, () =>
                {
                    if (!DownloadingBundles.Contains(sourceId) &&
                        !QueuedRequests.Contains(sourceId))
                    {
                        if (sourceId != targetId)
                            // Bugfix 11/4/23
                            // We have to remove target ID here because it's possible
                            // that the target ID was queued before the source ID.
                            QueuedRequests.Remove(targetId);

                        LoadPrefab(
                            targetId,
                            scene,
                            loaded,
                            cachedDto,
                            progress,
                            failed,
                            cancellationToken,
                            gameStateLoadRequestId);
                        return;
                    }

                    RateLimitAndRetry(sourceId, targetId);
                });
            }
        }

        /// <summary>
        /// Attempts to de-allocate the object pool if there are no instances of the 
        /// prefab left and if the pools lifetime has run out. This usually happens
        /// automatically.
        /// </summary>
        /// <param name="id">The ID of the meta prefab to de-allocate.</param>
        /// <returns></returns>
        public static bool TryDeAllocateObjectPool(Guid id)
        {
            if (ObjectPoolInstanceCount.ContainsKey(id) || !_objectPool.TryGetValue(id, out var p) || !p ||
                !p.scene.isLoaded)
                return false;

            ForceDeAllocateObjectPool(id);
            p.ManuallyDeAllocateReferencedAssets();
            UnityEngine.Object.Destroy(p);

            return true;
        }

        /// <summary>
        /// Rather than check the pool lifetime and the instance count, forcefully deletes the object pool from memory.
        /// </summary>
        /// <param name="id">The ID of the meta prefab to de-allocate.</param>
        public static void ForceDeAllocateObjectPool(Guid id)
        {
            _objectPool.Remove(id);
            _childIdsToSourcePrefabIds = _childIdsToSourcePrefabIds.Where(x => _objectPool.ContainsKey(x.Value))
                .ToDictionary(x => x.Key, y => y.Value);
        }

        /// <summary>
        /// Clears the object pool. This is usually called automatically.
        /// </summary>
        /// <param name="setToNull"></param>
        public static void ClearPool(bool setToNull)
        {
            if (_objectPoolContainers != null)
            {
                foreach (var (_, value) in _objectPoolContainers)
                    if (value)
                        UnityEngine.Object.DestroyImmediate(value);
                _objectPoolContainers = setToNull ? null : new Dictionary<Scene, GameObject>();
            }

            if (_objectPool != null)
            {
                foreach (var (_, value) in _objectPool)
                    if (value)
                        UnityEngine.Object.DestroyImmediate(value);
                _objectPool = setToNull ? null : new Dictionary<Guid, GameObject>();
            }

            ObjectPoolInstanceCount.Clear();
        }

        // TODO: This is a bit of a hack, but it's the best way I can think of to
        // prevent a malicious prefab from being spawned. We'll need to figure out
        // a better way to do this in the future.
        /// <summary>
        /// Checks if the prefab is secure and can be spawned. If it's not secure, the onFailed callback will be invoked.
        /// "Secure" means that the prefab is not unlisted and is not in a private space. For example someone could
        /// create a prefab that hasn't been verified that contains malicious code. This function is designed to prevent
        /// that from happening.
        /// </summary>
        /// <param name="prefab">The data transfer object of the prefab to check.</param>
        /// <param name="onSecure">Invoked if the prefab is secure.</param>
        /// <param name="onFailed">Invoked if the prefab is not secure.</param>
        private static void CheckSecure(AssetDto prefab, Action onSecure, Action<object> onFailed)
        {
            if (true)
            {
                // FIXME: At some point we'll need to flip this switch. But it may
                // affect a lot of already established assets.
                onSecure?.Invoke();
                return;
            }

            // This function is designed to prevent someone from remotely spawning
            // a malicious/unverified meta prefab that may contain
            // malicious content.
#pragma warning disable CS0162
            // ReSharper disable HeuristicUnreachableCode
            var space = MetaSpace.CurrentlyLoadedMetaSpaceDto;
            if (space == null)
            {
                // We don't really care about security in a non-meta space related
                // context.
                onSecure?.Invoke();
                return;
            }

            if (prefab.Listings != Common.Enumerations.AssetListings.Unlisted ||
                prefab.AssetContentType == Common.Enumerations.AssetContentType.Gltf)
            {
                // A GLTF is harmless, and if this has already been verified
                // then we're good.
                onSecure?.Invoke();
                return;
            }

            if (prefab.HasWriteAccess == true || space.HasWriteAccess == true)
            {
                // If we have write access to this 
                // then we don't care.
                onSecure?.Invoke();
                return;
            }

            if (space.RepresentativeName == prefab.RepresentativeName)
            {
                // If they have the same representative, it's fine.
                onSecure?.Invoke();
                return;
            }

            MetaverseProgram.ApiClient.MetaSpaces.GetContributorsAsync(new AssetContributorQueryParams
            {
                Id = space.Id,
                NameFilter = prefab.RepresentativeName,
                Count = 1,
                QueryInvites = false,
            }).ResponseThen(r =>
            {
                if (r.Any())
                {
                    // The representative is a contributor on the current meta space
                    // this means they have authority to spawn unlisted content.
                    onSecure?.Invoke();
                    return;
                }

                const string unsecureContentBeingSpawnedWarning =
                    "This prefab is not verified and could potentially contain malicious content.";
                onFailed?.Invoke(unsecureContentBeingSpawnedWarning);
            }, onFailed);

            // ReSharper restore HeuristicUnreachableCode
#pragma warning restore CS0162
        }

        private static bool DePoolChildPrefab(Guid targetId, Guid sourceId, Action<GameObject> loaded,
            Action<object> failed)
        {
            if (!IsPooled(sourceId)) return false;
            if (!DePool(targetId, loaded))
                failed?.Invoke("Source of prefab is pooled but the sub-prefab was not found.");
            return true;
        }

        // TODO: This is a bit of a hack. We need to find a better way to do this.
        // The problem is that we need to know if the game state has changed since
        // the load was requested. If it has, then we need to cancel the load.
        /// <summary>
        /// Determines if the scene has been unloaded or if the game state has changed since the load was requested.
        /// </summary>
        /// <param name="scene">The scene that originally requested the load.</param>
        /// <param name="cancellationToken">The cancellation token that was used to request the load.</param>
        /// <param name="loadRequest">The ID of the load request.</param>
        /// <param name="failed">Invoked if the load has been cancelled or the scene has been unloaded.</param>
        /// <returns>A value indicating if the load has been cancelled or the scene has been unloaded.</returns>
        private static bool CheckStale(Scene scene, CancellationToken cancellationToken, Guid? loadRequest,
            Action<object> failed)
        {
            const string operationWasCancelledError = "Operation was cancelled.";
            const string sceneHasBeenUnloadedError = "Scene has been unloaded.";

            if (cancellationToken.IsCancellationRequested)
            {
                failed?.Invoke(operationWasCancelledError);
                return true;
            }

            if (!scene.isLoaded || !scene.IsValid())
            {
                failed?.Invoke(sceneHasBeenUnloadedError);
                return true;
            }

#if UNITY_EDITOR
            const string gameStateHasChangedSinceLoadWasRequestedError =
                "Game state has changed since load was requested.";
            if (loadRequest != null && LoadRequestGameStateCache.TryGetValue(loadRequest.Value, out var wasPlaying) &&
                wasPlaying != Application.isPlaying)
            {
                failed?.Invoke(gameStateHasChangedSinceLoadWasRequestedError);
                return true;
            }
#endif

            return false;
        }

        private static void OnDownloadSuccess(
            Scene scene,
            GameObject downloadedObject,
            PrefabDto sourcePrefabDto,
            IAssetPlatformBundle sourcePrefabBundle,
            Guid targetPrefabId,
            Action<GameObject> loadedCallback,
            Action<object> failedCallback,
            CancellationToken cancellationToken,
            Guid? loadRequestId)
        {
            if (CheckStale(scene, cancellationToken, loadRequestId, failedCallback))
            {
                sourcePrefabBundle.UnloadAll();
                return;
            }

            if (!downloadedObject)
            {
                failedCallback?.Invoke("The prefab object returned null.");
                sourcePrefabBundle.UnloadAll();
                return;
            }

            EnsurePoolContainer(scene);

            GameObject outputObject;
            var pooledPrefab = outputObject = UnityEngine.Object.Instantiate(downloadedObject, _objectPoolContainers[scene].transform);

            ConfigureDownloadedObjectForCurrentRenderPipeline(
                pooledPrefab,
                hasChildren: sourcePrefabDto.PrefabChildren.Count > 0);

            _objectPool[sourcePrefabDto.Id] = pooledPrefab;
            _childIdsToSourcePrefabIds = _childIdsToSourcePrefabIds
                .Where(x => x.Value != sourcePrefabDto.Id)
                .ToDictionary(x => x.Key, y => y.Value);

            if (sourcePrefabDto.PrefabChildren.Count > 0)
            {
                var childPrefabs = pooledPrefab
                    .GetComponentsInChildren<MetaPrefab>(true)
                    .Where(x => x.ID is not null)
                    .GroupBy(x => x.ID.Value)
                    .ToDictionary(x => x.Key, y => y.FirstOrDefault());

                foreach (var childPrefabDto in sourcePrefabDto.PrefabChildren)
                {
                    if (!childPrefabs.TryGetValue(childPrefabDto.Id, out var childMetaPrefab) || !childMetaPrefab)
                        continue;
                    _objectPool[childPrefabDto.Id] = childMetaPrefab.gameObject;
                    _childIdsToSourcePrefabIds[childPrefabDto.Id] = sourcePrefabDto.Id;
                    if (targetPrefabId == childMetaPrefab.ID)
                        outputObject = childMetaPrefab.gameObject;
                }
            }

            try
            {
                if (MetaSpace.Instance &&
                    sourcePrefabBundle is UnityAssetPlatformBundle unityAssetBundle &&
                    unityAssetBundle.Bundle &&
                    !unityAssetBundle.Bundle.isStreamedSceneAssetBundle)
                {
                    try
                    {
                        var spawnablePrefabs = Resources.FindObjectsOfTypeAll<SpawnablePrefab>();
                        foreach (var spawnable in spawnablePrefabs)
                            if (spawnable.ID != null)
                                MetaSpace.Instance.NetworkOptions.RegisterSpawnable(spawnable);
                    }
                    catch (Exception e)
                    {
                        MetaverseProgram.Logger.LogError(e);
                    }
                }
            }
            finally
            {
                OnFinishedLoading(outputObject);
            }

            return;

            void OnFinishedLoading(GameObject gameObject)
            {
                sourcePrefabBundle.UnloadAll(() =>
                {
                    DownloadingBundles.Remove(targetPrefabId);
                    DownloadingBundles.Remove(sourcePrefabDto.Id);
                    
                    QueuedRequests.Remove(targetPrefabId);
                    QueuedRequests.Remove(sourcePrefabDto.Id);
                    
                    foreach (var child in sourcePrefabDto.PrefabChildren)
                    {
                        DownloadingBundles.Remove(child.Id);
                        QueuedRequests.Remove(child.Id);
                    }

                    if (loadRequestId is not null)
                        LoadRequestGameStateCache.Remove(loadRequestId.Value);

                    if (CheckStale(scene, cancellationToken, loadRequestId, failedCallback))
                        return;

                    if (gameObject)
                    {
                        if (!gameObject.TryGetComponent(out MetaPrefab mpf))
                            mpf = gameObject.AddComponent<MetaPrefab>();

                        if (targetPrefabId == sourcePrefabDto.Id)
                            mpf.UpdateFromDto(sourcePrefabDto);
                    }

                    loadedCallback?.Invoke(gameObject);
                });
            }
        }

        private static void ConfigureDownloadedObjectForCurrentRenderPipeline(GameObject downloadedObject,
            bool hasChildren)
        {
            if (!downloadedObject.TryGetComponent(out MetaPrefab metaPrefab))
                return;

            switch (metaPrefab.UsesScriptableRenderPipeline)
            {
                case true when !GraphicsSettings.defaultRenderPipeline:
                case false when GraphicsSettings.defaultRenderPipeline:
                    if (metaPrefab.CheckRenderPipeline(hasChildren))
                        MetaverseProgram.Logger.Log(
                            $"Successfully upgraded {downloadedObject.name} to the current render pipeline.");
                    break;
            }
        }

        private static bool IsPooled(Guid id)
        {
            if (!_objectPool.TryGetValue(id, out var objectInPool))
                return false;

            if (objectInPool)
                return true;

            ForceDeAllocateObjectPool(id);
            return false;
        }

        private static bool DePool(Guid id, Action<GameObject> loaded)
        {
            try
            {
                if (!_objectPool.TryGetValue(id, out var pooledObject))
                    return false;

                if (pooledObject == null)
                {
                    ForceDeAllocateObjectPool(id);
                    return false;
                }

                loaded?.Invoke(pooledObject);
                return true;
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
                ForceDeAllocateObjectPool(id);
                return false;
            }
        }

        private static void EnsurePoolContainer(Scene scene)
        {
            if (_objectPoolContainers.TryGetValue(scene, out var poolContainer))
                return;

            CleanupPool();

            poolContainer = new GameObject($"{scene.name} Meta Prefabs");
            poolContainer.SetActive(false);
            poolContainer.AddComponent<MetaPrefabPoolContainer>();
            poolContainer.hideFlags = Application.isPlaying
                ? HideFlags.HideInHierarchy
                : HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            _objectPoolContainers[scene] = poolContainer;

            if (scene.name == nameof(UnityEngine.Object.DontDestroyOnLoad))
                UnityEngine.Object.DontDestroyOnLoad(poolContainer);
        }

        private static void CleanupPool()
        {
            _objectPoolContainers =
                _objectPoolContainers.Where(x => x.Key.IsValid()).ToDictionary(x => x.Key, y => y.Value);
            _objectPool = _objectPool.Where(x => x.Value).ToDictionary(x => x.Key, y => y.Value);
            _childIdsToSourcePrefabIds = _childIdsToSourcePrefabIds.Where(x => _objectPool.ContainsKey(x.Value))
                .ToDictionary(x => x.Key, y => y.Value);
        }

        internal static void RegisterPrefabInstance(Guid id)
        {
            if (_childIdsToSourcePrefabIds.TryGetValue(id, out var sourceId))
                id = sourceId;
            if (_objectPool.TryGetValue(id, out var pool) && pool && pool.scene == MVUtils.GetDontDestroyOnLoadScene())
                return;
            if (!ObjectPoolInstanceCount.TryGetValue(id, out var count))
                ObjectPoolInstanceCount[id] = 0;
            ObjectPoolInstanceCount[id] = count + 1; // Increment pool instance count.
            PrefabsToExpire.Remove(id);
        }

        internal static void UnRegisterPrefabInstance(Guid id)
        {
            if (MetaverseProgram.IsQuitting) return;
            if (_childIdsToSourcePrefabIds.TryGetValue(id, out var sourceId))
                id = sourceId;

            if (!ObjectPoolInstanceCount.TryGetValue(id, out var count)) return;
            ObjectPoolInstanceCount[id] = count -= 1; // Decrement pool instance count
            if (count != 0)
                return;

            ObjectPoolInstanceCount.Remove(id);
            if (!_objectPool.TryGetValue(id, out var pool) || !pool || !pool.scene.isLoaded)
                return;

            if (PrefabsToExpire.Add(id))
            {
                UniTask.Void(async cancellationToken =>
                {
                    const float cacheReleaseDelay = 5;
                    var endTime = DateTime.UtcNow.AddSeconds(cacheReleaseDelay);
                    await UniTask.WaitUntil(() => DateTime.UtcNow >= endTime || !PrefabsToExpire.Contains(id),
                        cancellationToken: cancellationToken);
                    if (PrefabsToExpire.Remove(id))
                        TryDeAllocateObjectPool(id);
                }, _playModeCancellationToken.Token);
            }
        }
    }
}