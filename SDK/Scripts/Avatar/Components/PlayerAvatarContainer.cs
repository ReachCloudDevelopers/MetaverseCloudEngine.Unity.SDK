using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using MetaverseCloudEngine.Unity.Services.Abstract;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [Experimental]
    [HideMonoScript]
    [RequireComponent(typeof(Animator))]
    public partial class PlayerAvatarContainer : NetworkObjectBehaviour
    {
        [Serializable]
        public class AvatarContainerEvents
        {
            public UnityEvent onAvatarBeginLoad;
            public UnityEvent onAvatarEndLoad;
            public UnityEvent<GameObject> onAvatarModelLoaded;
            public UnityEvent<Animator> onAvatarSpawned;

            // ReSharper disable once StringLiteralTypo
            [FormerlySerializedAs("onAvatarDespawned")]
            public UnityEvent onAvatarDeSpawned;
            public UnityEvent onAvatarSpawnFailed;
        }

        [Tooltip("Set this value to false if you already have an avatar as a child of this game object. Otherwise an avatar will be loaded dynamically.")]
        [SerializeField, FormerlySerializedAs("autoLoadAvatar")] private bool avatarLoading = true;
        [Tooltip("Not required. This field allows you to specify a specific avatar URL of the avatar you want to load.")]
        [ShowIf(nameof(avatarLoading)), SerializeField] private string avatarUrl;
        [ShowIf(nameof(avatarLoading)), SerializeField] private AvatarType avatarUrlType;
        [SerializeField] private AvatarContainerEvents events = new();
        [Tooltip("Set to true if you want the avatar to automatically detect changes to the animator controller. This doesn't apply to 99% of use cases.")]
        [SerializeField] private bool autoDetectAnimatorChanges = true;

        private IPlayerGroupsService _playerGroups;
        private RuntimeAnimatorController _currentAnimatorController;
        private bool _hasBoundAnimator;
        private bool _networkReady;
        private RuntimeAnimatorController _originalAnimatorController;
        private PlayableGraph _animationGraph;
        private AnimationLayerMixerPlayable _playableMixer;
        private readonly List<AnimationClipPlayable> _activeAnimations = new();

        public AvatarContainerEvents Events => events;
        public Animator OwnAnimator { get; private set; }

        public Animator Avatar { get; private set; }
        public string AvatarUrl { get => avatarUrl; set => avatarUrl = value; }

        protected override void Awake()
        {
            OwnAnimator = GetComponent<Animator>();
            _originalAnimatorController = OwnAnimator.runtimeAnimatorController;

            var childAvatar = GetComponentsInChildren<Animator>().FirstOrDefault(x => x != OwnAnimator);
            if (childAvatar)
            {
                events.onAvatarModelLoaded?.Invoke(childAvatar.gameObject);
                SetCurrentAvatar(childAvatar, isPrefab: false);
            }

            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (_animationGraph.IsValid())
                _animationGraph.Destroy();
            
            base.OnDestroy();
            OnDestroyInternal();
        }

        private void OnDisable()
        {
            if (Avatar)
                ClearLoadedAvatar();
            CancelDownloadInternal();
            
            if (MetaverseProgram.ApiClient?.Account != null)
                MetaverseProgram.ApiClient.Account.UserAvatarUpdated -= OnLocalUserAvatarUpdated;
        }

        private void OnEnable()
        {
            if ((_networkReady || !NetworkObject) && avatarLoading && !Avatar)
                LoadAvatar();
            
            if (avatarLoading && NetworkObject)
                MetaverseProgram.OnInitialized(() => 
                    MetaverseProgram.ApiClient.Account.UserAvatarUpdated += OnLocalUserAvatarUpdated);
        }

        protected virtual void Reset()
        {
            autoDetectAnimatorChanges = false;
        }

        protected virtual void Update()
        {
            if (autoDetectAnimatorChanges && _hasBoundAnimator)
            {
                if (OwnAnimator.runtimeAnimatorController != _currentAnimatorController)
                    ReGenerateAnimationGraph();
            }
        }

        public override void OnNetworkReady(bool offline)
        {
            LoadAvatar();
            _networkReady = true;
        }

        protected override void RegisterNetworkRPCs()
        {
            base.RegisterNetworkRPCs();
            NetworkObject.RegisterRPC((short)NetworkRpcType.RequestLoadAvatar, RPC_OnRequestLoadAvatar, @override: false);
        }

        protected override void UnRegisterNetworkRPCs()
        {
            base.UnRegisterNetworkRPCs();
            NetworkObject.UnregisterRPC((short)NetworkRpcType.RequestLoadAvatar, RPC_OnRequestLoadAvatar);
        }

        protected override void OnMetaSpaceServicesRegistered()
        {
            _playerGroups = MetaSpace.GetService<IPlayerGroupsService>();
            _playerGroups.PlayerJoinedPlayerGroup += OnPlayerGroundJoined;
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            _playerGroups.PlayerJoinedPlayerGroup -= OnPlayerGroundJoined;
        }

        /// <summary>
        /// Clears the loaded avatar.
        /// </summary>
        public void ClearLoadedAvatar()
        {
            if (!avatarLoading)
                return;
            if (Avatar)
                Destroy(Avatar.gameObject);
        }

        /// <summary>
        /// Loads the avatar.
        /// </summary>
        public void LoadAvatar()
        {
            if (!this)
                return;
            if (!avatarLoading)
                return;
            if (!isActiveAndEnabled)
                return;
            if (MetaSpace.Instance && !MetaSpace.Instance.IsInitialized)
            {
                MetaSpace.Instance.Initialized += LoadAvatar;
                return;
            }
            if (string.IsNullOrEmpty(avatarUrl) && 
                _playerGroups is not null && 
                NetworkObject &&
                _playerGroups.TryGetPlayerPlayerGroup(NetworkObject.InputAuthorityID, out var group))
                TrySpawnAvatar(group);
            else if (!NetworkObject)
                TrySpawnAvatar();
        }

        /// <summary>
        /// If an <see cref="AvatarUrl"/> is specified, will equip that specific avatar.
        /// </summary>
        public void EquipAvatarUrl()
        {
            if (string.IsNullOrEmpty(avatarUrl))
                return;

            if (!MetaverseProgram.IsCoreApp)
            {
                MetaverseProgram.Logger.Log($"Avatar '{avatarUrl}' would have been equipped if this was the in the app.");
                return;
            }

            EquipAvatarInternal();
        }

        /// <summary>
        /// Plays an animation on this avatar.
        /// </summary>
        /// <param name="clip">The animation clip to play.</param>
        /// <param name="duration"></param>
        /// <param name="mask"></param>
        /// <param name="fadeInTime"></param>
        /// <param name="fadeOutTime"></param>
        public AnimationClipPlayable PlayAnimation(AnimationClip clip, float? duration = null, AvatarMask mask = null, float fadeInTime = 0, float fadeOutTime = 0)
        {
            if (!_animationGraph.IsValid())
            {
                // No animator has been bound yet.
                return default;
            }

            var pClip = AnimationClipPlayable.Create(_animationGraph, clip);
            pClip.SetApplyFootIK(false);

            var handle = _playableMixer.AddInput(pClip, 0, 1f);
            _activeAnimations.Add(pClip);

            if (duration != null)
                pClip.SetDuration(duration.Value);

            if (mask)
                _playableMixer.SetLayerMaskFromAvatarMask((uint)handle, mask);

            if (fadeInTime > 0 || fadeOutTime > 0)
            {
                _playableMixer.SetInputWeight(handle, 0);

                IEnumerator Fade()
                {
                    if (!pClip.IsValid())
                        yield break;

                    var f = (float)pClip.GetDuration();
                    var currentTime = (float)pClip.GetTime();
                    while (f - currentTime > fadeOutTime && pClip.IsValid())
                    {
                        currentTime = (float)pClip.GetTime();
                        var inT = Mathf.InverseLerp(0, fadeInTime, currentTime);
                        _playableMixer.SetInputWeight(pClip, inT);

                        if (currentTime > fadeInTime && float.IsInfinity(f))
                            yield break;

                        yield return null;
                    }

                    if (float.IsFinite(f))
                        StopAnimation(pClip, fadeOutTime);
                }

                StartCoroutine(Fade());
            }

            return pClip;
        }

        public void StopAllAnimations(float fadeOutTime = 0)
        {
            foreach (var anim in _activeAnimations)
                StopAnimation(anim, fadeOutTime);
        }

        public bool IsPlayingAnimation(AnimationClipPlayable handle) => _activeAnimations.Contains(handle) && !handle.IsDone();

        public void StopAnimation(AnimationClipPlayable handle, float fadeOutTime = 0)
        {
            if (!_activeAnimations.Remove(handle))
                return;

            if (fadeOutTime <= 0)
            {
                handle.Destroy();
                return;
            }

            StartCoroutine(Fade());
            return;

            IEnumerator Fade()
            {
                if (!handle.IsValid())
                    yield break;
                
                var currentTime = (float)handle.GetTime();
                var duration = double.IsFinite(handle.GetDuration()) ? (float)handle.GetDuration() : currentTime + fadeOutTime;
                var startTime = duration - fadeOutTime;

                while (currentTime < duration && handle.IsValid())
                {
                    currentTime = (float)handle.GetTime();
                    var outT = Mathf.InverseLerp(startTime, duration, currentTime);
                    _playableMixer.SetInputWeight(handle, 1f - outT);
                    yield return null;
                }

                if (handle.IsValid())
                    handle.Destroy();
            }
        }

        partial void EquipAvatarInternal();

        private void OnLocalUserAvatarUpdated(string url, AvatarType type)
        {
            if (!_networkReady || !NetworkObject || !NetworkObject.IsInputAuthority) 
                return;
            
            LoadAvatar();
            
            NetworkObject.InvokeRPC(
                (short)NetworkRpcType.RequestLoadAvatar,
                NetworkMessageReceivers.Others,
                new object[] { (byte)NetworkObject.GetNetworkObjectBehaviorID(this) });
        }

        private void RPC_OnRequestLoadAvatar(short procId, int senderId, object content)
        {
            if (!this || !isActiveAndEnabled) return;
            if (content is not object[] args || senderId != NetworkObject.InputAuthorityID)
                return;
            
            var behaviorId = (byte)args[0];
            if (behaviorId != NetworkObject.GetNetworkObjectBehaviorID(this))
                return;
            
            LoadAvatar();
        }

        private void OnPlayerGroundJoined(PlayerGroup playerGroup, int playerID)
        {
            if (NetworkObject && playerID == NetworkObject.InputAuthorityID && !Avatar)
                LoadAvatar();
        }

        private void TrySpawnAvatar(PlayerGroup playerGroup = null)
        {
            MetaverseProgram.OnInitialized(() =>
            {
                if (playerGroup == null || playerGroup.allowUserAvatars)
                {
                    if (!NetworkObject || !NetworkObject.Networking.IsOfflineMode)
                    {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
                        TrySpawnUserAvatarInternal(
                            () => { },
                            () => SpawnPlayerGroupAvatar(playerGroup));
                        return;
#endif
                    }
                }

                SpawnPlayerGroupAvatar(playerGroup);
            });
        }

        private void SpawnPlayerGroupAvatar(PlayerGroup playerGroup)
        {
            if (playerGroup == null)
            {
                SetCurrentAvatar(null);
                return;
            }

            var avatarID = NetworkObject ? NetworkObject.InputAuthorityID : 0;
            var avatarToSpawn = playerGroup.GetAvatar(avatarID);
            if (avatarToSpawn == null)
            {
                SetCurrentAvatar(null);
                return;
            }

            var avatar = avatarToSpawn.avatarPrefab;
            var controller = OwnAnimator.runtimeAnimatorController;
            controller
                .OverrideAnimations(controller as AnimatorOverrideController)
                .OverrideAnimations(MetaSpace.Instance.PlayerSpawnOptions.OverrideAnimator)
                .OverrideAnimations(_playerGroups?.CurrentPlayerGroup?.overrideAnimator)
                .OverrideAnimations(avatarToSpawn.overrideAnimator);
            avatarToSpawn.avatarPrefab.runtimeAnimatorController = controller;

            SetCurrentAvatar(avatar);
        }

        private void SetCurrentAvatar(Animator avatar, bool isPrefab = true)
        {
            if (!this || !isActiveAndEnabled)
            {
                OnDeactivateBeforeSpawningAvatar(avatar, isPrefab);
                return;
            }
            
            StartCoroutine(SetCurrentAvatarCoroutine(avatar, isPrefab));
        }

        private IEnumerator SetCurrentAvatarCoroutine(Animator newAvatar, bool isPrefab = true)
        {
            if (!this || !isActiveAndEnabled)
            {
                OnDeactivateBeforeSpawningAvatar(newAvatar, isPrefab);
                yield break;
            }

            if (Avatar)
            {
                if (!avatarLoading)
                {
                    MetaverseProgram.Logger.LogError("Cannot load another avatar after one is already set because 'Avatar Loading' is false.");
                    events.onAvatarEndLoad?.Invoke();
                    yield break;
                }

                MetaverseProgram.Logger.Log("Despawning avatar.");
                var av = Avatar;
                Avatar = null;
                Destroy(av.gameObject);
                OnAvatarDeSpawned();
            }

            try
            {
                if (newAvatar)
                {
                    var inst = isPrefab ? Instantiate(newAvatar.gameObject) : newAvatar.gameObject;
                    inst.transform.SetParent(transform, false);

                    Avatar = isPrefab ? inst.GetComponent<Animator>() : newAvatar;

                    if (Avatar)
                    {
                        if (isPrefab)
                            events.onAvatarModelLoaded?.Invoke(Avatar.gameObject);

                        UpdateAnimatorController();

                        Avatar.gameObject.OnDestroy(x =>
                        {
                            if (!this)
                                return;

                            if (x == null)
                                return;

                            if (Avatar != null && Avatar.gameObject == x.gameObject)
                                OnAvatarDeSpawned();
                        });

                        yield return null;
                        
                        inst.ResetTransform();

                        OwnAnimator.Rebind();

                        if (OwnAnimator.runtimeAnimatorController)
                            ReGenerateAnimationGraph();

                        if (Avatar)
                        {
                            events.onAvatarSpawned?.Invoke(Avatar);
                        }
                    }
                }
                else
                    Avatar = null;
            }
            finally
            {
                events.onAvatarEndLoad?.Invoke();
            }
        }

        private void OnDeactivateBeforeSpawningAvatar(Animator avatar, bool isPrefab)
        {
            if (avatar && !isPrefab && avatarLoading)
            {
                Destroy(avatar.gameObject);
                OnAvatarDeSpawned();
            }
            else
            {
                events.onAvatarEndLoad?.Invoke();
            }
        }

        private void UpdateAnimatorController()
        {
            if (Avatar.avatar)
                OwnAnimator.avatar = Avatar.avatar;

            RuntimeAnimatorController finalAnimator;
            if (!Avatar.runtimeAnimatorController)
            {
                if (MetaSpace && MetaSpace.PlayerSpawnOptions.OverrideAnimator)
                    finalAnimator = MetaSpace.PlayerSpawnOptions.OverrideAnimator;
                else finalAnimator = _originalAnimatorController;
            }
            else finalAnimator = Avatar.runtimeAnimatorController;

            if (finalAnimator)
                OwnAnimator.runtimeAnimatorController = finalAnimator;

            Avatar.runtimeAnimatorController = null;
        }

        private void ReGenerateAnimationGraph()
        {
            DestroyCurrentAnimationGraph();

            if (!OwnAnimator.runtimeAnimatorController)
            {
                // Nothing to bind to.
                _hasBoundAnimator = false;
                _currentAnimatorController = null;
                return;
            }
            
            _animationGraph = PlayableGraph.Create();
            _playableMixer = AnimationLayerMixerPlayable.Create(_animationGraph, 2);

            var controller = AnimatorControllerPlayable.Create(_animationGraph, OwnAnimator.runtimeAnimatorController);
            controller.SetLayerWeight(0, 1);

            _playableMixer.AddInput(controller, 0, 1f);

            var output = AnimationPlayableOutput.Create(_animationGraph, string.Empty, OwnAnimator);
            output.SetWeight(1f);
            output.SetSourcePlayable(_playableMixer);
            _animationGraph.Play();

            _hasBoundAnimator = true;
            _currentAnimatorController = OwnAnimator.runtimeAnimatorController;
        }

        private void DestroyCurrentAnimationGraph()
        {
            if (!_animationGraph.IsValid())
                return;
            _animationGraph.Stop();
            _animationGraph.Destroy();
            _currentAnimatorController = null;
            _hasBoundAnimator = false;
        }

        private void OnAvatarDeSpawned()
        {
            DestroyCurrentAnimationGraph();
            events.onAvatarDeSpawned?.Invoke();
        }

        partial void TrySpawnUserAvatarInternal(Action onSpawned, Action onFailed);

        partial void OnDestroyInternal();

        private void CancelDownload()
        {
            CancelDownloadInternal();
        }

        partial void CancelDownloadInternal();
    }
}