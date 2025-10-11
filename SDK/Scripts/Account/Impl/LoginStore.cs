using System;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Account.Abstract;
using MetaverseCloudEngine.Unity.Encryption;
using MetaverseCloudEngine.Unity.Services.Abstract;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Diagnostics;

namespace MetaverseCloudEngine.Unity.Account.Poco
{
    /// <summary>
    /// An implementation of <see cref="ILoginStore"/> that stores the login data in <see cref="IPrefs"/> and
    /// uses the <see cref="MetaverseClient"/> to login/out.
    /// </summary>
    public class LoginStore : ILoginStore
    {
        private readonly AES _aes;

        private string _plainTextAccessToken;
        private string _plainTextRefreshToken;
        private int _initializationRetries;
        private bool _hasShownRetryDialog;

        /// <summary>
        /// Creates a new instance of <see cref="LoginStore"/>.
        /// </summary>
        /// <param name="prefs">The configuration provider to use for storage.</param>
        /// <param name="apiClient">The API client to log in/out from and to get login data from.</param>
        public LoginStore(MetaverseClient apiClient, IPrefs prefs)
        {
            ApiClient = apiClient;
            Prefs = prefs;
            _aes = new AES();
            ApiClient.Account.LoggedIn += OnLoggedIn;
            ApiClient.Account.LoggedOut += OnLoggedOut;
            ApiClient.Account.TokensUpdated += OnTokensUpdated;
        }

        /// <summary>
        /// The login access token.
        /// </summary>
        public string AccessToken
        {
            get
            {
                var runtimeToken = ApiClient?.Account?.AccessToken;
                if (!string.IsNullOrEmpty(runtimeToken) && runtimeToken != _plainTextAccessToken)
                {
                    // Persist the freshest runtime token so subsequent accesses remain in sync.
                    AccessToken = runtimeToken;
                    return _plainTextAccessToken;
                }

                InitializeAccessToken();
                return _plainTextAccessToken;
            }
            private set
            {
                if (_plainTextAccessToken == value) 
                    return;
                
                _plainTextAccessToken = value;
                
                if (!string.IsNullOrEmpty(_plainTextAccessToken))
                    Prefs.SetString(GetObfuscatedAccessTokenKey(), _aes.EncryptString(_plainTextAccessToken));   
                else
                    Prefs.DeleteKey(GetObfuscatedAccessTokenKey());
            }
        }

        /// <summary>
        /// The login refresh token.
        /// </summary>
        public string RefreshToken
        {
            get
            {
                var runtimeToken = ApiClient?.Account?.RefreshToken;
                if (!string.IsNullOrEmpty(runtimeToken) && runtimeToken != _plainTextRefreshToken)
                {
                    // Persist the freshest runtime token so subsequent accesses remain in sync.
                    RefreshToken = runtimeToken;
                    return _plainTextRefreshToken;
                }

                InitializeRefreshToken();
                return _plainTextRefreshToken;
            }
            private set
            {
                if (_plainTextRefreshToken != value)
                {
                    _plainTextRefreshToken = value;
                    if (!string.IsNullOrEmpty(_plainTextRefreshToken))
                    {
                        Prefs.SetString(GetObfuscatedRefreshTokenKey(), _aes.EncryptString(_plainTextRefreshToken));
                    }
                    else
                    {
                        Prefs.DeleteKey(GetObfuscatedRefreshTokenKey());
                    }
                }
            }
        }

        /// <summary>
        /// The configuration provider to use for storage.
        /// </summary>
        private IPrefs Prefs { get; }

        /// <summary>
        /// The API client to log in/out from and to get login data from.
        /// </summary>
        private MetaverseClient ApiClient { get; }

        /// <summary>
        /// A flag that indicates whether we're logged in.
        /// </summary>
        public bool IsLoggedIn => ApiClient.Account.CurrentUser != null;

        public async Task InitializeAsync()
        {
            if (ApiClient.Account.UseCookieAuthentication || !string.IsNullOrEmpty(AccessToken))
            {
                await WaitForNetworkConnectivityAsync();

                var response = 
                    !ApiClient.Account.UseCookieAuthentication ?
                        await ApiClient.Account.ValidateTokenAsync(AccessToken, RefreshToken) :
                        await ApiClient.Account.ValidateTokenAsync();
                
                if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    if (!response.Succeeded)
                    {
                        _initializationRetries++;
                        var delaySeconds = Math.Min(30, 2 * _initializationRetries); // Exponential backoff, max 30s
                        // Infinite retry for 500 Internal Server Error
                        if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                        {
                            MetaverseProgram.Logger.LogWarning($"LoginStore Initialization failed with {response.StatusCode}. Retrying in {delaySeconds}s... (Attempt #{_initializationRetries})");
                            
                            // After 10 attempts, notify the user and offer to restart Unity
                            if (_initializationRetries >= 10 && !_hasShownRetryDialog)
                            {
                                _hasShownRetryDialog = true;
                                await ShowRestartDialogAsync(response.StatusCode.ToString());
                                return;
                            }
                            
                            if (Application.isEditor)
                                await Task.Delay(delaySeconds * 1000);
                            else
                                await UniTask.Delay(delaySeconds * 1000);
                            await InitializeAsync();
                            return;
                        }
                        
                        // For other errors, retry up to 5 times
                        MetaverseProgram.Logger.LogWarning("LoginStore Initialization failed: " + response.StatusCode);
                        if (_initializationRetries < 5)
                        {
                            if (Application.isEditor)
                                await Task.Delay(delaySeconds * 1000);
                            else
                                await UniTask.Delay(delaySeconds * 1000);
                            await InitializeAsync();
                        }
                    }

                    if (!ApiClient.Account.UseCookieAuthentication)
                    {
                        AccessToken = ApiClient.Account.AccessToken;
                        RefreshToken = ApiClient.Account.RefreshToken;
                    }
                    return; 
                }

                var errorMessage = await response.GetErrorAsync();
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MetaverseProgram.Logger.LogWarning("Failed to initialize login store. Invalid access token: " + errorMessage);
                }

                AccessToken = null;
                RefreshToken = null;
            }
        }

        private void InitializeAccessToken()
        {
            if (!string.IsNullOrEmpty(_plainTextAccessToken)) return;
            var encrypted = Prefs.GetString(GetObfuscatedAccessTokenKey(), null);
            if (!string.IsNullOrEmpty(encrypted))
                _plainTextAccessToken = _aes.DecryptString(encrypted);
        }

        private void InitializeRefreshToken()
        {
            if (!string.IsNullOrEmpty(_plainTextRefreshToken)) return;
            var encrypted = Prefs.GetString(GetObfuscatedRefreshTokenKey(), null);
            if (!string.IsNullOrEmpty(encrypted))
                _plainTextRefreshToken = _aes.DecryptString(encrypted);
        }
        
        private void OnLoggedIn(SystemUserDto user, AccountController.LogInKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            PersistTokens(ApiClient.Account.AccessToken, ApiClient.Account.RefreshToken, "OnLoggedIn");
        }

        private void OnLoggedOut(SystemUserDto user, AccountController.LogOutKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            ClearPersistedTokens($"OnLoggedOut ({kind})");
        }

        private void OnTokensUpdated(UserTokenDto token)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            // Prefer values supplied by the event, but fall back to the client's current tokens
            var accessToken = string.IsNullOrEmpty(token?.AccessToken)
                ? ApiClient.Account.AccessToken
                : token.AccessToken;
            var refreshToken = string.IsNullOrEmpty(token?.RefreshToken)
                ? ApiClient.Account.RefreshToken
                : token.RefreshToken;

            PersistTokens(accessToken, refreshToken, "OnTokensUpdated");
        }

        private void PersistTokens(string accessToken, string refreshToken, string source)
        {
            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
                return;

            var changed = false;

            if (!string.IsNullOrEmpty(accessToken) && _plainTextAccessToken != accessToken)
            {
                AccessToken = accessToken;
                changed = true;
            }

            if (!string.IsNullOrEmpty(refreshToken) && _plainTextRefreshToken != refreshToken)
            {
                RefreshToken = refreshToken;
                changed = true;
            }

            if (changed)
            {
                MetaverseProgram.Logger.Log($"LoginStore: {source} - Tokens persisted (immediate)");
                FlushPrefs();
            }
        }

        private void ClearPersistedTokens(string source)
        {
            if (string.IsNullOrEmpty(_plainTextAccessToken) && string.IsNullOrEmpty(_plainTextRefreshToken))
                return;

            AccessToken = null;
            RefreshToken = null;
            MetaverseProgram.Logger.Log($"LoginStore: {source} - Tokens cleared (immediate)");
            FlushPrefs();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void FlushPrefs()
        {
#if UNITY_EDITOR
            // EditorPrefs flushes immediately; PlayerPrefs not used here, but be defensive if path changes.
            PlayerPrefs.Save();
#else
            PlayerPrefs.Save();
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterAssemblyReloadHandler()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            // Ensure any last-moment PlayerPrefs writes are flushed.
            PlayerPrefs.Save();
        }
#endif

        private string GetObfuscatedRefreshTokenKey() => _aes.EncryptString(nameof(RefreshToken));

        private string GetObfuscatedAccessTokenKey() => _aes.EncryptString(nameof(AccessToken));

        private async Task ShowRestartDialogAsync(string statusCode)
        {
#if UNITY_EDITOR
            await UniTask.SwitchToMainThread();
            
            var message = $"The Login Store has been unable to connect to the authentication server after {_initializationRetries} attempts (Status: {statusCode}).\n\n" +
                         "This is usually caused by:\n" +
                         "• Network connectivity issues\n" +
                         "• Server maintenance\n" +
                         "• DNS resolution problems\n\n" +
                         "Would you like to restart Unity? This may help resolve the issue.";
            
            var restart = UnityEditor.EditorUtility.DisplayDialog(
                "Login Store Connection Issues",
                message,
                "Restart Unity",
                "Keep Retrying");
            
            if (restart)
            {
                MetaverseProgram.Logger.Log("User requested Unity restart due to LoginStore connection issues.");
                UnityEditor.EditorApplication.OpenProject(Application.dataPath.Replace("/Assets", ""));
            }
            else
            {
                // Reset the flag so they can be prompted again after another 10 attempts
                _hasShownRetryDialog = false;
                MetaverseProgram.Logger.Log("User chose to keep retrying LoginStore initialization.");
                await InitializeAsync();
            }
#else
            // In builds, just log and continue retrying silently
            MetaverseProgram.Logger.LogWarning($"LoginStore has retried {_initializationRetries} times. Continuing to retry in background...");
            await InitializeAsync();
#endif
        }

        private async Task WaitForNetworkConnectivityAsync()
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
                return;

            var stopwatch = Stopwatch.StartNew();
            MetaverseProgram.Logger.LogWarning("LoginStore: Waiting for network connectivity before initializing session.");

            while (Application.internetReachability == NetworkReachability.NotReachable)
            {
                if (Application.isEditor)
                    await Task.Delay(2000);
                else
                    await UniTask.Delay(2000);
            }

            stopwatch.Stop();
            MetaverseProgram.Logger.Log($"LoginStore: Network connectivity restored after {stopwatch.Elapsed.TotalSeconds:F1}s; continuing initialization.");
        }
    }
}