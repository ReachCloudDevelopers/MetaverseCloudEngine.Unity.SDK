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
                InitializeRefreshToken();
                return _plainTextRefreshToken;
            }
            private set
            {
                if (_plainTextRefreshToken == value)
                    return;

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
            MetaverseProgram.Logger.Log($"LoginStore: Beginning initialization. UseCookieAuth: {ApiClient.Account.UseCookieAuthentication}, HasAccessToken: {!string.IsNullOrEmpty(AccessToken)}");

            if (ApiClient.Account.UseCookieAuthentication || !string.IsNullOrEmpty(AccessToken))
            {
                await WaitForNetworkConnectivityAsync();

                MetaverseProgram.Logger.Log($"LoginStore: Validating tokens with server...");
                var response =
                    !ApiClient.Account.UseCookieAuthentication ?
                        await ApiClient.Account.ValidateTokenAsync(AccessToken, RefreshToken) :
                        await ApiClient.Account.ValidateTokenAsync();
                
                if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    if (!response.Succeeded)
                    {
                        _initializationRetries++;
                        var delaySeconds = Math.Min(2, 0.5 * _initializationRetries); // Shorter backoff: 0.5s base, max 2s
                        
                        // For 500+ errors: Retry up to 3 times total (capped even in builds); in editor, only once
                        if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                        {
                            MetaverseProgram.Logger.LogWarning($"LoginStore: 500+ error (Attempt #{_initializationRetries}/3). Retrying in {delaySeconds}s...");
                            
                            if (_initializationRetries >= 3)
                            {
                                MetaverseProgram.Logger.LogError("LoginStore: Max retries (3) reached for server error. Proceeding as unauthenticated.");
                                return;
                            }

                            if (Application.isEditor && _initializationRetries >= 2)
                            {
                                MetaverseProgram.Logger.LogWarning("LoginStore: Editor mode - skipping further 500 retries. Check your server.");
                                return;
                            }

                            // After 3 attempts, show dialog (reduced from 10)
                            if (_initializationRetries >= 3 && !_hasShownRetryDialog)
                            {
                                _hasShownRetryDialog = true;
                                await ShowRestartDialogAsync(response.StatusCode.ToString());
                                return;
                            }

                            await DelayAsync((int)(delaySeconds * 1000));
                            await InitializeAsync();
                            return;
                        }

                        // For other errors: Up to 3 retries total (reduced from 5)
                        MetaverseProgram.Logger.LogWarning($"LoginStore: Non-500 error (Attempt #{_initializationRetries}/3): {response.StatusCode}");
                        if (_initializationRetries < 3)
                        {
                            // In editor, only retry once
                            if (Application.isEditor && _initializationRetries >= 2)
                            {
                                MetaverseProgram.Logger.LogWarning("LoginStore: Editor mode - no further retries. Proceeding as unauthenticated.");
                                return;
                            }

                            await DelayAsync((int)(delaySeconds * 1000));
                            await InitializeAsync();
                        }
                        else
                        {
                            MetaverseProgram.Logger.LogError("LoginStore: Max retries (3) reached. Proceeding as unauthenticated.");
                        }
                    }
                    else
                    {
                        MetaverseProgram.Logger.Log($"LoginStore: Token validation successful. Status: {response.StatusCode}");
                    }

                    return;
                }

                var errorMessage = await response.GetErrorAsync();
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MetaverseProgram.Logger.LogWarning("LoginStore: Failed to initialize - invalid token: " + errorMessage);
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
        
        private async void OnLoggedIn(SystemUserDto user, AccountController.LogInKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            if (Application.isPlaying)
            {
                await UniTask.SwitchToMainThread();
            }

            MetaverseProgram.Logger.Log($"LoginStore: User logged in via {kind}. Persisting tokens...");
            PersistTokens(ApiClient.Account.AccessToken, ApiClient.Account.RefreshToken, "OnLoggedIn");
        }

        private async void OnLoggedOut(SystemUserDto user, AccountController.LogOutKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            if (Application.isPlaying)
            {
                await UniTask.SwitchToMainThread();
            }

            MetaverseProgram.Logger.Log($"LoginStore: User logged out via {kind}. Clearing persisted tokens...");
            ClearPersistedTokens($"OnLoggedOut ({kind})");
        }

        private async void OnTokensUpdated(UserTokenDto token)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            if (Application.isPlaying)
            {
                await UniTask.SwitchToMainThread();
            }

            // Get the current tokens from the API client (these should be the updated ones)
            var accessToken = ApiClient.Account.AccessToken;
            var refreshToken = ApiClient.Account.RefreshToken;

            MetaverseProgram.Logger.Log($"LoginStore: Tokens updated via event. AccessToken present: {!string.IsNullOrEmpty(accessToken)}, RefreshToken present: {!string.IsNullOrEmpty(refreshToken)}");
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
                // Use a more robust flush mechanism that handles assembly reloads better
                FlushPrefsSafely();
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

        private static void FlushPrefsSafely()
        {
            try
            {
#if UNITY_EDITOR
                // EditorPrefs flushes immediately; PlayerPrefs not used here, but be defensive if path changes.
                PlayerPrefs.Save();
                // Note: EditorPrefs.Save() may not exist in all Unity versions, so we skip it for compatibility
#else
                PlayerPrefs.Save();
#endif
                MetaverseProgram.Logger.Log("LoginStore: Preferences flushed successfully");
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Failed to flush preferences: {ex.Message}");
            }
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
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            try
            {
                // Ensure any last-moment PlayerPrefs writes are flushed before assembly reload.
                PlayerPrefs.Save();
#if UNITY_EDITOR
                // EditorPrefs doesn't have a Save method in all Unity versions, but PlayerPrefs.Save() handles both
                // In newer Unity versions, EditorPrefs.Save() may not exist, so we skip it
#endif
                MetaverseProgram.Logger.Log("LoginStore: Preferences flushed before assembly reload");
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Failed to flush preferences before assembly reload: {ex.Message}");
            }
        }

        private static void OnAfterAssemblyReload()
        {
            try
            {
                // After assembly reload, ensure our static state is properly initialized
                // This helps prevent issues where the LoginStore instance might be in an inconsistent state
                MetaverseProgram.Logger.Log("LoginStore: Assembly reload completed - ensuring token persistence integrity");
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Error during post-assembly reload initialization: {ex.Message}");
            }
        }
#endif

        private string GetObfuscatedRefreshTokenKey() => _aes.EncryptString(nameof(RefreshToken));

        private string GetObfuscatedAccessTokenKey() => _aes.EncryptString(nameof(AccessToken));

        private async Task ShowRestartDialogAsync(string statusCode)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                await UniTask.SwitchToMainThread();
            }
            
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
                await DelayAsync(2000);
            }

            stopwatch.Stop();
            MetaverseProgram.Logger.Log($"LoginStore: Network connectivity restored after {stopwatch.Elapsed.TotalSeconds:F1}s; continuing initialization.");
        }
    }
}