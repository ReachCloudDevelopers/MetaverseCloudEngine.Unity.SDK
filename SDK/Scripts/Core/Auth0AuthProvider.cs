using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.UI.Components;
using UnityEngine;
#if MV_VUPLEX_DEFINED
using TMPro;
using Object = UnityEngine.Object;
using UnityEngine.UI;
using Vuplex.WebView;
#endif

namespace MetaverseCloudEngine.Unity
{
    public class Auth0AuthProvider
    {
        private CancellationTokenSource _cancellationToken;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int OpenAuth0Popup(string popupUrl, string returnObjectName);
#endif

        public bool SupportsInAppUI
        {
            get
            {
#if !MV_VUPLEX_DEFINED || UNITY_STANDALONE
                return false;
#else
                return false; // TODO: Fix me...
#endif
            }
        }

        public void Start(Action finished, Action failed)
        {
            _cancellationToken = new CancellationTokenSource();

            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();

                Guid? organizationId = null;
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                organizationId = MetaverseProgram.RuntimeServices?.InternalOrganizationManager?.SelectedOrganization?.Id;
#endif
                var startRequest = await MetaverseProgram.ApiClient.Account.StartAuth0SignInAsync(organizationId);
                if (!startRequest.Succeeded)
                {
                    failed?.Invoke();
                    return;
                }

                await UniTask.Delay(5, cancellationToken: _cancellationToken.Token);

                var startResponse = await startRequest.GetResultAsync();
                if (Application.isMobilePlatform)
                {
                    var uri = new Uri(startResponse.SignInUrl);
                    var query = HttpUtility.ParseQueryString(uri.Query);
                    query["redirectUrl"] = Application.absoluteURL;
                    var uriBuilder = new UriBuilder(uri)
                    {
                        Query = query.ToString(),
                        Scheme = "https",
                        Port = -1
                    };
                    startResponse.SignInUrl = uriBuilder.ToString();
                }
                
                var code = await OpenLoginPopupAsync(startResponse.SignInUrl);
                if (code != "ok")
                {
                    failed?.Invoke();
                    return;
                }
                
                try
                {
                    var endRequest = await MetaverseProgram.ApiClient.Account.CompleteAuth0SignInAsync(
                        new GenerateSystemUserTokenAuth0Form
                        {
                            RequestToken = startResponse.RequestToken,
                        },
                        cancellationToken: _cancellationToken.Token);

                    if (!endRequest.Succeeded)
                    {
                        failed?.Invoke();
                        return;
                    }

                    finished?.Invoke();
                }
                catch
                {
                    failed?.Invoke();
                }
            });
        }

        public void Cancel()
        {
            _cancellationToken?.Cancel();
        }

        public void Unloaded()
        {
            if (Application.isMobilePlatform)
                return;
            _cancellationToken?.Cancel();
        }

        private async Task<string> OpenLoginPopupAsync(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var bridge = WebGLAuth0Bridge.GetOrCreate();

            // reset and prepare to wait for code/closure
            bridge.PrepareAwait();

            int opened = 0;
            try
            {
                opened = OpenAuth0Popup(url, bridge.gameObject.name);
            }
            catch
            {
                // fall through to fallback
            }

            if (opened == 0)
            {
                Application.OpenURL(url);
                return "ok";
            }

            return await bridge.WaitForAuthCodeAsync();
#elif !MV_VUPLEX_DEFINED
            Application.OpenURL(url);
            return await Task.FromResult<string>(null);
#else
            if (Application.isPlaying && SupportsInAppUI)
            {
                await CreateEmbeddedLogInWindowAsync(url, _cancellationToken);
            }
            else
                Application.OpenURL(url);
            return "ok";
#endif
        }

#if MV_VUPLEX_DEFINED
        private static async Task CreateEmbeddedLogInWindowAsync(string url, CancellationTokenSource cancellationToken = null)
        {
            var canvas = new GameObject("WebViewCanvas");
            var canvasComponent = canvas.AddComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasComponent.sortingOrder = 32767;
            var canvasScaler = canvas.AddComponent<CanvasScaler>();
            canvasScaler.matchWidthOrHeight = 0.75f;
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvas.AddComponent<GraphicRaycaster>();

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(canvas.transform, false);
            var image = background.GetComponent<Image>();
            image.color = ColorUtility.TryParseHtmlString("#1C1D1E", out var color) ? color : Color.black;
            image.rectTransform.anchorMin = new Vector2(0, 0);
            image.rectTransform.anchorMax = new Vector2(1, 1);
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;

            var layout = new GameObject("Layout", typeof(RectTransform));
            layout.transform.SetParent(canvas.transform, false);
            var layoutRt = layout.GetComponent<RectTransform>();
            layoutRt.anchorMin = new Vector2(0, 0);
            layoutRt.anchorMax = new Vector2(1, 1);
            layoutRt.offsetMin = Vector2.zero;
            layoutRt.offsetMax = Vector2.zero;
            layoutRt.gameObject.AddComponent<RectTransformSafeZone>();

            var closeButtonObj = new GameObject("CloseButton", typeof(Button));
            closeButtonObj.transform.SetParent(layoutRt, false);
            var closeButton = closeButtonObj.GetComponent<Button>();
            closeButton.image = closeButtonObj.AddComponent<Image>();
            closeButton.image.color = new Color(1, 1, 1, 0);
            var closeButtonRt = closeButton.GetComponent<RectTransform>();
            closeButtonRt.anchorMin = new Vector2(1f, 1f);
            closeButtonRt.anchorMax = new Vector2(1f, 1f);
            closeButtonRt.pivot = new Vector2(1f, 1f);
            closeButtonRt.sizeDelta = new Vector2(50, 50);
            closeButtonRt.anchoredPosition3D = new Vector3(-25, -15, 0);

            var closeTxt = new GameObject("&times", typeof(TextMeshProUGUI));
            closeTxt.transform.SetParent(closeButtonObj.transform, false);
            var tmp = closeTxt.GetComponent<TextMeshProUGUI>();
            tmp.text = "\u00D7";
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.rectTransform.anchorMin = new Vector2(0, 0);
            tmp.rectTransform.anchorMax = new Vector2(1, 1);
            tmp.rectTransform.offsetMin = Vector2.zero;
            tmp.rectTransform.offsetMax = Vector2.zero;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;

            var mainWebViewPrefab = CanvasWebViewPrefab.Instantiate();
            mainWebViewPrefab.Native2DModeEnabled = true;
            mainWebViewPrefab.InitialUrl = url;
            mainWebViewPrefab.transform.SetParent(layoutRt, false);

            var rt = mainWebViewPrefab.transform as RectTransform;
            if (rt)
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.offsetMin = new Vector2(0, 0);
                rt.offsetMax = new Vector2(0, -75);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            await mainWebViewPrefab.WaitUntilInitialized();

            if (cancellationToken?.IsCancellationRequested ?? false)
            {
                mainWebViewPrefab.Destroy();
                Object.Destroy(canvas);
                return;
            }

            mainWebViewPrefab.WebView.SetDefaultBackgroundEnabled(false);

            if (cancellationToken == null)
                return;

            cancellationToken.Token.Register(Destroy);
            mainWebViewPrefab.WebView.CloseRequested += (_, _) => Destroy();
            mainWebViewPrefab.WebView.LoadFailed += (_, _) => cancellationToken.Cancel();
            closeButton.onClick.AddListener(cancellationToken.Cancel);
            return;

            void Destroy()
            {
                if (mainWebViewPrefab)
                    mainWebViewPrefab.Destroy();
                if (canvas)
                    Object.Destroy(canvas);
            }
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// WebGL bridge that awaits a return code from JS (no cancellation).
    /// </summary>
    internal sealed class WebGLAuth0Bridge : MonoBehaviour
    {
        private static WebGLAuth0Bridge _instance;
        private TaskCompletionSource<string> _tcs;

        [Preserve]
        public static WebGLAuth0Bridge GetOrCreate()
        {
            if (_instance != null)
                return _instance;

            var go = new GameObject("WebGLAuth0Bridge");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WebGLAuth0Bridge>();
            return _instance;
        }

        public void PrepareAwait()
        {
            // New waiter each time
            _tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<string> WaitForAuthCodeAsync() => _tcs?.Task ?? Task.FromResult<string>(null);

        // Called by JS when popup closes without code
        [Preserve]
        public void OnAuth0PopupClosed()
        {
            // Resolve as null (graceful failure), do not Cancel/throw
            _tcs?.TrySetResult(null);
        }

        // Called by JS with the return code (string)
        [Preserve]
        public void OnAuth0ReturnCode(string code)
        {
            _tcs?.TrySetResult(code);
        }
    }
#endif
}
