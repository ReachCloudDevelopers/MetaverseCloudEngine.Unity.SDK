using MetaverseCloudEngine.ApiClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MetaverseCloudEngine.Unity.Async
{
    /// <summary>
    /// Extension methods for awaiting tasks. This is a convenience class to avoid having to use the
    /// <see cref="MetaverseDispatcher"/> directly.
    /// </summary>
    public static class MetaverseDispatcherExtensions
    {
        public static void Then(this Task task, Action onSuccess, Action<object> onError = null,
            CancellationToken cancellationToken = default) =>
            MetaverseDispatcher.Await(task, onSuccess, onError, cancellationToken);

        public static void Then<T>(this Task<T> task, Action<T> onSuccess, Action<object> onError = null,
            CancellationToken cancellationToken = default) =>
            MetaverseDispatcher.Await(task, onSuccess, onError, cancellationToken);

        public static void Always<T>(this Task<T> task, Action<object> onCompleted,
            CancellationToken cancellationToken = default) => task.Then(o => onCompleted?.Invoke(o),
            e => onCompleted?.Invoke(e), cancellationToken);

        public static void GetErrorAsync<T>(this ApiResponse<T> response, Action<object> onError = null,
            CancellationToken cancellationToken = default) =>
            response.GetErrorAsync().Always(onError, cancellationToken);

        public static void GetErrorAsync(this ApiResponse response, Action<object> onError = null,
            CancellationToken cancellationToken = default) =>
            response.GetErrorAsync().Always(onError, cancellationToken);

        public static void ResponseThen(this Task<ApiResponse> task, Action onSuccess, Action<object> onError = null,
            CancellationToken cancellationToken = default)
        {
            task.Then(response =>
            {
                if (response.Succeeded) onSuccess?.Invoke();
                else response.GetErrorAsync(onError, cancellationToken);
            }, onError, cancellationToken: cancellationToken);
        }

        public static void ResponseThen<T>(this Task<ApiResponse<T>> task, Action<T> onSuccess,
            Action<object> onError = null, CancellationToken cancellationToken = default)
        {
            task.Then(response =>
            {
                if (response.Succeeded)
                    response.GetResultAsync().Then(onSuccess, onError, cancellationToken: cancellationToken);
                else
                    response.GetErrorAsync().Always(e =>
                        {
                            string errorString = e.ToPrettyErrorString();
                            onError?.Invoke(errorString);
                        },
                        cancellationToken);
            }, onError, cancellationToken: cancellationToken);
        }

        public static void ResponseThen<T>(this Task<ApiResponse<T>> task, Action<T, ApiResponse> onSuccess,
            Action<object> onError = null, CancellationToken cancellationToken = default)
        {
            task.Then(response =>
            {
                if (response.Succeeded)
                    response.GetResultAsync().Then(x => onSuccess?.Invoke(x, response), onError,
                        cancellationToken: cancellationToken);
                else
                    response.GetErrorAsync()
                        .Always((e) => onError?.Invoke(e is Exception ex ? ex.Message : e.ToString()),
                            cancellationToken);
            }, onError, cancellationToken: cancellationToken);
        }
    }
}