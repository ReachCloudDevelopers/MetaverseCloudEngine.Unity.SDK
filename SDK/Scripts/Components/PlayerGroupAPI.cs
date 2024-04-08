using System;
using System.Linq;

using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [DeclareFoldoutGroup("Group Status")]
    [DeclareFoldoutGroup("Networking")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Gameplay/Player Groups/Player Groups API")]
    [HideMonoScript]
    public class PlayerGroupAPI : MetaSpaceBehaviour
    {
        public enum PlayerGroupStringOutputValue
        {
            Name,
            Identifier
        }

        [Serializable]
        public class PlayerGroupEvents
        {
            [InfoBox("Specify either the group name or the identifier as the string argument passed to the Unity Event.")]
            public PlayerGroupStringOutputValue outputType;
            [Space]
            public UnityEvent<string> onPlayerGroupJoined;
            public UnityEvent<string> onPlayerGroupLeft;
        }

        [Serializable]
        public class LocalPlayerGroupEvents : PlayerGroupEvents
        {
            [Tooltip("Invoked when the local player is not in any of the specified groups.")]
            public UnityEvent onNotInAnyGroup;
        }

        [Header("Filter (Optional)")]
        [PlayerGroupId] public string[] specificGroups;

        [Header("Events")]
        [Group("Networking")][LabelText("Local")] public LocalPlayerGroupEvents localOnlyCallbacks;
        [Group("Networking")][LabelText("Remote")] public PlayerGroupEvents remoteOnlyCallbacks;
        [Group("Networking")][LabelText("Local & Remote")] public PlayerGroupEvents callbacks;
        
        [Space]
        [Group("Group Status")] public UnityEvent<int> onPlayerGroupCountChanged;
        [Group("Group Status")] public UnityEvent onPlayerGroupNotFilled;
        [Group("Group Status")] public UnityEvent onPlayerGroupFilled;

        private IPlayerGroupsService _playerGroupService;
        private IMetaSpaceNetworkingService _networkService;
        private bool _dirty;

        private void Start() { /* for enabled/disabled */ }

        private void OnEnable()
        {
            if (_dirty)
            {
                CheckInitialState();
            }
        }

        protected override void OnMetaSpaceServicesRegistered()
        {
            _playerGroupService = MetaSpace.GetService<IPlayerGroupsService>();
            _playerGroupService.PlayerJoinedPlayerGroup += OnPlayerJoinedGroup;
            _playerGroupService.PlayerLeftPlayerGroup += OnPlayerLeftGroup;
            _networkService = MetaSpace.GetService<IMetaSpaceNetworkingService>();

            _dirty = true;
            CheckInitialState();
        }

        private void CheckInitialState()
        {
            if (!this) return;
            if (!_dirty) return;
            if (!isActiveAndEnabled) return;

            _dirty = false;

            System.Collections.Generic.IDictionary<int, PlayerGroup> groups = _playerGroupService.GetPlayerGroups();
            bool didCallEvents = false;
            foreach (System.Collections.Generic.KeyValuePair<int, PlayerGroup> pg in groups)
            {
                if (!CanListenForGroup(pg.Value))
                    continue;

                OnPlayerJoinedGroup(pg.Value, pg.Key);
                if (pg.Key == _networkService.LocalPlayerID)
                    didCallEvents = true;
            }

            if (!didCallEvents)
            {
                localOnlyCallbacks?.onNotInAnyGroup?.Invoke();
                onPlayerGroupCountChanged?.Invoke(_playerGroupService.GetTotalNumPlayersInGroups(specificGroups));
                CheckFillStatus();
            }
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            if (!this) return;
            if (_playerGroupService == null) return;
            _playerGroupService.PlayerJoinedPlayerGroup -= OnPlayerJoinedGroup;
            _playerGroupService.PlayerLeftPlayerGroup -= OnPlayerLeftGroup;
        }

        public void JoinGroup(string identifier)
        {
            MetaSpace.OnReady(_ => MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                if (!this) return;
                if (!isActiveAndEnabled) return;
                _playerGroupService?.TryJoinPlayerGroup(identifier);
            }));
        }

        [Obsolete("Please use '" + nameof(JoinNextAvailable) + "' instead.")]
        public void JoinRandom()
        {
            JoinNextAvailable();
        }

        public void JoinNextAvailable()
        {
            MetaSpace.OnReady(_ => MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                if (!this) return;
                if (!isActiveAndEnabled) return;
                _playerGroupService?.TryJoinNextAvailablePlayerGroup();
            }));
        }

        private void OnPlayerLeftGroup(PlayerGroup playerGroup, int playerID)
        {
            if (!this) return;
            if (!isActiveAndEnabled)
            {
                _dirty = true;
                return;
            }

            if (!CanListenForGroup(playerGroup)) 
                return;
            
            if (playerID == _networkService.LocalPlayerID)
            {
                localOnlyCallbacks?.onPlayerGroupLeft?.Invoke(localOnlyCallbacks.outputType == PlayerGroupStringOutputValue.Identifier ? playerGroup.displayName : playerGroup.identifier);
                localOnlyCallbacks?.onNotInAnyGroup?.Invoke();
            }
            else
            {
                remoteOnlyCallbacks?.onPlayerGroupLeft?.Invoke(remoteOnlyCallbacks.outputType == PlayerGroupStringOutputValue.Name ? playerGroup.displayName : playerGroup.identifier);
            }

            callbacks?.onPlayerGroupLeft?.Invoke(callbacks.outputType == PlayerGroupStringOutputValue.Name ? playerGroup.displayName : playerGroup.identifier);
            onPlayerGroupCountChanged?.Invoke(_playerGroupService.GetTotalNumPlayersInGroups(specificGroups));

            CheckFillStatus();
        }

        private void OnPlayerJoinedGroup(PlayerGroup playerGroup, int playerID)
        {
            if (!this) return;
            if (!isActiveAndEnabled)
            {
                _dirty = true;
                return;
            }

            if (!CanListenForGroup(playerGroup)) 
                return;
            
            if (playerID == _networkService.LocalPlayerID)
            {
                localOnlyCallbacks?.onPlayerGroupJoined?.Invoke(localOnlyCallbacks.outputType == PlayerGroupStringOutputValue.Identifier ? playerGroup.displayName : playerGroup.identifier);
            }
            else
            {
                remoteOnlyCallbacks?.onPlayerGroupJoined?.Invoke(remoteOnlyCallbacks.outputType == PlayerGroupStringOutputValue.Name ? playerGroup.displayName : playerGroup.identifier);
            }

            callbacks?.onPlayerGroupJoined?.Invoke(callbacks.outputType == PlayerGroupStringOutputValue.Name ? playerGroup.displayName : playerGroup.identifier);
            onPlayerGroupCountChanged?.Invoke(_playerGroupService.GetTotalNumPlayersInGroups(specificGroups));

            CheckFillStatus();
        }

        private bool CanListenForGroup(PlayerGroup playerGroup)
        {
            return specificGroups.Length == 0 || specificGroups.Any(x => x == playerGroup?.identifier);
        }

        private void CheckFillStatus()
        {
            var anyNotFull = specificGroups.Length > 0 
                ? specificGroups.Any(x => !_playerGroupService.IsPlayerGroupFull(x)) 
                : MetaSpace.PlayerGroupOptions.PlayerGroups.Any(x => !_playerGroupService.IsPlayerGroupFull(x.identifier));
            if (anyNotFull) onPlayerGroupNotFilled?.Invoke();
            else onPlayerGroupFilled?.Invoke();
        }
    }
}