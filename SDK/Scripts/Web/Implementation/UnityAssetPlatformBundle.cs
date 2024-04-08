using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.ApiClient.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class UnityAssetPlatformBundle : IAssetPlatformBundle
    {
        private bool _unloaded;

        public UnityAssetPlatformBundle(AssetBundle bundle) => Bundle = bundle;

        public AssetBundle Bundle { get; private set; }

        public void LoadAsset<T>(string name, Action<T> loaded = null, Action<object> error = null, CancellationToken cancellationToken = default)
        {
            if (Bundle == null)
            {
                error?.Invoke("Bundle has been unloaded.");
                return;
            }

            try
            {
                if (Application.isPlaying)
                {
                    var loadOp = Bundle.LoadAssetAsync(name, typeof(T));
                    loadOp.completed += _ =>
                    {
                        if (loadOp.asset == null)
                        {
                            error?.Invoke("Asset failed to load.");
                            return;
                        }
                        
                        loaded?.Invoke((T)(object)loadOp.asset);
                    };
                    return;
                }

                var asset = Bundle.LoadAsset(name);
                if (asset != null)
                {
                    loaded?.Invoke((T)(object)asset);
                    return;
                }

                error?.Invoke("Asset failed to load.");
            }
            catch (Exception e)
            {
                error?.Invoke(e);
            }
        }

        public Task<T> LoadAssetAsync<T>(string name, CancellationToken cancellationToken = default)
        {
            if (Bundle == null)
                throw new Exception("Bundle has been unloaded.");

            var taskSource = new TaskCompletionSource<T>();

            try
            {
                var loadOp = Bundle.LoadAssetAsync(name, typeof(T));
                loadOp.completed += _ =>
                {
                    if (loadOp.asset == null)
                    {
                        taskSource.TrySetException(new Exception("Asset failed to load."));
                        return;
                    }
                    taskSource.TrySetResult((T)(object)loadOp.asset);
                };
            }
            catch (Exception e)
            {
                taskSource.TrySetException(e);
            }

            return taskSource.Task;
        }

        public void UnloadAll(Action completed = null)
        {
            if (_unloaded)
            {
                completed?.Invoke();
                return;
            }

            _unloaded = true;
            try
            {
                if (Bundle)
                {
                    var isScene = Bundle.isStreamedSceneAssetBundle;
                    Bundle.UnloadAsync(isScene).completed += _ =>
                    {
                        Bundle.SafeDestroy();
                        Bundle = null;
                        if (isScene)
                            Resources.UnloadUnusedAssets().completed += _ => completed?.Invoke();
                        else
                            completed?.Invoke();
                    };
                }
                else
                {
                    completed?.Invoke();
                    Bundle = null;
                }
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
                completed?.Invoke();
            }
        }

        public Task UnloadAllAsync()
        {
            return UniTask.Create(async () =>
            {
                if (_unloaded) return;
                _unloaded = true;
                try
                {
                    if (Bundle)
                    {
                        var isStreamedScene = Bundle.isStreamedSceneAssetBundle;
                        await Bundle.UnloadAsync(isStreamedScene).ToUniTask();
                        Bundle.SafeDestroy();
                        Bundle = null;
                        if (isStreamedScene)
                            await Resources.UnloadUnusedAssets().ToUniTask();
                    }
                    else
                    {
                        Bundle = null;
                    }
                }
                catch (Exception e)
                {
                    MetaverseProgram.Logger.LogError(e);
                }

            }).AsTask();
        }
    }
}