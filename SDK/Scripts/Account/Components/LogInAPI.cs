/* Copyright (C) 2024 Reach Cloud - All Rights Reserved */

using System;
using Cysharp.Threading.Tasks;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This is similar to the <see cref="Account.Components.AccountAPI"/> component, but it
    /// provides functionality more specific to logging in and out and
    /// handling the behaviour of this game object based on the login state.
    /// </summary>
    [HideMonoScript]
    [Experimental]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Account/Log In API")]
    public class LogInAPI : MetaverseBehaviour
    {
        /// <summary>
        /// The behavior to perform on this object.
        /// </summary>
        public enum LocalBehaviour
        {
            /// <summary>
            /// Does nothing.
            /// </summary>
            Nothing,
            /// <summary>
            /// Deactivates this game object.
            /// </summary>
            Deactivate,
            /// <summary>
            /// Activates this game object.
            /// </summary>
            Activate,
        }

        /// <summary>
        /// Events that are called when logging in and out.
        /// </summary>
        [Serializable]
        public class Events
        {
            [Tooltip("The event that's invoked when logged in.")]
            public UnityEvent onLoggedIn;
            [Tooltip("The event that's invoked when logged out.")]
            public UnityEvent onLoggedOut;
        }

        [Tooltip("Events that are called when logged in/out.")]
        public Events events;
        [Header("Behaviour on this game object")]
        [Tooltip("The behavior that should occur when logging in.")]
        public LocalBehaviour loggedInBehaviour;
        [Tooltip("The behavior that should occur when logging out.")]
        public LocalBehaviour loggedOutBehaviour;

        protected override void Awake()
        {
            LogOutBehaviour();

            base.Awake();
        }
        
        protected override void OnMetaverseBehaviourInitialize(MetaverseRuntimeServices services)
        {
            if (MetaverseProgram.ApiClient.Account.IsLoggedIn)
                LogInBehaviour();
            MetaverseProgram.ApiClient.Account.LoggedIn += OnLoggedIn;
            MetaverseProgram.ApiClient.Account.LoggedOut += OnLoggedOut;
        }

        protected override void OnMetaverseBehaviourUnInitialize()
        {
            MetaverseProgram.ApiClient.Account.LoggedIn -= OnLoggedIn;
            MetaverseProgram.ApiClient.Account.LoggedOut -= OnLoggedOut;
        }

        /// <summary>
        /// Logs out from the currently logged in account.
        /// </summary>
        public void LogOut()
        {
            if (MetaverseProgram.IsQuitting)
                return;
            UniTask.Void(async () => await MetaverseProgram.ApiClient.Account.LogOutAsync());
        }

        private void OnLoggedIn(SystemUserDto user, AccountController.LogInKind kind) => MetaverseDispatcher.AtEndOfFrame(LogInBehaviour);

        private void OnLoggedOut(SystemUserDto user, AccountController.LogOutKind kind) => MetaverseDispatcher.AtEndOfFrame(LogOutBehaviour);

        private void LogInBehaviour()
        {
            if (!this) return;
            InvokeBehaviour(loggedInBehaviour);
            events.onLoggedIn?.Invoke();
        }

        private void LogOutBehaviour()
        {
            if (!this) return;
            InvokeBehaviour(loggedOutBehaviour);
            events.onLoggedOut?.Invoke();
        }

        private void InvokeBehaviour(LocalBehaviour beh)
        {
            if (!this) return;
            switch (beh)
            {
                case LocalBehaviour.Activate:
                    gameObject.SetActive(true);
                    break;
                case LocalBehaviour.Deactivate:
                    gameObject.SetActive(false);
                    break;
                case LocalBehaviour.Nothing:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(beh), beh, null);
            }
        }
    }
}
