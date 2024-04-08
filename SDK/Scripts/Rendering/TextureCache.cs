using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace MetaverseCloudEngine.Unity.Rendering
{
    public static class TextureCache
    {
        private static readonly ConcurrentDictionary<TextureFormat, ObjectPool<Texture2D>> s_Texture2dPools = new();
        private static readonly ConcurrentDictionary<Texture2D, int> s_Texture2dReferenceCounts = new();

        public static Texture2D ReuseTexture2D()
        {
            GetObjectPoolForTextureFormat(TextureFormat.RGBA32).Get(out var texture);
            s_Texture2dReferenceCounts[texture] = 1;
            return texture;
        }

        public static Texture2D Reference(this Texture2D tex)
        {
            if (!tex)
                return null;
            if (!s_Texture2dReferenceCounts.TryGetValue(tex, out var count))
                return null;
            count++;
            s_Texture2dReferenceCounts[tex] = count;
            return tex;
        }

        public static Texture2D Dereference(this Texture2D tex)
        {
            if (!tex)
                return null;
            if (!s_Texture2dReferenceCounts.TryGetValue(tex, out var count))
                return null;
            count--;
            if (count <= 0)
            {
                if (s_Texture2dReferenceCounts.TryRemove(tex, out _) && count == 0)
                    GetObjectPoolForTextureFormat(tex.format).Release(tex);
                return tex;
            }

            s_Texture2dReferenceCounts[tex] = count;
            return tex;
        }

        public static void Cleanup()
        {
            foreach (var (texture, value) in s_Texture2dReferenceCounts)
            {
                if (value > 0)
                    continue;
                if (s_Texture2dReferenceCounts.TryRemove(texture, out _))
                    GetObjectPoolForTextureFormat(texture.format).Release(texture);
            }
            
            s_Texture2dReferenceCounts.Clear();
        }

        public static Task CleanupAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.RunOnThreadPool(async _ =>
            {
                const int maxTexturesPerFrame = 10;
                await UniTask.SwitchToMainThread();
                var count = 0;
                foreach (var (texture, value) in s_Texture2dReferenceCounts)
                {
                    if (value > 0)
                        continue;
                    if (s_Texture2dReferenceCounts.TryRemove(texture, out var _))
                        GetObjectPoolForTextureFormat(texture.format).Release(texture);
                    count++;
                    if (count >= maxTexturesPerFrame)
                        await UniTask.Yield(cancellationToken);
                }
                
            }, null, cancellationToken: cancellationToken).AsTask();
        }
        
        private static ObjectPool<Texture2D> GetObjectPoolForTextureFormat(TextureFormat format)
        {
            if (s_Texture2dPools.TryGetValue(format, out var pool)) 
                return pool;
            
            pool = new ObjectPool<Texture2D>(
                () => new Texture2D(1, 1, format, false),
                actionOnGet: texture => texture.SetPixels(new Color[1]), // Set the texture to a 1x1 black texture.
                actionOnRelease: texture => s_Texture2dReferenceCounts.TryRemove(texture, out _),
                actionOnDestroy: Object.Destroy, maxSize: 50);
            
            s_Texture2dPools.TryAdd(format, pool);
            
            return pool;
        }
    }
}