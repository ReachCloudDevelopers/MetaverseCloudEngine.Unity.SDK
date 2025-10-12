using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Abstract;
using MetaverseCloudEngine.ApiClient.Extensions;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.Networking;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class MetaverseWebClient : MonoBehaviour, IHttpClient, IAssetPlatformDownloader, IImageDownloader,
        ITaskHandler
    {
        private const string ApplicationQuittingErrorMessage = "Application Quitting";
        private const int CacheCheckInterval = 500;

#if UNITY_EDITOR
        private static readonly HttpClient EditorHttpClient = new()
        {
            Timeout = TimeSpan.FromHours(10),
        };
#endif

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
        }

        async Task<HttpResponseMessage> IHttpClient.SendAsync(
            HttpRequestMessage request,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (MetaverseProgram.IsQuitting && !Application.isEditor)
            {
                var response = new Exception(ApplicationQuittingErrorMessage).ToHttpResponseMessage();
                if (response.Content != null)
                    await response.Content.LoadIntoBufferAsync();
                return response;
            }

#if UNITY_EDITOR
            if (progress == null && !MetaverseDispatcher.UseUniTaskThreading)
            {
                var response = await EditorHttpClient.SendAsync(request, cancellationToken);
                if (response.Content != null)
                    await response.Content.LoadIntoBufferAsync();
                return response;
            }
#endif

            try
            {
                var httpResponseMessage = UniTask.Create(async () =>
                {
                    await UniTask.SwitchToMainThread(cancellationToken);

                    var uploadHandler = request.Content != null
                        ? new UploadHandlerRaw(await request.Content.ReadAsByteArrayAsync())
                        : null;

                    using var webRequest = request.ToUnityWebRequest(
                        uploadHandler: uploadHandler,
                        downloadHandler: new DownloadHandlerBuffer());

                    try
                    {
                        using var @lock =
                            await UnityWebRequestRateLimiter.EnterAsync(cancellationToken: cancellationToken);

                        var uwr = webRequest.SendWebRequest();
                        var task = uwr.ToUniTask(cancellationToken: cancellationToken);

                        if (progress == null)
                            await task;
                        else
                        {
                            while (task.Status == UniTaskStatus.Pending)
                            {
                                var p = uwr.progress;
                                progress.Report(p);
                                await UniTask.Yield();
                            }
                        }
                    }
                    catch (UnityWebRequestException e)
                    {
                        if (Application.isEditor && e.ResponseCode is >= 500 or 401)
                        {
                            var error = e.Error;
                            if (e.Text != null) error += $"\n{e.Text}";
                            MetaverseProgram.Logger.LogError($"{request.RequestUri} ({e.ResponseCode}): {error}");
                        }
                    }
                    catch (Exception e)
                    {
                        if (Application.isEditor)
                            MetaverseProgram.Logger.LogError(e);
                        var responseMessage = e.ToHttpResponseMessage();
                        if (responseMessage.Content != null)
                            await responseMessage.Content.LoadIntoBufferAsync();
                        return responseMessage;
                    }

                    {
                        var responseMessage = webRequest.ToHttpResponseMessage();
                        if (webRequest.downloadHandler is DownloadHandlerBuffer b &&
                            webRequest.downloadHandler.data != null)
                        {
                            responseMessage.Content = new StreamContent(new MemoryStream(b.data));
                            await responseMessage.Content.LoadIntoBufferAsync();
                        }
                        else if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            responseMessage.Content = new StringContent("Please log in to perform this action.");
                            await responseMessage.Content.LoadIntoBufferAsync();
                        }
                        else if ((int)responseMessage.StatusCode == 0)
                        {
                            // HTTP status 0 indicates network connectivity issues
                            MetaverseProgram.Logger.LogWarning($"HTTP status 0 detected (network connectivity issue) for {request.RequestUri}. This will be handled by the retry mechanism.");
                            responseMessage.Content = new StringContent("Network connectivity issue detected.");
                            await responseMessage.Content.LoadIntoBufferAsync();
                        }

                        return responseMessage;
                    }
                }).AsTask();
                return await httpResponseMessage;
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
                return e.ToHttpResponseMessage();
            }
        }

        async Task<ApiResponse<IImage>> IImageDownloader.DownloadImageAsync(
            HttpRequestMessage request,
            long? version,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (MetaverseProgram.IsQuitting && !Application.isEditor)
                return await new Exception(ApplicationQuittingErrorMessage)
                    .ToHttpResponseMessage()
                    .ToApiResponseAsync<IImage>();

            try
            {
                return await UniTask.Create(async () =>
                {
                    await UniTask.SwitchToMainThread(cancellationToken);

#if UNITY_EDITOR || !UNITY_WEBGL
                    while (!Caching.ready)
                        await UniTask.Delay(CacheCheckInterval, cancellationToken: cancellationToken);
#endif

                    using var @lock = await UnityWebRequestRateLimiter.EnterAsync(cancellationToken: cancellationToken);
                    using var uwr = CacheableUnityWebRequest.Get(request.RequestUri,
                        downloadHandler: new DownloadHandlerTexture(true));
                    uwr.Request.WithHttpRequestMessageData(request);

                    try
                    {
                        await uwr.SendWebRequestAsync(cancellationToken: cancellationToken, progress: progress);
                    }
                    catch (UnityWebRequestException e)
                    {
                        return await e.UnityWebRequest.ToHttpResponseMessage().ToApiResponseAsync<IImage>();
                    }
                    catch (Exception e)
                    {
                        if (uwr.Request.downloadHandler != null &&
                            !string.IsNullOrEmpty(uwr.Request.downloadHandler.error))
                            MetaverseProgram.Logger.LogError(uwr.Request.downloadHandler.error);
                        else MetaverseProgram.Logger.LogError(e);
                        return await e.ToHttpResponseMessage().ToApiResponseAsync<IImage>();
                    }

                    if (uwr.Success)
                    {
                        var t2d = uwr.GetTexture();
                        var output = new UnityImage(t2d);
                        return new ApiResponse<IImage>(output);
                    }

                    return await uwr.Request.ToHttpResponseMessage().ToApiResponseAsync<IImage>();
                }).AsTask();
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
                return await e.ToHttpResponseMessage().ToApiResponseAsync<IImage>();
            }
        }

        async Task<ApiResponse<IAssetPlatformBundle>> IAssetPlatformDownloader.DownloadAssetBundleAsync(
            HttpRequestMessage request,
            AssetDto asset,
            DocumentDto document,
            long? version,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (MetaverseProgram.IsQuitting && !Application.isEditor)
                return await new Exception(ApplicationQuittingErrorMessage)
                    .ToHttpResponseMessage()
                    .ToApiResponseAsync<IAssetPlatformBundle>();

            try
            {
                return await BeginAssetBundleDownloadTask(
                    request, asset, document, version, progress, cancellationToken);
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                {
                    return await new Exception("Asset bundle download operation cancelled. Check application logs for details.").ToHttpResponseMessage()
                        .ToApiResponseAsync<IAssetPlatformBundle>();
                }
                
                MetaverseProgram.Logger.LogError(e);
                return await e.ToHttpResponseMessage().ToApiResponseAsync<IAssetPlatformBundle>();
            }
        }

        public Task Delay(int milliseconds, CancellationToken cancellationToken = default)
        {
            return MetaverseDispatcher.UseUniTaskThreading
                ? UniTask.Delay(milliseconds, cancellationToken: cancellationToken).AsTask()
                : Task.Delay(milliseconds, cancellationToken);
        }

        public Task<bool> SemaphoreWaitAsync(SemaphoreSlim semaphore, int? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return MVUtils.AwaitSemaphore(semaphore, timeout, cancellationToken);
        }

        public static MetaverseWebClient CreateNew()
        {
            var go = new GameObject(nameof(MetaverseWebClient))
            {
                hideFlags = Application.isPlaying
                    ? (HideFlags.HideInHierarchy | HideFlags.NotEditable)
                    : HideFlags.HideAndDontSave
            };
            return go.AddComponent<MetaverseWebClient>();
        }

        private static Task<ApiResponse<IAssetPlatformBundle>> BeginAssetBundleDownloadTask(HttpRequestMessage request,
            AssetDto asset, DocumentDto document, long? version, IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var task = UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread(cancellationToken);

                if (MVUtils.IsOutOfMemory((long)(document?.Size ?? 0)))
                    throw new OutOfMemoryException("Not enough memory to load asset bundle.");

#if UNITY_EDITOR || !UNITY_WEBGL
                while (!Caching.ready)
                    await UniTask.Delay(CacheCheckInterval, cancellationToken: cancellationToken);
#endif

                if (asset.AssetContentType == AssetContentType.Gltf)
                {
                    var response =
                        new ApiResponse<IAssetPlatformBundle>(new UnityAssetPlatformGltf(request.RequestUri));
                    return response;
                }

                var unityWebRequest = UnityWebRequestAssetBundle
                    .GetAssetBundle(request.RequestUri,
                        Hash128.Compute(version.HasValue ? version.Value.ToString() : request.RequestUri.AbsolutePath))
                    .WithHttpRequestMessageData(request);

                var isFinished = false;
                var succeeded = false;
                
                // Yes. I have to be incredibly redundant and use a try-catch blocks and multiple 
                // unload calls because UnityWebRequestAssetBundle is incredibly un-reliable and inconsistent.
                try
                {
                    try
                    {
                        var send = unityWebRequest.SendWebRequest();
                        send.completed += _ =>
                        {
                            UniTask.Void(async () =>
                            {
                                await UniTask.SwitchToMainThread();
                                await UniTask.WaitUntil(() => isFinished);
                                AssetBundle bundle = null;
                                try
                                {
                                    if (!succeeded)
                                    {
                                        bundle = DownloadHandlerAssetBundle.GetContent(unityWebRequest);
                                        if (bundle && AssetBundle.GetAllLoadedAssetBundles().Contains(bundle))
                                            bundle.Unload(true);
                                    }
                                }
                                catch (Exception)
                                {
                                    if (bundle && AssetBundle.GetAllLoadedAssetBundles().Contains(bundle))
                                        bundle.Unload(true);
                                }
                            });
                        };
                        await send.ToUniTask(cancellationToken: cancellationToken, progress: progress);
                    }
                    catch (UnityWebRequestException e)
                    {
                        return await e.UnityWebRequest.ToHttpResponseMessage()
                            .ToApiResponseAsync<IAssetPlatformBundle>();
                    }
                    catch (Exception e)
                    {
                        if (unityWebRequest.downloadHandler is not null &&
                            !string.IsNullOrEmpty(unityWebRequest.downloadHandler.error))
                        {
                            MetaverseProgram.Logger.LogError(unityWebRequest.downloadHandler.error);
                        }
                        else
                        {
                            MetaverseProgram.Logger.LogError(e);
                        }

                        isFinished = true;
                        return await e.ToHttpResponseMessage().ToApiResponseAsync<IAssetPlatformBundle>();
                    }

                    AssetBundle bundle = null;
                    try
                    {
                        switch (unityWebRequest.result)
                        {
                            case UnityWebRequest.Result.Success:
                            {
                                bundle = DownloadHandlerAssetBundle.GetContent(unityWebRequest) 
                                    ?? AssetBundle.GetAllLoadedAssetBundles()
                                        .FirstOrDefault(b => 
                                            b.name.Contains(unityWebRequest.url) || 
                                            b.name.Contains(request.RequestUri.AbsolutePath))
                                    ?? throw new Exception(
                                        $"Asset bundle failed to load: Checked for bundles: {string.Join(", ", AssetBundle.GetAllLoadedAssetBundles().Select(b => b.name))}");

                                bundle.name = unityWebRequest.url;
                                var output = new UnityAssetPlatformBundle(bundle);
                                succeeded = true;
                                return new ApiResponse<IAssetPlatformBundle>(output);
                            }
                            case UnityWebRequest.Result.DataProcessingError:
                            {
                                bundle = AssetBundle.GetAllLoadedAssetBundles()
                                    .FirstOrDefault(b => 
                                        b.name.Contains(unityWebRequest.url) || 
                                        b.name.Contains(request.RequestUri.AbsolutePath));
                                if (!bundle)
                                {
                                    throw new Exception(
                                        $"DataProcessingError: Asset bundle failed to load. Checked for bundles: {string.Join(", ", AssetBundle.GetAllLoadedAssetBundles().Select(b => b.name))}");
                                }
                                
                                var output = new UnityAssetPlatformBundle(bundle);
                                succeeded = true;
                                return new ApiResponse<IAssetPlatformBundle>(output);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (bundle && AssetBundle.GetAllLoadedAssetBundles().Contains(bundle)) 
                            bundle.Unload(true);
                        else
                        {
                            var potentialBundle = AssetBundle.GetAllLoadedAssetBundles()
                                .FirstOrDefault(x => 
                                    x.name == request.RequestUri.AbsolutePath ||
                                    x.name == unityWebRequest.url);
                            if (potentialBundle)
                                potentialBundle.Unload(true);
                        }
                        return await e.ToHttpResponseMessage().ToApiResponseAsync<IAssetPlatformBundle>();
                    }

                    return await unityWebRequest.ToHttpResponseMessage().ToApiResponseAsync<IAssetPlatformBundle>();
                }
                finally
                {
                    isFinished = true;
                }
            }).AsTask();

            return task;
        }
    }
}