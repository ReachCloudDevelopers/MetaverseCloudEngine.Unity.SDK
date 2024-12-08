using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Web.Abstract;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class MetaSpaceExternalApiService : IMetaSpaceExternalApiService
    {
        private readonly CancellationTokenSource _cancellationToken = new();

        public void Initialize()
        {
        }

        public void Dispose()
        {
            _cancellationToken.Cancel();
        }

        public void Delete(string query, Action<MetaSpaceExternalServiceResponse> callback)
        {
            if (!MetaverseProgram.IsCoreApp)
            {
                SdkResponse(callback);
                return;
            }

            MetaverseProgram.ApiClient.MetaSpaces.SendRequestToExternalService(new MetaSpaceExternalServiceRequestForm
            {
                InstanceUserId = GetCurrentUserID(),
                RequestUri = query,
                RequestMethod = HttpMethod.Delete.Method,

            }).Handle(callback, _cancellationToken.Token);
        }

        public void Get(string query, Action<MetaSpaceExternalServiceResponse> callback)
        {
            if (!MetaverseProgram.IsCoreApp)
            {
                SdkResponse(callback);
                return;
            }

            MetaverseProgram.ApiClient.MetaSpaces.SendRequestToExternalService(new MetaSpaceExternalServiceRequestForm
            {
                InstanceUserId = GetCurrentUserID(),
                RequestUri = query,
                RequestMethod = HttpMethod.Get.Method,

            }).Handle(callback, _cancellationToken.Token);
        }

        public void Post(string query, string body, string contentType, Action<MetaSpaceExternalServiceResponse> callback)
        {
            if (!MetaverseProgram.IsCoreApp)
            {
                SdkResponse(callback);
                return;
            }

            MetaverseProgram.ApiClient.MetaSpaces.SendRequestToExternalService(new MetaSpaceExternalServiceRequestForm
            {
                InstanceUserId = GetCurrentUserID(),
                RequestUri = query,
                RequestMethod = HttpMethod.Post.Method,
                RequestBody = body,
                MediaType = contentType

            }).Handle(callback, _cancellationToken.Token);
        }

        public void Put(string query, string body, string contentType, Action<MetaSpaceExternalServiceResponse> callback)
        {
            if (!MetaverseProgram.IsCoreApp)
            {
                SdkResponse(callback);
                return;
            }

            MetaverseProgram.ApiClient.MetaSpaces.SendRequestToExternalService(new MetaSpaceExternalServiceRequestForm
            {
                InstanceUserId = GetCurrentUserID(),
                RequestUri = query,
                RequestMethod = HttpMethod.Put.Method,
                RequestBody = body,
                MediaType = contentType

            }).Handle(callback, _cancellationToken.Token);
        }

        private static void SdkResponse(Action<MetaSpaceExternalServiceResponse> callback)
        {
            MetaverseDispatcher.WaitForSeconds(1.5f, () =>
            {
                callback?.Invoke(new MetaSpaceExternalServiceResponse
                {
                    StatusCode = 500,
                    Content = "",
                    ContentType = "",
                    ErrorMessage = "Not implemented",
                    Message = "Not implemented"
                });
            });
        }

        private static Guid GetCurrentUserID()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED
            return MetaverseProgram.RuntimeServices.InternalMatchmakingSystem.LocalUserID;
#else
            return Guid.Empty;
#endif
        }
    }

    internal static class MetaSpaceExternalApiServiceExtensions
    {
        public static void Handle(
            this Task<ApiResponse<MetaSpaceExternalServiceResponseDto>> task,
            Action<MetaSpaceExternalServiceResponse> callback,
            CancellationToken cancellationToken)
        {
            task.Then(r =>
            {
                if (r.Succeeded)
                {
                    r.GetResultAsync().Then(r =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            callback?.Invoke(new MetaSpaceExternalServiceResponse
                            {
                                StatusCode = (int)r.StatusCode,
                                Content = r.Content,
                                ContentType = r.ContentType,
                                ErrorMessage = r.Message,
                                Message = r.Message
                            });
                        }
                    }, cancellationToken: cancellationToken);

                    return;
                }

                r.GetErrorAsync().Then(err =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        callback?.Invoke(new MetaSpaceExternalServiceResponse
                        {
                            StatusCode = (int)r.StatusCode,
                            Content = err,
                            ContentType = "text/plain",
                            ErrorMessage = err,
                        });
                    }
                }, cancellationToken: cancellationToken);

            }, cancellationToken: cancellationToken);
        }
    }
}
