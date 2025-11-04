using System;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Account.Abstract;
using MetaverseCloudEngine.Unity.Encryption;
using MetaverseCloudEngine.Unity.Services.Abstract;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

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
        private bool _isInitialized;

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

        private static string BuildServerReasonMessage(string rawError)
        {
            if (string.IsNullOrWhiteSpace(rawError))
                return "No details";
            try
            {
                var token = JToken.Parse(rawError);
                // Try common fields often returned by APIs
                var reason = (string)(token.SelectToken("reason") ?? token.SelectToken("message") ?? token.SelectToken("error") ?? token.SelectToken("detail"));
                var refreshedAtStr = (string)(token.SelectToken("refreshedAt") ?? token.SelectToken("refreshTime") ?? token.SelectToken("refreshed_at"));
                var serverTimeStr = (string)(token.SelectToken("serverTimeUtc") ?? token.SelectToken("serverTime") ?? token.SelectToken("timeUtc") ?? token.SelectToken("timestamp"));
                var requestId = (string)(token.SelectToken("requestId") ?? token.SelectToken("traceId") ?? token.SelectToken("correlationId"));

                string FormatDate(string s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
                        return dt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                    return s;
                }

                var refreshedAt = FormatDate(refreshedAtStr);
                var serverTime = FormatDate(serverTimeStr);

                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(reason)) parts.Add($"Reason: {reason}");
                if (!string.IsNullOrWhiteSpace(refreshedAt)) parts.Add($"TokenRefreshedAt: {refreshedAt}");
                if (!string.IsNullOrWhiteSpace(serverTime)) parts.Add($"ServerTime: {serverTime}");
                if (!string.IsNullOrWhiteSpace(requestId)) parts.Add($"RequestId: {requestId}");

                // If we couldn't find structured fields, fall back to raw string
                if (parts.Count == 0)
                {
                    // If this is a string token, use it directly
                    if (token.Type == JTokenType.String)
                        return token.Value<string>();
                    // Otherwise return compact JSON
                    return token.ToString(Newtonsoft.Json.Formatting.None);
                }

                return string.Join(" | ", parts);
            }
            catch
            {
                // Not JSON - return raw text
                return rawError.Trim();
            }
        }

        public async Task InitializeAsync()
        {
            const int maxRetries = 3;

            MetaverseProgram.Logger.Log($"LoginStore: Beginning initialization. UseCookieAuth: {ApiClient.Account.UseCookieAuthentication}, HasAccessToken: {!string.IsNullOrEmpty(AccessToken)}");

            if (ApiClient.Account.UseCookieAuthentication || !string.IsNullOrEmpty(AccessToken))
            {
#if UNITY_EDITOR
                await WaitForNetworkConnectivityAsync();  // Skip network wait in editor
#endif

                MetaverseProgram.Logger.Log($"LoginStore: Validating tokens with server...");
                var response =
                    !ApiClient.Account.UseCookieAuthentication ?
                        await ApiClient.Account.ValidateTokenAsync(AccessToken, RefreshToken, refreshTokenRetries: 2) :  // Reduced retries globally for faster feedback
                        await ApiClient.Account.ValidateTokenAsync();

                if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    if (!response.Succeeded)
                    {
                        var errorDetails = await response.GetErrorAsync();
                        var serverReason = BuildServerReasonMessage(errorDetails);

                        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
#if UNITY_EDITOR
                            MetaverseProgram.Logger.LogWarning($"LoginStore: 403 Forbidden. Server: {serverReason}. Editor fast-fail; proceeding as unauthenticated.");
                            _isInitialized = true;
                            return;
#else
                            MetaverseProgram.Logger.LogWarning($"LoginStore: 403 Forbidden. Server: {serverReason}. Retrying with backoff...");
#endif
                        }

                        _initializationRetries++;
                        var delaySeconds = Math.Min(2, 0.5 * _initializationRetries); // Short backoff

                        // For 500+ errors: Retry up to 3 times
                        if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                        {
                            MetaverseProgram.Logger.LogWarning($"LoginStore: 5xx error (Attempt #{_initializationRetries}/{maxRetries}). Server: {serverReason}. Retrying in {delaySeconds}s...");

                            if (_initializationRetries >= maxRetries)
                            {
                                MetaverseProgram.Logger.LogError($"LoginStore: Max retries ({maxRetries}) reached for server error. Server: {serverReason}. Proceeding as unauthenticated.");
                                _isInitialized = true;
                                return;
                            }

                            // After maxRetries, show dialog
                            if (_initializationRetries >= maxRetries && !_hasShownRetryDialog)
                            {
                                _hasShownRetryDialog = true;
                                await ShowRestartDialogAsync(response.StatusCode.ToString());
                                _isInitialized = true;
                                return;
                            }

                            await DelayAsync((int)(delaySeconds * 1000));
                            await InitializeAsync();
                            return;
                        }

                        // For other errors: Up to 3 retries
                        MetaverseProgram.Logger.LogWarning($"LoginStore: Non-5xx error (Attempt #{_initializationRetries}/{maxRetries}). Status: {response.StatusCode}. Server: {serverReason}");
                        if (_initializationRetries < maxRetries)
                        {
                            await DelayAsync((int)(delaySeconds * 1000));
                            await InitializeAsync();
                        }
                        else
                        {
                            MetaverseProgram.Logger.LogError($"LoginStore: Max retries ({maxRetries}) reached. Server: {serverReason}. Proceeding as unauthenticated.");
                        }
                    }
                    else
                    {
                        MetaverseProgram.Logger.Log($"LoginStore: Token validation successful. Status: {response.StatusCode}");
                    }

                    _isInitialized = true;
                    return;
                }

                var errorMessage = await response.GetErrorAsync();
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MetaverseProgram.Logger.LogWarning("LoginStore: Failed to initialize - invalid token: " + BuildServerReasonMessage(errorMessage));
                }

                AccessToken = null;
                RefreshToken = null;
            }

            // Attempt environment-based login as a fallback when no tokens are present
            if (!ApiClient.Account.UseCookieAuthentication && string.IsNullOrEmpty(AccessToken))
            {
                var creds = GetEnvCredentials();
                if (creds.HasValue)
                {
                    await AttemptLoginWithEnvAsync("InitializeAsync");
                    _isInitialized = true;
                    return;
                }
            }

            _isInitialized = true;
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

            // Don't clear tokens if initialization hasn't completed yet
            // This prevents tokens from being cleared during editor domain reload
            if (!_isInitialized)
            {
                MetaverseProgram.Logger.Log($"LoginStore: Logout event during initialization (user: {(user != null ? "present" : "null")}), preserving tokens.");
                return;
            }

            if (kind == AccountController.LogOutKind.InvalidAccessToken)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.DisplayDialog("Session Expired", "Your session has expired. Please sign in again.", "OK");
                else
                    MetaverseProgram.Logger.LogWarning("LoginStore: Your session has expired. Please sign in again.");
#else
                MetaverseProgram.Logger.LogWarning("LoginStore: Your session has expired. Please sign in again.");
#endif
            }

            MetaverseProgram.Logger.Log($"LoginStore: User logged out via {kind}. Clearing persisted tokens...");
            ClearPersistedTokens($"OnLoggedOut ({kind})");

            // Auto-retry login using environment credentials, if available
            await AttemptLoginWithEnvAsync($"OnLoggedOut ({kind})");
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

        private static string ReadEnvironmentVariable(params string[] keys)
        {
            if (keys == null)
                return null;
            foreach (var key in keys)
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(key);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch (Exception ex)
                {
                    MetaverseProgram.Logger.LogWarning($"LoginStore: Failed to read environment variable '{key}': {ex.Message}");
                }
            }
            return null;
        }

        private (string username, string password)? GetEnvCredentials()
        {
            // Prefer METAVERSE_*; fall back to MV_*
            var username = ReadEnvironmentVariable("METAVERSE_USERNAME", "MV_USERNAME");
            var password = ReadEnvironmentVariable("METAVERSE_PASSWORD", "MV_PASSWORD");

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                return (username, password);

            return null;
        }

        private async Task<bool> AttemptLoginWithEnvAsync(string reason)
        {
            try
            {
                if (ApiClient.Account.UseCookieAuthentication)
                    return false;

                var creds = GetEnvCredentials();
                if (!creds.HasValue)
                {
                    // Keep this log at Info to aid debugging but avoid noise
                    MetaverseProgram.Logger.Log("LoginStore: No environment credentials detected; skipping auto-login.");
                    return false;
                }

                MetaverseProgram.Logger.Log($"LoginStore: Environment credentials detected ({reason}); attempting username/password sign-in.");
                await WaitForNetworkConnectivityAsync();

                var form = new GenerateSystemUserTokenForm
                {
                    UserNameOrEmail = creds.Value.username,
                    Password = creds.Value.password,
                    RememberMe = true
                };

                var response = await ApiClient.Account.PasswordSignInAsync(form);
                if (!response.Succeeded)
                {
                    var error = await response.GetErrorAsync();
                    MetaverseProgram.Logger.LogWarning($"LoginStore: Environment sign-in failed: {BuildServerReasonMessage(error)}");
                    return false;
                }

                // Tokens will be persisted via LoggedIn/TokensUpdated events
                MetaverseProgram.Logger.Log("LoginStore: Environment sign-in succeeded.");
                return true;
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Exception during environment sign-in: {ex.Message}");
                return false;
            }
        }


        private async Task WaitForNetworkConnectivityAsync()
        {
            if (Application.isEditor)
            {
                MetaverseProgram.Logger.Log("LoginStore: Editor mode - skipping network wait for speed.");
                return;
            }

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

        private async Task DelayAsync(int milliseconds)
        {
            if (Application.isPlaying)
            {
                await UniTask.Delay(milliseconds);
            }
            else
            {
                await Task.Delay(milliseconds);
            }
        }
    }
}