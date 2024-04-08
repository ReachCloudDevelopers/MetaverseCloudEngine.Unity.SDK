using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Web.Implementation;
using System.Text;
using System;
using UnityEngine;
using System.Threading;
using MetaverseCloudEngine.Unity.Rendering;
using UnityEngine.Networking;

namespace MetaverseCloudEngine.Unity
{
    public static class MetaverseIpfsAPI
    {
        /// <summary>
        /// Converts an IPFS URI into a <see cref="GameObject"/> by downloading the GLB, or converting the UTF8 bytes into a GLB.
        /// </summary>
        /// <param name="urlOrUtf8Bytes">Either the IPFS URI or the bytes encoded as a UTF8 string.</param>
        /// <param name="onGameObjectValue">Invoked when the Game Object was generated.</param>
        /// <param name="onFailed">Invoked if the function fails.</param>
        /// <param name="validateUrl">Validates the IPFS URI to ensure it's a valid URI.</param>
        /// <param name="cancellationToken">Specify a cancellation token to allow cancellation of the download operation.</param>
        /// <returns>Whether or not the function has immediately failed or succeeded. Note: This will not return whether or not the function will eventually succeed, this merely tells whether the function has initiated an async action.</returns>
        public static bool FetchGlb(string urlOrUtf8Bytes, Action<GameObject> onGameObjectValue, Action onFailed = null,
            bool validateUrl = true, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(urlOrUtf8Bytes))
                {
                    onFailed?.Invoke();
                    return false;
                }

                if (!validateUrl || urlOrUtf8Bytes.StartsWith(MetaverseConstants.Urls.IpfsUri))
                {
                    var sanitizedUrl = urlOrUtf8Bytes.Replace(MetaverseConstants.Urls.IpfsUri,
                        MetaverseConstants.Urls.IpfsGateway);
                    var uri = new Uri(sanitizedUrl);
                    var downloader = new UnityAssetPlatformGltf(uri, unloadOnDestroy: true)
                        { RawImport = true, IgnoreCacheModifications = true };
                    downloader.LoadAssetAsync<GameObject>(string.Empty, cancellationToken)
                        .Then(r => { onGameObjectValue?.Invoke(r); }, e =>
                        {
                            MetaverseProgram.Logger.LogError(e);
                            onFailed?.Invoke();
                        }, cancellationToken: cancellationToken);

                    return true;
                }

                var utf8Bytes = Encoding.UTF8.GetBytes(urlOrUtf8Bytes);
                if (utf8Bytes.Length > 0)
                {
                    var downloader = new UnityAssetPlatformGltf(utf8Bytes)
                        { RawImport = true, IgnoreCacheModifications = true };
                    downloader.LoadAssetAsync<GameObject>(string.Empty, cancellationToken)
                        .Then(r => { onGameObjectValue?.Invoke(r); }, e =>
                        {
                            MetaverseProgram.Logger.LogError(e);
                            onFailed?.Invoke();
                        }, cancellationToken: cancellationToken);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
                onFailed?.Invoke();
                return false;
            }
        }

        /// <summary>
        /// Converts an IPFS URI into a <see cref="Texture2D"/> by downloading the texture, or converting the UTF8 bytes into a texture.
        /// </summary>
        /// <param name="urlOrUtf8Bytes">Either the IPFS URI or the bytes encoded as a UTF8 string.</param>
        /// <param name="onTextureValue">Invoked when the texture was generated.</param>
        /// <param name="onSpriteValue">Invoked when the sprite was generated.</param>
        /// <param name="onFailed">Invoked if the function fails.</param>
        /// <param name="validateUrl">Validates the IPFS URI to ensure it's a valid URI.</param>
        /// <returns>Whether or not the function has immediately failed or succeeded. Note: This will not return whether or not the function will eventually succeed, this merely tells whether the function has initiated an async action.</returns>
        public static bool FetchImage(string urlOrUtf8Bytes, Action<Texture2D> onTextureValue,
            Action<Sprite> onSpriteValue, Action onFailed = null, bool validateUrl = true)
        {
            try
            {
                if (string.IsNullOrEmpty(urlOrUtf8Bytes))
                {
                    onFailed?.Invoke();
                    return false;
                }

                void OnLoadImageSuccess(Texture2D tex)
                {
                    onTextureValue?.Invoke(tex);
                    onSpriteValue?.Invoke(Sprite.Create(
                        tex,
                        new Rect(0, 0, tex.width, tex.height),
                        Vector2.zero,
                        1,
                        0,
                        SpriteMeshType.FullRect));
                }

                bool LoadBytes(byte[] data)
                {
                    var tex = TextureCache.ReuseTexture2D();
                    if (!tex.LoadImage(data))
                        return false;
                    OnLoadImageSuccess(tex);
                    return true;
                }

                if (!validateUrl || urlOrUtf8Bytes.StartsWith(MetaverseConstants.Urls.IpfsUri))
                {
                    var sanitizedUrl = urlOrUtf8Bytes.Replace(MetaverseConstants.Urls.IpfsUri,
                        MetaverseConstants.Urls.IpfsGateway);
                    var uri = new Uri(sanitizedUrl);
                    var uwr = CacheableUnityWebRequest.Get(uri, downloadHandler: new DownloadHandlerTexture());
                    uwr.SendWebRequestAsync().Then(() =>
                    {
                        if (uwr.Success)
                        {
                            var t2d = uwr.GetTexture();
                            if (t2d)
                                OnLoadImageSuccess(t2d);
                            else
                                onFailed?.Invoke();
                        }
                        else
                            onFailed?.Invoke();
                    }, _ => onFailed?.Invoke());

                    return true;
                }

                var utf8Bytes = Encoding.UTF8.GetBytes(urlOrUtf8Bytes);
                return utf8Bytes.Length > 0 && LoadBytes(utf8Bytes);
            }
            catch (Exception e)
            {
                MetaverseProgram.Logger.LogError(e);
                onFailed?.Invoke();
                return false;
            }
        }
    }
}