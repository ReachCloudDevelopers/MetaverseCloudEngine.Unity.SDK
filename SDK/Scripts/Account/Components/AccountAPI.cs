/* Copyright (C) 2024 Reach Cloud - All Rights Reserved */

using System;

using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Components;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Account.Components
{
    /// <summary>
    /// This component provides access to the account API, exposing it to the
    /// Unity inspector.
    /// </summary>
    [HideMonoScript]
    [Experimental]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Account/Account API")]
    public partial class AccountAPI : MetaverseBehaviour
    {
        [Tooltip("Invoked when the user logs in, outputs the username.")]
        public UnityEvent<string> onUserName;
        [Tooltip("Invoked when the user logs in, outputs the raw email (should be used with caution).")]
        public UnityEvent<string> onUserEmail;
        [Tooltip("Invoked when the user logs in, outputs a protected email address.")]
        public UnityEvent<string> onProtectedUserEmail;

        protected override void OnMetaverseBehaviourInitialize(MetaverseRuntimeServices services)
        {
            // If the user is already logged in, we want to invoke the login
            // behaviour.
            if (MetaverseProgram.ApiClient.Account.IsLoggedIn)
                LogInBehaviour();

            // Listen to the login event.
            MetaverseProgram.ApiClient.Account.LoggedIn += OnLoggedIn;
        }

        protected override void OnMetaverseBehaviourUnInitialize()
        {
            MetaverseProgram.ApiClient.Account.LoggedIn -= OnLoggedIn;
        }

        private void OnLoggedIn(SystemUserDto user, AccountController.LogInKind kind)
        {
            MetaverseDispatcher.AtEndOfFrame(LogInBehaviour);
        }

        private void LogInBehaviour()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                if (!this || MetaverseProgram.ApiClient == null || MetaverseProgram.ApiClient.Account == null)
                    return;
                
                var user = MetaverseProgram.ApiClient.Account.CurrentUser;
                if (user == null)
                    // The user is not logged in: exit.
                {
                    OnLogInBehaviourInternal(null);
                    return;
                }

                // Invoke the events.
                onUserName?.Invoke(user.UserName);
                if (!string.IsNullOrEmpty(user.Email))
                {
                    onUserEmail?.Invoke(user.Email);
                    onProtectedUserEmail?.Invoke($"{user.Email[0]}****{user.Email[user.Email.IndexOf("@", StringComparison.Ordinal) - 1]}{user.Email[user.Email.IndexOf("@", StringComparison.Ordinal)..]}");
                }
                else
                {
                    onUserEmail?.Invoke("");
                    onProtectedUserEmail?.Invoke("");
                }
                OnLogInBehaviourInternal(user);
            });
        }

        partial void OnLogInBehaviourInternal(SystemUserDto user);
    }
}