using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Web.Implementation;
using UnityEngine;
using UnityEngine.Networking;

namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    ///An API for fetching resources from the cloud.
    /// </summary>
    public static class MetaverseResourcesAPI
    {
        /// <summary>
        /// The base URI Scheme for resources, like AI models, etc.
        /// </summary>
        public enum CloudResourcePath
        {
            /// <summary>
            /// The path for AI & ML models.
            /// </summary>
            AIModels,
        }

        /// <summary>
        /// Fetches a list of resources from the cloud and stores them in the specified output directory.
        /// </summary>
        /// <param name="files">The list of files to fetch.</param>
        /// <param name="outputDirectory">The output directory to store the files in.</param>
        /// <param name="outputs">Invoked when the files have been fetched.</param>
        public static void Fetch(
            List<(CloudResourcePath, string)> files,
            string outputDirectory,
            Action<string[]> outputs)
        {
            UniTask.Void(async () =>
            {
                foreach (var file in files)
                {
                    var resourcesUrl = ResourcesUrl(file.Item1, file.Item2);
                    using var request = CacheableUnityWebRequest.Get(resourcesUrl);
                    var outputPath = Path.Combine(
                        GetStreamingFolder(), 
                        outputDirectory, 
                        file.Item2);

                    request.Request.downloadHandler = new DownloadHandlerFile(outputPath, true)
                    {
                        removeFileOnAbort = false
                    };
                    request.Request.disposeDownloadHandlerOnDispose = true;
                    request.CustomCachePath = outputPath;
                    request.Request.redirectLimit = 0;

                    MetaverseProgram.Logger.Log($"{request.Request.method} {request.Request.url}");

                    await request.SendWebRequestAsync();
                    if (request.Success)
                    {
                        MetaverseProgram.Logger.Log($"{request.Request.method} {request.Request.url} ({request.ResponseCode})");
                        request.Request?.Dispose();
                        request.Request?.downloadHandler?.Dispose();
                        continue;
                    }

                    MetaverseProgram.Logger.LogError(
                        $"Failed to fetch resource '{file}' from '{resourcesUrl}': {request.ResponseCode}");
                    return;
                }

                MetaverseDispatcher.WaitForSeconds(1f, () =>
                {
                    outputs?.Invoke(files
                        .Select(file => Path.Combine(GetStreamingFolder(), outputDirectory, file.Item2))
                        .ToArray());
                });
            });
        }

        /// <summary>
        /// The base URI Scheme for resources, like AI models, etc.
        /// </summary>
        public static string ResourcesUrl(CloudResourcePath path, string file) =>
            $"{MetaverseConstants.Urls.ResourcesUrl}/{path}/{file}";

        private static string GetStreamingFolder()
        {
            return Application.temporaryCachePath + "/StreamingResources";
        }
    }
}