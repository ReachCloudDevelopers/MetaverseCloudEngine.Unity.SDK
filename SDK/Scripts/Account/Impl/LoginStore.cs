using System;
using System.Threading;
using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Common.Models.Forms;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Account;
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
        private readonly SemaphoreSlim _sessionRecoverySemaphore = new(1, 1);

        private string _plainTextAccessToken;
        private string _plainTextRefreshToken;
        private DateTime? _accessTokenExpirationUtc;
        private bool _accessTokenExpirationInitialized;
        private bool _isRecoveringSession;
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

        private DateTime? AccessTokenExpirationUtc
        {
            get
            {
                InitializeAccessTokenExpiration();
                return _accessTokenExpirationUtc;
            }
            set
            {
                InitializeAccessTokenExpiration();

                if (_accessTokenExpirationUtc == value)
                    return;

                _accessTokenExpirationUtc = value?.ToUniversalTime();

                if (_accessTokenExpirationUtc.HasValue)
                {
                    var ticks = _accessTokenExpirationUtc.Value.Ticks.ToString();
                    Prefs.SetString(GetObfuscatedAccessTokenExpirationKey(), _aes.EncryptString(ticks));
                }
                else
                {
                    Prefs.DeleteKey(GetObfuscatedAccessTokenExpirationKey());
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

        private enum SessionRecoveryOutcome
        {
            Skipped,
            Recovered,
            Unauthorized,
            Failed,
        }

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
                // Prime the API client's token state immediately so editor tooling that runs before
                // validation completes can still send authenticated requests (or refresh if needed).
                if (!ApiClient.Account.UseCookieAuthentication)
                {
                    ApiClient.Account.AccessToken = AccessToken;
                    ApiClient.Account.RefreshToken = RefreshToken;
                    var resolvedExpirationUtc = ResolveAccessTokenExpirationUtc(AccessToken, AccessTokenExpirationUtc);
                    AccountTokenUtility.TrySetApiClientAccessTokenExpirationUtc(ApiClient.Account, resolvedExpirationUtc);
                    ConfigureRefreshThreshold(resolvedExpirationUtc);
                }

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

                        MetaverseProgram.Logger.LogWarning($"Metaverse Authentication: Token validation during initialization failed with status {(int)response.StatusCode} ({response.StatusCode}). Details: {serverReason}");

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

        private void InitializeAccessTokenExpiration()
        {
            if (_accessTokenExpirationInitialized)
                return;

            _accessTokenExpirationInitialized = true;

            var encrypted = Prefs.GetString(GetObfuscatedAccessTokenExpirationKey(), null);
            if (string.IsNullOrEmpty(encrypted))
                return;

            var decrypted = _aes.DecryptString(encrypted);
            if (string.IsNullOrEmpty(decrypted))
                return;

            if (long.TryParse(decrypted, out var ticks))
            {
                try
                {
                    _accessTokenExpirationUtc = new DateTime(ticks, DateTimeKind.Utc);
                }
                catch
                {
                    _accessTokenExpirationUtc = null;
                }

                return;
            }

            if (DateTime.TryParse(decrypted, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
            {
                _accessTokenExpirationUtc = dt.ToUniversalTime();
            }
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

            if (token == null)
            {
                PersistCurrentTokensAsync("OnTokensUpdated:null").Forget();
                return;
            }

            PersistTokenDtoAsync(token, "OnTokensUpdated").Forget();
        }

        private static TimeSpan ComputeRefreshThreshold(DateTime? accessTokenExpirationUtc)
        {
            const double minSeconds = 1;
            const double maxSeconds = 30;

            if (!accessTokenExpirationUtc.HasValue)
                return TimeSpan.FromSeconds(maxSeconds);

            var ttlSeconds = (accessTokenExpirationUtc.Value - DateTime.UtcNow).TotalSeconds;
            if (ttlSeconds <= 0)
                return TimeSpan.Zero;

            // Aim for a small window before expiry to avoid validate thrashing in editor tooling.
            // Clamp aggressively to avoid "always within threshold" when tokens have short lifetimes.
            var desiredSeconds = Math.Min(maxSeconds, Math.Max(minSeconds, ttlSeconds * 0.1));
            return TimeSpan.FromSeconds(desiredSeconds);
        }

        private void ConfigureRefreshThreshold(DateTime? accessTokenExpirationUtc)
        {
            try
            {
                if (ApiClient?.Account == null)
                    return;

                var desired = ComputeRefreshThreshold(accessTokenExpirationUtc);
                var current = ApiClient.Account.RefreshThreshold;

                // Respect explicit disable (<= 0) and avoid increasing a caller-configured smaller threshold.
                if (current <= TimeSpan.Zero)
                    return;

                if (desired < current)
                    ApiClient.Account.RefreshThreshold = desired;
            }
            catch
            {
                // ignore
            }
        }

        private static DateTime? NormalizeTokenExpirationUtc(DateTime tokenExpiration)
        {
            if (tokenExpiration == default)
                return null;

            return tokenExpiration.Kind switch
            {
                DateTimeKind.Utc => tokenExpiration,
                DateTimeKind.Local => tokenExpiration.ToUniversalTime(),
                _ => DateTime.SpecifyKind(tokenExpiration, DateTimeKind.Utc),
            };
        }

        private static DateTime? ResolveAccessTokenExpirationUtc(string accessToken, DateTime? fallbackExpirationUtc)
        {
            var jwtExpirationUtc = AccountTokenUtility.TryGetJwtExpirationUtc(accessToken);
            if (jwtExpirationUtc.HasValue)
                return jwtExpirationUtc.Value;

            if (!fallbackExpirationUtc.HasValue)
                return null;

            var value = fallbackExpirationUtc.Value;
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        private async UniTask<SessionRecoveryOutcome> TryRecoverSessionAfterInvalidAccessTokenLogoutAsync()
        {
            if (_isRecoveringSession)
                return SessionRecoveryOutcome.Failed;

            try
            {
                await EnsureUnityThreadAsync();

                // Pull from persisted state (ApiClient may have cleared its in-memory tokens on logout).
                var accessToken = AccessToken;
                var refreshToken = RefreshToken;

                if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
                    return SessionRecoveryOutcome.Skipped;

                await _sessionRecoverySemaphore.WaitAsync();
                _isRecoveringSession = true;
                try
                {
                    // Re-prime the API client state so subsequent calls can use tokens immediately.
                    ApiClient.Account.AccessToken = accessToken;
                    ApiClient.Account.RefreshToken = refreshToken;
                    var resolvedExpirationUtc = ResolveAccessTokenExpirationUtc(accessToken, AccessTokenExpirationUtc);
                    AccountTokenUtility.TrySetApiClientAccessTokenExpirationUtc(ApiClient.Account, resolvedExpirationUtc);
                    ConfigureRefreshThreshold(resolvedExpirationUtc);

                    const int maxAttempts = 3;
                    const int refreshTokenRetries = 2;

                    for (var attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        var response = await ApiClient.Account.ValidateTokenAsync(
                            accessToken,
                            refreshToken,
                            refreshTokenRetries: refreshTokenRetries);

                        if (response.Succeeded)
                            return SessionRecoveryOutcome.Recovered;

                        var errorDetails = await response.GetErrorAsync();
                        var serverReason = BuildServerReasonMessage(errorDetails);

                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            MetaverseProgram.Logger.LogWarning(
                                $"Metaverse Authentication: Session recovery failed with 401 Unauthorized. Details: {serverReason}");
                            return SessionRecoveryOutcome.Unauthorized;
                        }

                        MetaverseProgram.Logger.LogWarning(
                            $"Metaverse Authentication: Session recovery attempt {attempt}/{maxAttempts} failed with status {(int)response.StatusCode} ({response.StatusCode}). Details: {serverReason}");

                        if (attempt < maxAttempts)
                            await Task.Delay(250 * (int)Math.Pow(2, attempt - 1));
                    }

                    return SessionRecoveryOutcome.Failed;
                }
                finally
                {
                    _isRecoveringSession = false;
                    _sessionRecoverySemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogWarning($"Metaverse Authentication: Session recovery threw: {ex}");
                return SessionRecoveryOutcome.Failed;
            }
        }

        private async UniTask PersistCurrentTokensAsync(string source)
        {
            // Get the current tokens from the API client (these should be the updated ones)
            var accessToken = ApiClient.Account.AccessToken;
            var refreshToken = ApiClient.Account.RefreshToken;
            var accessTokenExpirationUtc = ResolveAccessTokenExpirationUtc(accessToken, AccessTokenExpirationUtc);

            await UpdateTokensSafeAsync(accessToken, refreshToken, accessTokenExpirationUtc, source);
        }

        private async UniTask PersistTokenDtoAsync(UserTokenDto token, string source)
        {
            if (token == null)
                return;

            // Prefer the DTO values (most reliable when refresh tokens are rotated), but fall back to
            // the AccountController properties in case the DTO is partial.
            var accessToken = !string.IsNullOrEmpty(token.AccessToken)
                ? token.AccessToken
                : ApiClient.Account.AccessToken;
            var refreshToken = !string.IsNullOrEmpty(token.RefreshToken)
                ? token.RefreshToken
                : ApiClient.Account.RefreshToken;

            var accessTokenExpirationUtc =
                ResolveAccessTokenExpirationUtc(accessToken, NormalizeTokenExpirationUtc(token.AccessTokenExpiration));

            await UpdateTokensSafeAsync(accessToken, refreshToken, accessTokenExpirationUtc, source);
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
                var recovery = await TryRecoverSessionAfterInvalidAccessTokenLogoutAsync();
                if (recovery == SessionRecoveryOutcome.Recovered)
                {
                    MetaverseProgram.Logger.Log("Metaverse Authentication: Session recovered after InvalidAccessToken logout.");
                    return;
                }

                // If recovery failed for non-401 reasons (transient network/server issues), keep the
                // persisted tokens so we can retry later instead of forcing re-auth.
                if (recovery == SessionRecoveryOutcome.Failed)
                {
                    MetaverseProgram.Logger.LogWarning(
                        "Metaverse Authentication: Logged out due to InvalidAccessToken but session recovery failed (non-401). Keeping persisted tokens to retry later.");
                    return;
                }

#if UNITY_EDITOR
                await EnsureUnityThreadAsync();
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.DisplayDialog("Session Expired", "Your session has expired. Please sign in again.", "OK");
#endif
            }

            await ClearPersistedTokensAsync($"OnLoggedOut ({kind})");

            // Auto-retry login using environment credentials, if available
            if (kind != AccountController.LogOutKind.PerformedByUser)
                await AttemptLoginWithEnvAsync($"OnLoggedOut ({kind})");
        }

        private async UniTask UpdateTokensSafeAsync(string accessToken, string refreshToken, DateTime? accessTokenExpirationUtc, string source)
        {
            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
                return;

            try
            {
                await EnsureUnityThreadAsync();

                // Keep API client's preflight token refresh logic stable (and reduce validate thrash)
                // by ensuring it has a sane expiration + refresh threshold.
                if (!ApiClient.Account.UseCookieAuthentication)
                {
                    var resolvedExpirationUtc = ResolveAccessTokenExpirationUtc(accessToken, accessTokenExpirationUtc);
                    AccountTokenUtility.TrySetApiClientAccessTokenExpirationUtc(ApiClient.Account, resolvedExpirationUtc);
                    ConfigureRefreshThreshold(resolvedExpirationUtc);
                    accessTokenExpirationUtc = resolvedExpirationUtc;
                }

                await _tokenPersistenceSemaphore.WaitAsync();
                try
                {
                    PersistTokens(accessToken, refreshToken, accessTokenExpirationUtc, source);
                }
                finally
                {
                    _tokenPersistenceSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                MetaverseProgram.Logger.LogError($"Metaverse Authentication: Failed to persist tokens ({source}): {ex}");
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
                MetaverseProgram.Logger.LogError($"Metaverse Authentication: Failed to clear persisted tokens ({source}): {ex}");
            }
        }

        private void PersistTokens(string accessToken, string refreshToken, DateTime? accessTokenExpirationUtc, string source)
        {
            if (string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(refreshToken))
                return;

            var changed = false;
            var refreshChanged = false;

            if (!string.IsNullOrEmpty(accessToken) && _plainTextAccessToken != accessToken)
            {
                AccessToken = accessToken;
                changed = true;
            }

            if (!string.IsNullOrEmpty(refreshToken) && _plainTextRefreshToken != refreshToken)
            {
                RefreshToken = refreshToken;
                changed = true;
                refreshChanged = true;
            }

            if (accessTokenExpirationUtc.HasValue)
            {
                var utc = accessTokenExpirationUtc.Value.ToUniversalTime();
                if (_accessTokenExpirationUtc != utc)
                {
                    AccessTokenExpirationUtc = utc;
                    changed = true;
                }
            }

            if (changed)
            {
                if (refreshChanged)
                    MetaverseProgram.Logger.Log($"Metaverse Authentication: Refresh token rotated ({source}).");

                // Use a more robust flush mechanism that handles assembly reloads better
                FlushPrefsSafely();
            }
        }

        private void ClearPersistedTokens(string source)
        {
            var hadAny =
                !string.IsNullOrEmpty(_plainTextAccessToken) ||
                !string.IsNullOrEmpty(_plainTextRefreshToken) ||
                AccessTokenExpirationUtc.HasValue;
            if (!hadAny)
                return;

            AccessToken = null;
            RefreshToken = null;
            AccessTokenExpirationUtc = null;
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
                MetaverseProgram.Logger.LogError($"Metaverse Authentication: Failed to flush preferences: {ex.Message}");
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
                MetaverseProgram.Logger.LogError($"Metaverse Authentication: Failed to flush preferences before assembly reload: {ex.Message}");
            }
        }
#endif

        private string GetObfuscatedRefreshTokenKey() => _aes.EncryptString(nameof(RefreshToken));

        private string GetObfuscatedAccessTokenKey() => _aes.EncryptString(nameof(AccessToken));

        private string GetObfuscatedAccessTokenExpirationKey() => _aes.EncryptString(nameof(AccessTokenExpirationUtc));

        private async Task ShowRestartDialogAsync(string statusCode)
        {
#if UNITY_EDITOR
            await EnsureUnityThreadAsync();

            var message = $"Unable to connect to the authentication server after {_initializationRetries} attempts (Status: {statusCode}).\n\n" +
                         "This is usually caused by:\n" +
                         "• Network connectivity issues\n" +
                         "• Server maintenance\n" +
                         "• DNS resolution problems\n\n" +
                         "Would you like to restart Unity? This may help resolve the issue.";

            var restart = UnityEditor.EditorUtility.DisplayDialogComplex(
                "Authentication Connection Issues",
                message,
                "Restart Unity",
                "Keep Retrying",
                "Continue Offline");

            if (restart == 0)
            {
                MetaverseProgram.Logger.Log("User requested Unity restart due to Metaverse Authentication connection issues.");
                UnityEditor.EditorApplication.OpenProject(Application.dataPath.Replace("/Assets", ""));
            }
            else if (restart == 1)
            {
                // Reset the flag so they can be prompted again after another 10 attempts
                _hasShownRetryDialog = false;
                MetaverseProgram.Logger.Log("User chose to keep retrying Metaverse Authentication initialization.");
                await InitializeAsync();
            }
            else
            {

            }
#else
            // In builds, just log and continue retrying silently
            MetaverseProgram.Logger.LogWarning($"Metaverse Authentication has retried {_initializationRetries} times. Continuing to retry in background...");
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
