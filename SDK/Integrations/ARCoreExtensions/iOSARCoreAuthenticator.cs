#if METAVERSE_CLOUD_ENGINE_INTERNAL && (UNITY_IOS || UNITY_EDITOR)

using System;
using Firebase;
using Firebase.Auth;
using Google.XR.ARCoreExtensions;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Object = UnityEngine.Object;

namespace MetaverseCloudEngine.Unity.ARCoreExtensions
{
    public static class iOSARCoreAuthenticator
    {
        /// <summary>
        /// Authenticates the user with Firebase and passes the ID token to ARCore Extensions.
        /// </summary>
        /// <param name="onSuccess">Callback invoked upon successful authentication.</param>
        /// <param name="onFailed">Callback invoked upon authentication failure with an error message.</param>
        public static void AuthenticateAsync(Action onSuccess, Action<string> onFailed)
        {
            // Start the asynchronous authentication process without blocking the main thread
            AuthenticateInternal(onSuccess, onFailed).Forget();
        }

        /// <summary>
        /// Internal asynchronous method to handle authentication using UniTask.
        /// </summary>
        private static async UniTaskVoid AuthenticateInternal(Action onSuccess, Action<string> onFailed)
        {
            try
            {
                // Initialize Firebase
                var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask();
                if (dependencyStatus != DependencyStatus.Available)
                {
                    onFailed?.Invoke($"Firebase dependencies not available: {dependencyStatus}");
                    return;
                }

                // Get FirebaseAuth instance
                FirebaseAuth auth = FirebaseAuth.DefaultInstance;

                // Sign in anonymously
                FirebaseUser user = await SignInAnonymouslyAsync(auth, onFailed);
                if (user == null)
                {
                    // Error callback already invoked in SignInAnonymouslyAsync
                    return;
                }

                // Retrieve the ID token
                string idToken = await GetIdTokenAsync(user, onFailed);
                if (string.IsNullOrEmpty(idToken))
                {
                    // Error callback already invoked in GetIdTokenAsync
                    return;
                }

                // Pass the token to ARCore Extensions
                bool tokenPassed = PassTokenToARCore(idToken, onFailed);
                if (!tokenPassed)
                {
                    // Error callback already invoked in PassTokenToARCore
                    return;
                }

                // Start token refresh loop
                RefreshTokenAsync(user).Forget();

                // Invoke success callback
                onSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                // Invoke failure callback with exception message
                onFailed?.Invoke($"Authentication failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Signs in the user anonymously with Firebase Auth.
        /// </summary>
        private static async UniTask<FirebaseUser> SignInAnonymouslyAsync(FirebaseAuth auth, Action<string> onFailed)
        {
            try
            {
                var signInTask = auth.SignInAnonymouslyAsync();
                await signInTask.AsUniTask();

                if (signInTask.IsCanceled)
                {
                    onFailed?.Invoke("Anonymous sign-in was canceled.");
                    return null;
                }
                if (signInTask.IsFaulted)
                {
                    onFailed?.Invoke($"Anonymous sign-in encountered an error: {signInTask.Exception?.Message}");
                    return null;
                }

                FirebaseUser user = signInTask.Result.User;
                Debug.Log($"User signed in anonymously: {user.UserId}");
                return user;
            }
            catch (Exception ex)
            {
                onFailed?.Invoke($"Exception during anonymous sign-in: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves the Firebase ID token for the authenticated user.
        /// </summary>
        private static async UniTask<string> GetIdTokenAsync(FirebaseUser user, Action<string> onFailed)
        {
            try
            {
                var tokenTask = user.TokenAsync(true);
                await tokenTask.AsUniTask();

                if (tokenTask.IsCanceled)
                {
                    onFailed?.Invoke("Token retrieval was canceled.");
                    return null;
                }
                if (tokenTask.IsFaulted)
                {
                    onFailed?.Invoke($"Token retrieval encountered an error: {tokenTask.Exception?.Message}");
                    return null;
                }

                string idToken = tokenTask.Result;
                Debug.Log("Obtained Firebase ID Token.");
                return idToken;
            }
            catch (Exception ex)
            {
                onFailed?.Invoke($"Exception during token retrieval: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Passes the Firebase ID token to ARCore Extensions.
        /// </summary>
        private static bool PassTokenToARCore(string idToken, Action<string> onFailed)
        {
            try
            {
                var arAnchorManager = Object.FindObjectOfType<ARAnchorManager>();
                if (arAnchorManager == null)
                {
                    onFailed?.Invoke("ARAnchorManager not found in the scene.");
                    return false;
                }
                
                arAnchorManager.SetAuthToken(idToken);
                return true;
            }
            catch (Exception ex)
            {
                onFailed?.Invoke($"Exception while passing token to ARCore Extensions: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refreshes the Firebase ID token periodically.
        /// </summary>
        /// <param name="user">The authenticated Firebase user.</param>
        /// <param name="onFailed">Callback invoked upon token refresh failure.</param>
        private static async UniTaskVoid RefreshTokenAsync(FirebaseUser user)
        {
            while (true)
            {
                // Wait for 55 minutes before refreshing the token
                await UniTask.Delay(TimeSpan.FromMinutes(55));

                try
                {
                    string idToken = await GetIdTokenAsync(user, null);
                    if (!string.IsNullOrEmpty(idToken))
                    {
                        bool tokenPassed = PassTokenToARCore(idToken, null);
                        if (!tokenPassed)
                        {
                            // Handle token pass failure if necessary
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception during token refresh: {ex.Message}");
                }
            }
        }
    }
}

#endif
