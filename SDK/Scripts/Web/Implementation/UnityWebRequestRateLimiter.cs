using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class UnityWebRequestRateLimiter : IDisposable
    {
        private readonly string _key;

        // Dictionary to store semaphores for each key
        private static readonly IDictionary<string, SemaphoreSlim> ActiveSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

        private UnityWebRequestRateLimiter(string key)
        {
            _key = key;
        }

        // Maximum number of concurrent requests allowed globally
        public static int MaximumConcurrentRequestsPerKey { get; set; } = 15;

        // Method to enter the rate-limited section asynchronously
        public static async Task<UnityWebRequestRateLimiter> EnterAsync(string key = default, CancellationToken cancellationToken = default)
        {
            key ??= string.Empty;
            var semaphore = ActiveSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(MaximumConcurrentRequestsPerKey, MaximumConcurrentRequestsPerKey));
            try
            {
                await MVUtils.AwaitSemaphore(semaphore, timeout: null, cancellationToken);
            }
            finally
            {
                ActiveSemaphores.Remove(key);
            }
            return new UnityWebRequestRateLimiter(key);
        }

        // Dispose method to release the semaphore
        public void Dispose()
        {
            if (ActiveSemaphores.TryGetValue(_key, out var semaphore))
            {
                semaphore.Release();
            }
        }
    }
}