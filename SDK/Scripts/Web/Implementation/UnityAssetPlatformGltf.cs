using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cysharp.Threading.Tasks;

using MetaverseCloudEngine.ApiClient.Abstract;
using MetaverseCloudEngine.Unity.Assets.LandPlots;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Components;
using Siccity.GLTFUtility;

using UnityEngine;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class UnityAssetPlatformGltf : IAssetPlatformBundle
    {
        private const uint GlbMagic = 1179937895u;
        private readonly Uri _uri;
        private readonly byte[] _buffer;
        private GameObject _container;
        private bool _unloadOnDestroy;
        
        private static ImportSettings _defaultImportSettings;
        private static ImportSettings _rawImportSettings;

        public UnityAssetPlatformGltf(Uri uri, bool unloadOnDestroy = false)
        {
            _uri = uri;
            _unloadOnDestroy = unloadOnDestroy;
        }

        public UnityAssetPlatformGltf(byte[] buffer, bool unloadOnDestroy = false)
        {
            _buffer = buffer;
            _unloadOnDestroy = unloadOnDestroy;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplicationInit()
        {
            // Init import settings
            // Note: Every time the active render pipeline asset changes, the
            // import settings need to be regenerated because the shader
            // materials are cached in the constructor call.
            InitImportSettings();
            RenderPipelineManager.activeRenderPipelineTypeChanged += InitImportSettings;
            RenderPipelineManager.activeRenderPipelineAssetChanged += (_, _) => InitImportSettings();
        }

        private static void InitImportSettings()
        {
            _defaultImportSettings = new ImportSettings
            {
                animationSettings = new AnimationSettings
                {
                    useLegacyClips = true,
                    looping = true,
                }
            };

            _rawImportSettings = new ImportSettings();
        }

        /// <summary>
        /// If true, won't add any extra components or perform any extra
        /// operations to the object upon import.
        /// </summary>
        public bool RawImport { get; set; }

        /// <summary>
        /// If true, will set the <see cref="CacheableUnityWebRequest.IgnoreModifications"/> flag to true.
        /// </summary>
        public bool IgnoreCacheModifications { get; set; }
        
        public void LoadAsset<T>(string name, Action<T> loaded, Action<object> error, CancellationToken cancellationToken = default)
        {
            LoadAssetAsync<T>(name, cancellationToken).Then(loaded, error, cancellationToken: cancellationToken);
        }

        public Task<T> LoadAssetAsync<T>(string name, CancellationToken cancellationToken = default)
        {
            return UniTask.Create(async () =>
            {
                if (!Application.isPlaying)
                    throw new InvalidOperationException("GLTF can only load during runtime.");

                if (!typeof(GameObject).IsAssignableFrom(typeof(T)))
                    throw new InvalidCastException("Specified type is not valid.");

                byte[] dataToLoad;
                
                if (_buffer == null)
                {
                    using var uwrLock = await UnityWebRequestRateLimiter.EnterAsync(cancellationToken: cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        throw new TaskCanceledException();

                    var req = CacheableUnityWebRequest.Get(_uri);
                    req.IgnoreModifications = IgnoreCacheModifications;
                    await req.SendWebRequestAsync(cancellationToken: cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        throw new TaskCanceledException();

                    if (!req.Success)
                        throw new Exception("Failed to download.");

                    dataToLoad = req.Data;
                }
                else
                {
                    dataToLoad = _buffer;
                }

                if (IsGltfBinary(dataToLoad))
                {
                    var done = false;
                    var error = false;
                    GameObject glb;

                    void OnLoaded(GameObject go, AnimationClip[] clips)
                    {
                        done = true;
                        if (!go)
                        {
                            error = true;
                            return;
                        }

                        _container = new GameObject(_uri.AbsolutePath);
#if !METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
                        _container.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable | HideFlags.HideInInspector;
#endif
                        if (_unloadOnDestroy)
                            _container.OnDestroy(cb => CleanUpRuntimeResources(cb.gameObject, clips));

                        if (!RawImport)
                            AddAnimationClips(go, clips);
                        
                        FixSkinnedMeshBounds();

                        glb = go;
                        glb.transform.SetParent(_container.transform);

                        if (!RawImport)
                        {
                            AddSeats();
                            AddLandPlotProperties(clips);
                        }
                    }

                    try
                    {
                        if (Application.platform == RuntimePlatform.WebGLPlayer)
                        {
                            var go = Importer.LoadFromBytes(dataToLoad,
                                RawImport ? _rawImportSettings : _defaultImportSettings, out var animClips);
                            OnLoaded(go, animClips);
                        }
                        else
                        {
                            Importer.ImportGLBAsync(dataToLoad, RawImport ? _rawImportSettings : _defaultImportSettings, OnLoaded);
                        }
                    }
                    catch (Exception e)
                    {
                        done = true;
                        error = true;
                        MetaverseProgram.Logger.LogError("Failed to import GLB: " + e.ToPrettyErrorString());
                    }

                    await UniTask.WaitUntil(() => done);

                    if (error || cancellationToken.IsCancellationRequested)
                    {
                        Dispose();
                        throw new Exception("Failed to load.");
                    }
                }
                else
                {
                    throw new Exception("Non-binary GLTF is not supported.");
                }

                return (T)(object)_container;

            }).AsTask();
        }

        private void FixSkinnedMeshBounds()
        {
            var skinnedMeshRenderers = _container.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                if (skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh) 
                    skinnedMeshRenderer.sharedMesh.RecalculateBounds();
        }

        private static void CleanUpRuntimeResources(GameObject go, IEnumerable<AnimationClip> clips)
        {
            go.ManuallyDeAllocateReferencedAssets(materials: false, animationClips: false);
            foreach (var clip in clips)
                if (clip) UnityEngine.Object.Destroy(clip);
        }

        public void UnloadAll(Action completed = null)
        {
            Dispose();
            completed?.Invoke();
        }

        public Task UnloadAllAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        private void Dispose()
        {
            if (_container)
                UnityEngine.Object.Destroy(_container);
        }

        public static bool IsGltfBinary(byte[] data)
        {
            if (data == null)
            {
                return false;
            }

            var num = BitConverter.ToUInt32(data, 0);
            return num == GlbMagic;
        }

        private static void AddAnimationClips(GameObject go, AnimationClip[] clips)
        {
            if (clips is not { Length: > 0 }) return;
            var anims = go.GetOrAddComponent<Animation>();
            anims.clip = clips[0];
            foreach (var c in clips)
                anims.AddClip(c, c.name);
        }

        private void AddLandPlotProperties(AnimationClip[] clips)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            _container.AddComponent<LandPlotObjectCollisionProperty>();
            if (clips != null && clips.Length > 0) _container.AddComponent<LandPlotObjectAnimationProperty>().WithClips(clips);
            _container.AddComponent<LandPlotObjectScaleProperty>();
#endif
        }

        private void AddSeats()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            if (MetaverseInternalResources.Instance.seatPrefab)
            {
                var seatTransforms = _container.GetComponentsInChildren<Transform>().Where(x => x.name.EndsWith("__Seat", StringComparison.OrdinalIgnoreCase));
                foreach (var seatTransform in seatTransforms)
                {
                    var seat = UnityEngine.Object.Instantiate(MetaverseInternalResources.Instance.seatPrefab, seatTransform);
                    seat.transform.localPosition = Vector3.zero;
                    seat.transform.localRotation = Quaternion.identity;
                    seat.transform.localScale = Vector3.one;
                }
            }
#endif
        }
    }
}