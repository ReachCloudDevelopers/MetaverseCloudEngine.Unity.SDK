using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Abstract;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Web.Implementation
{
    public class UnityImage : IImage
    {
        private readonly Texture2D _texture;
        private const int MaximumMemoryCachedSprites = 50;
        private static readonly Dictionary<string, Sprite> SpriteCache = new ();

        public UnityImage(Texture2D texture)
        {
            _texture = texture;
        }

        public void GetImageBytes(Action<byte[]> completed = null)
        {
            completed?.Invoke(_texture.GetRawTextureData());
        }

        public Task<byte[]> GetImageBytesAsync()
        {
            return Task.FromResult(_texture.GetRawTextureData());
        }

        public void GetResultAs<T>(Action<T> completed = null)
        {
            var res = default(T);
            if (typeof(Texture2D).IsAssignableFrom(typeof(T)) && _texture)
            {
                res = (T)(object)_texture;
            }
            
            if (typeof(Sprite).IsAssignableFrom(typeof(T)) && _texture && _texture.isReadable)
            {
                if (SpriteCache.TryGetValue(_texture.GetInstanceID().ToString(), out var sprite) && sprite)
                {
                    res = (T)(object)sprite;
                }
                else
                {
                    if (SpriteCache.Count > MaximumMemoryCachedSprites)
                        SpriteCache.Clear();
                    sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                    res = (T)(object)sprite;
                    SpriteCache[_texture.GetInstanceID().ToString()] = sprite;
                }
            }
            
            completed?.Invoke(res);
        }

        public Task<T> GetResultAsAsync<T>()
        {
            TaskCompletionSource<T> t = new TaskCompletionSource<T>();
            GetResultAs<T>(x => t.SetResult(x));
            return t.Task;
        }
    }
}