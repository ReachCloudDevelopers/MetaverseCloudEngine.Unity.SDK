using System;
using MetaverseCloudEngine.ApiClient.Controllers;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Components;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [Experimental]
    [HideMonoScript]
    public class PlayerAvatarHelper : MetaverseBehaviour
    {
        [Header("Has Avatar")]
        public UnityEvent onHasAvatar;
        public UnityEvent onHasNoAvatar;

        [Header("Avatar URL")]
        public UnityEvent onAvatarUrlUpdated;
        [FormerlySerializedAs("onBeginUpdate")]
        public UnityEvent onBeginUpdateUrl;
        [FormerlySerializedAs("onEndUpdate")]
        public UnityEvent onEndUpdateUrl;

        private void OnEnable()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                onEndUpdateUrl?.Invoke();
                
                CheckHasAvatarUrl();
                
                MetaverseProgram.ApiClient.Account.LoggedIn += OnLoggedIn;
                MetaverseProgram.ApiClient.Account.LoggedOut += OnLoggedOut;
                MetaverseProgram.ApiClient.Account.UserAvatarUpdated += OnUserAvatarUpdated;

#if METAVERSE_CLOUD_ENGINE_INTERNAL
                MetaverseProgram.RuntimeServices.InternalAvatarManager.AvatarUrlUpdated += OnAvatarUpdated;
                MetaverseProgram.RuntimeServices.InternalAvatarManager.BeginUpdate += OnAvatarBeginUpdate;
                MetaverseProgram.RuntimeServices.InternalAvatarManager.EndUpdate += OnAvatarEndUpdate;
#endif
            });
        }

        private void OnDisable()
        {
            if (MetaverseProgram.ApiClient != null)
            {
                MetaverseProgram.ApiClient.Account.LoggedIn -= OnLoggedIn;
                MetaverseProgram.ApiClient.Account.LoggedOut -= OnLoggedOut;
                MetaverseProgram.ApiClient.Account.UserAvatarUpdated -= OnUserAvatarUpdated;
            }

#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (MetaverseProgram.RuntimeServices?.InternalAvatarManager != null)
            {
                MetaverseProgram.RuntimeServices.InternalAvatarManager.AvatarUrlUpdated -= OnAvatarUpdated;
                MetaverseProgram.RuntimeServices.InternalAvatarManager.BeginUpdate -= OnAvatarBeginUpdate;
                MetaverseProgram.RuntimeServices.InternalAvatarManager.EndUpdate -= OnAvatarEndUpdate;
            }
#endif
        }

        private void OnLoggedIn(SystemUserDto user, AccountController.LogInKind kind) => MetaverseDispatcher.AtEndOfFrame(CheckHasAvatarUrl);

        private void OnLoggedOut(SystemUserDto user, AccountController.LogOutKind kind) => MetaverseDispatcher.AtEndOfFrame(CheckHasAvatarUrl);

        private void OnUserAvatarUpdated(string url, AvatarType type)
        {
            MetaverseDispatcher.AtEndOfFrame(CheckHasAvatarUrl);
        }

        public void SetReadyPlayerMeAvatarUrl(string url)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (!this || !isActiveAndEnabled) return;
            MetaverseProgram.RuntimeServices.InternalAvatarManager.UpdateAvatarUrl(url, AvatarType.ReadyPlayerMe);
#endif
        }

        public void SetAvaturnAvatarUrl(string url)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (!this || !isActiveAndEnabled) return;
            MetaverseProgram.RuntimeServices.InternalAvatarManager.UpdateAvatarUrl(url, AvatarType.Avaturn);
#endif
        }

        public void SetMetaAvatarUrl(string url)
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (!this || !isActiveAndEnabled) return;
            MetaverseProgram.RuntimeServices.InternalAvatarManager.UpdateAvatarUrl(url, AvatarType.Prefab);
#endif
        }

        public void SetMetaAvatarUrl(PrefabDto prefab)
        {
            if (!this || !isActiveAndEnabled) return;
            if (prefab.IsAvatar)
                SetMetaAvatarUrl("avatar://" + prefab.Id);
        }

        public void SetMetaAvatarUrl(object o)
        {
            if (o is PrefabDto p)
                SetMetaAvatarUrl(p);
        }

        public void RefreshAllContainers()
        {
            var containers = FindObjectsOfType<PlayerAvatarContainer>();
            foreach (var container in containers)
                container.LoadAvatar();
        }

        private void OnAvatarBeginUpdate()
        {
            if (!this || !isActiveAndEnabled) return;
            onBeginUpdateUrl?.Invoke();
        }

        private void OnAvatarEndUpdate()
        {
            if (!this || !isActiveAndEnabled) return;
            onEndUpdateUrl?.Invoke();
            CheckHasAvatarUrl();
        }

        private void OnAvatarUpdated(string url)
        {
            if (!this || !isActiveAndEnabled) return;
            onAvatarUrlUpdated?.Invoke();
            CheckHasAvatarUrl();
        }

        private void CheckHasAvatarUrl()
        {
            if (!this || !isActiveAndEnabled) return;
            var url = MetaverseProgram.ApiClient.Account.CurrentUser?.AvatarUrl;
            if (!string.IsNullOrEmpty(url))
                onHasAvatar?.Invoke();
            else onHasNoAvatar?.Invoke();
        }
    }
}
