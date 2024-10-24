﻿using System.Threading.Tasks;
using MetaverseCloudEngine.ApiClient;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Account.Abstract;
using MetaverseCloudEngine.Unity.Encryption;
using MetaverseCloudEngine.Unity.Services.Abstract;
using UnityEngine;

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

        /// <summary>
        /// Creates a new instance of <see cref="LoginStore"/>.
        /// </summary>
        /// <param name="prefs">The configuration provider to use for storage.</param>
        /// <param name="apiClient">The API client to log in/out from and to get login data from.</param>
        public LoginStore(IPrefs prefs, MetaverseClient apiClient)
        {
            Prefs = prefs;

            ApiClient = apiClient;
            ApiClient.Account.LoggedIn += OnLoggedIn;
            ApiClient.Account.LoggedOut += OnLoggedOut;

            _aes = new AES();
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
                var response = 
                    !ApiClient.Account.UseCookieAuthentication ?
                        await ApiClient.Account.ValidateTokenAsync(AccessToken, RefreshToken) :
                        await ApiClient.Account.ValidateTokenAsync();
                
                if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    if (!response.Succeeded)
                    {
                        Debug.LogWarning("LoginStore Initialization failed: " + response.StatusCode);
                        _initializationRetries++;
                        if (_initializationRetries < 5)
                            await InitializeAsync();
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
                    Debug.LogWarning("Failed to initialize login store. Invalid access token: " + errorMessage);
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

            if (_plainTextAccessToken == ApiClient.Account.AccessToken &&
                _plainTextRefreshToken == ApiClient.Account.RefreshToken)
                return;

            var accessToken = ApiClient.Account.AccessToken;
            var refreshToken = ApiClient.Account.RefreshToken;

            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (ApiClient.Account.AccessToken != accessToken ||
                    ApiClient.Account.RefreshToken != refreshToken)
                {
                    // The access token and/or refresh token
                    // have changed since the login event was fired.
                    return;
                }
                
                AccessToken = ApiClient.Account.AccessToken;
                RefreshToken = ApiClient.Account.RefreshToken;
            });
        }

        private void OnLoggedOut(SystemUserDto user, AccountController.LogOutKind kind)
        {
            if (ApiClient.Account.UseCookieAuthentication)
                return;

            if (string.IsNullOrEmpty(_plainTextAccessToken) &&
                string.IsNullOrEmpty(_plainTextRefreshToken))
                return;

            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (ApiClient.Account.AccessToken != null ||
                    ApiClient.Account.RefreshToken != null)
                {
                    // The access token and/or refresh token
                    // have changed since the login event was fired.
                    return;
                }

                AccessToken = null;
                RefreshToken = null;
            });
        }

        private string GetObfuscatedRefreshTokenKey() => _aes.EncryptString(nameof(RefreshToken));

        private string GetObfuscatedAccessTokenKey() => _aes.EncryptString(nameof(AccessToken));
    }
}