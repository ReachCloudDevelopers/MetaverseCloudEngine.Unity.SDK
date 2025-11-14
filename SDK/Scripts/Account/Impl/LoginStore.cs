using System;
using System.Threading;
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
        private readonly SemaphoreSlim _tokenPersistenceSemaphore = new(1, 1);

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

            if (ApiClient.Account.UseCookieAuthentication || !string.IsNullOrEmpty(AccessToken))
            {
#if UNITY_EDITOR
                await WaitForNetworkConnectivityAsync();  // Skip network wait in editor
#endif

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

                        MetaverseProgram.Logger.LogWarning($"LoginStore: Token validation during initialization failed with status {(int)response.StatusCode} ({response.StatusCode}). Details: {serverReason}");

                        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
#if UNITY_EDITOR
                            _isInitialized = true;
                            return;
#endif
                        }

                        _initializationRetries++;
                        var delaySeconds = Math.Min(2, 0.5 * _initializationRetries); // Short backoff

                        // For 500+ errors: Retry up to 3 times, then optionally prompt to restart in editor
                        if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                        {
                            if (_initializationRetries >= maxRetries)
                            {
#if UNITY_EDITOR
                                if (!_hasShownRetryDialog)
                                {
                                    _hasShownRetryDialog = true;
                                    await ShowRestartDialogAsync(response.StatusCode.ToString());
                                }
#endif
                                _isInitialized = true;
                                return;
                            }

                            await DelayAsync((int)(delaySeconds * 1000));
                            await InitializeAsync();
                            return;
                        }

                        // For other errors: Up to 3 retries
                        if (_initializationRetries < maxRetries)
                        {
                            await DelayAsync((int)(delaySeconds * 1000));
                            await InitializeAsync();
                        }
                    }

                    _isInitialized = true;
                    return;
                }

                await ClearPersistedTokensAsync("InitializeAsync:Unauthorized");
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

        private void OnLoggedIn(SystemUserDto user, AccountController.LogInKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            PersistCurrentTokensAsync("OnLoggedIn").Forget();
        }

        private void OnLoggedOut(SystemUserDto user, AccountController.LogOutKind kind)
        {
            HandleLoggedOutAsync(kind).Forget();
        }

        private void OnTokensUpdated(UserTokenDto token)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            PersistCurrentTokensAsync("OnTokensUpdated").Forget();
        }

        private async UniTask PersistCurrentTokensAsync(string source)
        {
            // Get the current tokens from the API client (these should be the updated ones)
            var accessToken = ApiClient.Account.AccessToken;
            var refreshToken = ApiClient.Account.RefreshToken;

            await UpdateTokensSafeAsync(accessToken, refreshToken, source);
        }

        private async UniTask HandleLoggedOutAsync(AccountController.LogOutKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            // Don't clear tokens if initialization hasn't completed yet
            // This prevents tokens from being cleared during editor domain reload
            if (!_isInitialized)
            {
                return;
            }

            if (kind == AccountController.LogOutKind.InvalidAccessToken)
            {
#if UNITY_EDITOR
                await EnsureUnityThreadAsync();
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.DisplayDialog("Session Expired", "Your session has expired. Please sign in again.", "OK");
#endif
            }

            await ClearPersistedTokensAsync($"OnLoggedOut ({kind})");

            // Auto-retry login using environment credentials, if available
            await AttemptLoginWithEnvAsync($"OnLoggedOut ({kind})");
        }

        private async UniTask UpdateTokensSafeAsync(string accessToken, string refreshToken, string source)
        {
            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
                return;

            try
            {
                await EnsureUnityThreadAsync();

                await _tokenPersistenceSemaphore.WaitAsync();
                try
                {
                    PersistTokens(accessToken, refreshToken, source);
                }
                finally
                {
                    _tokenPersistenceSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Failed to persist tokens ({source}): {ex}");
            }
        }

        private async UniTask ClearPersistedTokensAsync(string source)
        {
            try
            {
                await EnsureUnityThreadAsync();

                await _tokenPersistenceSemaphore.WaitAsync();
                try
                {
                    ClearPersistedTokens(source);
                }
                finally
                {
                    _tokenPersistenceSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Failed to clear persisted tokens ({source}): {ex}");
            }
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
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Failed to flush preferences: {ex.Message}");
            }
        }

        [Conditional("UNITY_EDITOR")]
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
            try
            {
                // Ensure any last-moment PlayerPrefs writes are flushed before assembly reload.
                PlayerPrefs.Save();
#if UNITY_EDITOR
                // EditorPrefs doesn't have a Save method in all Unity versions, but PlayerPrefs.Save() handles both
                // In newer Unity versions, EditorPrefs.Save() may not exist, so we skip it
#endif
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"LoginStore: Failed to flush preferences before assembly reload: {ex.Message}");
            }
        }
#endif

        private string GetObfuscatedRefreshTokenKey() => _aes.EncryptString(nameof(RefreshToken));

        private string GetObfuscatedAccessTokenKey() => _aes.EncryptString(nameof(AccessToken));

        private async Task ShowRestartDialogAsync(string statusCode)
        {
#if UNITY_EDITOR
            await EnsureUnityThreadAsync();

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
                catch
                {
                    // Silently ignore
                }
            }
            return null;
        }

        private static (string username, string password)? GetEnvCredentials()
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
                    return false;
                }

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
                    return false;
                }

                // Tokens will be persisted via LoggedIn/TokensUpdated events
                return true;
            }
            catch
            {
                return false;
            }
        }


        private async Task WaitForNetworkConnectivityAsync()
        {
            await EnsureUnityThreadAsync();
            if (Application.isEditor)
            {
                return;
            }

            if (Application.internetReachability != NetworkReachability.NotReachable)
                return;

            while (Application.internetReachability == NetworkReachability.NotReachable)
            {
                await DelayAsync(2000);
                await EnsureUnityThreadAsync();
            }
        }

        private async Task DelayAsync(int milliseconds)
        {
#if UNITY_EDITOR
            await EnsureUnityThreadAsync();
            if (!Application.isPlaying)
            {
                await Task.Delay(milliseconds);
                return;
            }
#endif
            await UniTask.Delay(milliseconds);
        }

        private static async UniTask EnsureUnityThreadAsync()
        {
            if (PlayerLoopHelper.IsMainThread)
                return;

            await UniTask.SwitchToMainThread();
        }
    }
}
