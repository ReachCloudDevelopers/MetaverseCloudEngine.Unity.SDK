using Cysharp.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MetaverseCloudEngine.Unity.Rendering;
using UnityEngine;
using UnityEngine.Networking;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class CacheableUnityWebRequest : IDisposable
    {
        private static string CacheFilePath =>
#if UNITY_WEBGL && !UNITY_EDITOR
            "";
#else
            Caching.currentCacheForWriting.path + "/Misc/";
#endif

        private const int MaximumConcurrentCacheRequests = 20;

        private static readonly ConcurrentDictionary<string, byte> CacheRequests = new();

        private bool _lockedCache;
        private bool _cacheSuccess;
        private string _cachedItemHash;
        private byte[] _buffer;
        private Texture2D _cachedTex;

        private CacheableUnityWebRequest(Uri uri, string method, DownloadHandler downloadHandler, UploadHandler uploadHandler)
        {
            Request = new UnityWebRequest(uri, method, downloadHandler ?? new DownloadHandlerBuffer(), uploadHandler);
        }

        public UnityWebRequest Request { get; }
        public string CustomCachePath { get; set; }

        public static IList<string> HeadersToCopyToHead { get; private set; } = new List<string>
        {
            "Authorization",
        };
        public bool Success => _cacheSuccess || Request.result == UnityWebRequest.Result.Success;
        public HttpStatusCode ResponseCode => _cacheSuccess ? HttpStatusCode.NotModified : (HttpStatusCode)Request.responseCode;
        public byte[] Data => _buffer ?? Request.downloadHandler.data;
        public bool IgnoreModifications { get; set; }

        public Texture2D GetTexture(bool readable = true)
        {
            if (_cachedTex is not null)
                return _cachedTex;

            if (Data is null || Data.Length is 0)
                return null;

            try
            {
                var tex = TextureCache.ReuseTexture2D();
                tex.name = Request.url;
                if (tex.LoadImage(Data, !readable))
                    _cachedTex = tex;
                else
                {
                    _cachedTex = null;
                    MetaverseProgram.Logger.LogError($"Failed to load image at {Request.url}");
                }

                return _cachedTex;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public Task SendWebRequestAsync(IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            return UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread(cancellationToken);

                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    await Request.SendWebRequest().ToUniTask(progress: progress, cancellationToken: cancellationToken);
                    return;
                }

                if (!Request.method.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase))
                {
                    await Request.SendWebRequest().ToUniTask(progress: progress, cancellationToken: cancellationToken);
                    return;
                }

                _cachedItemHash = ComputeHash(Request.uri.AbsoluteUri);

                await UniTask.SwitchToMainThread(cancellationToken);
                while (CacheRequests.Count >= MaximumConcurrentCacheRequests || CacheRequests.ContainsKey(_cachedItemHash))
                    await UniTask.Delay(100, cancellationToken: cancellationToken);

                if (!CacheRequests.TryAdd(_cachedItemHash, 0))
                    throw new Exception("Failed to add cache request to the queue.");

                _lockedCache = true;

                var allowCaching = true;
                try
                {
                    var cachedFile = new FileInfo(CustomCachePath ?? CacheFilePath + _cachedItemHash);
                    if (cachedFile.Exists && cachedFile.Length > 0)
                    {
                        bool readFromCache;
                        if (!IgnoreModifications)
                        {
                            using var headRequest = UnityWebRequest.Head(Request.uri);
                            headRequest.SetRequestHeader(
                                "If-Modified-Since",
                                // ASP.NET Core accepts datetime offset in RFC1123 format
                                cachedFile.LastWriteTimeUtc.ToString("R", CultureInfo.InvariantCulture));
                                

                            if (HeadersToCopyToHead.Count > 0)
                            {
                                foreach (var header in HeadersToCopyToHead)
                                {
                                    var value = Request.GetRequestHeader(header);
                                    if (!string.IsNullOrEmpty(value))
                                        headRequest.SetRequestHeader(header, value);
                                }
                            }

                            try
                            {
                                await headRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
                            }
                            catch (UnityWebRequestException)
                            {
                                allowCaching = false;
                            }

                            readFromCache = headRequest.responseCode == (int)HttpStatusCode.NotModified;
                        }
                        else
                        {
                            readFromCache = true;
                        }

                        if (readFromCache)
                        {
                            var supportFileCache = Application.platform != RuntimePlatform.WebGLPlayer;
                            if (supportFileCache && Request.downloadHandler is DownloadHandlerTexture)
                            {
                                var path = "file://" + cachedFile.FullName;
#pragma warning disable CS0618 // Type or member is obsolete
                                using var www = new WWW(path);
#pragma warning restore CS0618 // Type or member is obsolete
                                // ReSharper disable once AccessToDisposedClosure
                                await UniTask.WaitUntil(() => www.isDone, cancellationToken: cancellationToken);
                                if (www.error == null)
                                {
                                    if (Request.downloadHandler is DownloadHandlerTexture)
                                    {
                                        _cachedTex = TextureCache.ReuseTexture2D();
                                        _cacheSuccess = true;
                                        www.LoadImageIntoTexture(_cachedTex);   
                                    }
                                    
                                    progress?.Report(1);
                                    return;
                                }
                            }

                            if (Request.downloadHandler is not DownloadHandlerFile)
                            {
                                await using var read = cachedFile.OpenRead();
                                using var memoryStream = new MemoryStream();
                                await read.CopyToAsync(memoryStream, cancellationToken: cancellationToken);

                                _buffer = memoryStream.ToArray();
                            }
                            
                            _cacheSuccess = true;
                            progress?.Report(1);
                            return;
                        }

                        cachedFile.Delete();
                    }

                    if (_cacheSuccess || _cachedTex is not null)
                        return;

                    if (!Request.isDone)
                        await Request.SendWebRequest()
                            .ToUniTask(progress: progress, cancellationToken: cancellationToken);
                    else
                        await UniTask.WaitUntil(() => Request.isDone, cancellationToken: cancellationToken);

                    if (Request.result == UnityWebRequest.Result.Success)
                    {
                        if (Request.downloadHandler is not DownloadHandlerFile)
                        {
                            var downloadedBytes = Request.downloadHandler?.data ?? _buffer;
                            if (downloadedBytes == null || downloadedBytes.Length == 0)
                                return;

                            if (allowCaching)
                            {
                                if (!cachedFile.Exists)
                                {
                                    if (cachedFile.Directory is { Exists: false })
                                        cachedFile.Directory.Create();
                                }

                                await using var write = cachedFile.OpenWrite();
                                using var memoryStream = new MemoryStream(downloadedBytes);
                                await memoryStream.CopyToAsync(write, cancellationToken);
                                _buffer = memoryStream.ToArray();
                            }
                            else
                                _buffer = downloadedBytes;
                        }
                    }
                }
                finally
                {
                    CacheRequests.Remove(_cachedItemHash, out _);
                    _lockedCache = false;
                }

            }).AsTask();
        }

        public static CacheableUnityWebRequest Get(string uriString, DownloadHandler downloadHandler = null, UploadHandler uploadHandler = null)
        {
            return Get(new Uri(uriString), downloadHandler, uploadHandler);
        }

        public static CacheableUnityWebRequest Get(Uri uri, DownloadHandler downloadHandler = null, UploadHandler uploadHandler = null)
        {
            return new CacheableUnityWebRequest(uri, HttpMethod.Get.Method, downloadHandler, uploadHandler);
        }

        private static string ComputeHash(string uri)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = Encoding.ASCII.GetBytes(uri);
            var hashBytes = md5.ComputeHash(inputBytes);
            var sb = new StringBuilder();
            foreach (var t in hashBytes)
                sb.Append(t.ToString("X2"));
            return sb.ToString();
        }

        public void Dispose()
        {
            Request?.Dispose();

            _buffer = null;

            if (string.IsNullOrEmpty(_cachedItemHash) || !_lockedCache) 
                return;
            
            CacheRequests.Remove(_cachedItemHash, out _);
            _lockedCache = false;
            _cachedItemHash = null;
        }
    }
}