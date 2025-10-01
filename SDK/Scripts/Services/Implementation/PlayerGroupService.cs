using System.Linq;
using System.Collections.Generic;

using MetaverseCloudEngine.Unity.Networking.Enumerations;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Services.Options;

using System;

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    public class PlayerGroupService : IPlayerGroupsService
    {
        public event PlayerGroupPlayerEvent PlayerJoinedPlayerGroup;
        public event PlayerGroupPlayerEvent PlayerLeftPlayerGroup;

        private readonly IMetaSpaceNetworkingService _networking;
        private readonly IPlayerGroupOptions _playerGroupOptions;
        private readonly IDebugLogger _logger;

        private bool _isRequestingPlayerGroups;
        private bool _didInitializeTeams;

        private readonly Dictionary<int, PlayerGroup> _playerPlayerGroups = new();
        private readonly Dictionary<string, List<int>> _playerGroupPlayers = new();
        private readonly Dictionary<string, PlayerGroup> _playerGroupLookup = new();

        public PlayerGroupService(
            IMetaSpaceNetworkingService networking,
            IPlayerGroupOptions playerGroupOptions,
            IDebugLogger logger = null)
        {
            _networking = networking;
            _playerGroupOptions = playerGroupOptions;
            _logger = logger;

            _networking.UnReady += UnInitializeTeams;
            _networking.PlayerLeft += OnPlayerLeft;
            _networking.HostChanged += OnHostChanegd;

            _networking.AddEventHandler((short)NetworkEventType.ClientSayingToSetHisPlayerGroupOnYourComputer, RPC_NotifySetPlayerGroup);
            _networking.AddEventHandler((short)NetworkEventType.JoiningClientWantsAllPlayerGroupData, RPC_RequestPlayerGroupData);
            _networking.AddEventHandler((short)NetworkEventType.SomeoneSendingYouAllThePlayerGroupData, RPC_HostSendingPlayerGroups);
        }

        public PlayerGroup CurrentPlayerGroup { get; private set; }
        public bool MeetsMinimumRequirements => _playerGroupOptions.PlayerGroups.All(x => GetPlayerGroupPlayerCount(x.identifier) >= x.minPlayers);

        public void Initialize()
        {
            RequestPlayerGroups();
        }

        public void Dispose()
        {
            _networking.RemoveEventHandler((short)NetworkEventType.ClientSayingToSetHisPlayerGroupOnYourComputer, RPC_NotifySetPlayerGroup);
            _networking.RemoveEventHandler((short)NetworkEventType.JoiningClientWantsAllPlayerGroupData, RPC_RequestPlayerGroupData);
            _networking.RemoveEventHandler((short)NetworkEventType.SomeoneSendingYouAllThePlayerGroupData, RPC_HostSendingPlayerGroups);

            _networking.HostChanged -= OnHostChanegd;
            _networking.PlayerLeft -= OnPlayerLeft;
            _networking.UnReady -= UnInitializeTeams;
        }

        public bool TryJoinNextAvailablePlayerGroup()
        {
            PlayerGroup desiredPlayerGroup = _playerGroupOptions.PlayerGroupSelectionMode switch
            {
                PlayerGroupSelectionMode.EvenDistribution =>
                    _playerGroupOptions.PlayerGroups
                        .OrderBy(x => GetPlayerGroupPlayerCount(x.identifier))
                        .FirstOrDefault(),

                PlayerGroupSelectionMode.FirstAvailable =>
                    _playerGroupOptions.PlayerGroups
                        .FirstOrDefault(x => !IsPlayerGroupFull(x.identifier)),
                _ => null
            };

            if (desiredPlayerGroup == null)
                return false;

            _networking.InvokeEvent(
                (short)NetworkEventType.ClientSayingToSetHisPlayerGroupOnYourComputer,
                NetworkMessageReceivers.All,
                false,
                new object[] { _networking.LocalPlayerID, desiredPlayerGroup.identifier });

            return true;
        }

        public bool TrySetPlayerPlayerGroup(int playerID, string id)
        {
            PlayerGroup playerGroup = GetPlayerGroup(id);
            if (playerGroup == null || IsPlayerGroupFull(id))
                return false;

            if (IsInPlayerGroup(playerID, id))
                return true;

            _networking.InvokeEvent(
                (short)NetworkEventType.ClientSayingToSetHisPlayerGroupOnYourComputer,
                NetworkMessageReceivers.All,
                false,
                new object[] { playerID, id });

            return true;
        }

        public bool TryJoinPlayerGroup(string id)
        {
            return TrySetPlayerPlayerGroup(_networking.LocalPlayerID, id);
        }

        public bool TryGetPlayerPlayerGroup(int playerID, out PlayerGroup playerGroup)
        {
            return _playerPlayerGroups.TryGetValue(playerID, out playerGroup);
        }

        public PlayerGroup GetPlayerGroup(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (!_playerGroupLookup.TryGetValue(id, out PlayerGroup playerGroup))
                _playerGroupLookup[id] =
                    playerGroup = _playerGroupOptions.PlayerGroups.FirstOrDefault(x => x.identifier == id);

            return playerGroup;
        }

        public IDictionary<int, PlayerGroup> GetPlayerGroups() => _playerPlayerGroups ?? new Dictionary<int, PlayerGroup>();

        public bool PlayerGroupExists(string id)
        {
            return GetPlayerGroup(id) != null;
        }

        public int GetPlayerGroupPlayerCount(string id)
        {
            return !PlayerGroupExists(id) ? 0 : _playerPlayerGroups.Count(x => x.Value?.identifier == id);
        }

        public bool IsPlayerGroupFull(string id)
        {
            if (!PlayerGroupExists(id))
                return true;

            if (!_playerGroupPlayers.TryGetValue(id, out List<int> playerList) || playerList == null)
                return false;

            PlayerGroup pg = GetPlayerGroup(id);
            if (pg.maxPlayers <= 0)
                return false;

            return playerList.Count >= pg.maxPlayers;
        }

        public bool IsInPlayerGroup(int playerID, string id)
        {
            return _playerGroupPlayers.TryGetValue(id, out List<int> players) && players != null && players.Contains(playerID);
        }

        public int GetTotalNumPlayersInGroups(params string[] groupIds)
        {
            if (groupIds == null || groupIds.Length == 0)
                return _playerPlayerGroups.Count;
            return _playerPlayerGroups.Count(x => groupIds.Contains(x.Value.identifier));
        }

        private void OnPlayerLeft(int playerID)
        {
            UnsetPlayerGroupInternal(playerID);

            if (_isRequestingPlayerGroups && _networking.PlayerCount <= 1)
                InitializeTeams(new Dictionary<int, string>());
        }

        private void OnHostChanegd()
        {
            if (!_isRequestingPlayerGroups)
                return;

            if (!_networking.IsHost)
            {
                // We're not the host but the host changed
                // while we sent the first request. If this
                // happens we want to re-request the player
                // groups.
                RequestPlayerGroups();
                return;
            }

            if (_networking.PlayerCount > 1)
            {
                // If we get to this point we've actually messed up
                // really bad because now we're the host
                // but we haven't actually retreived
                // the player groups yet...

                // In this case we just want to request
                // the player groups from all the users
                // but we'll only accept the first response.
                RequestPlayerGroups(hostOnly: false);
                return;
            }

            // Otherwise just initialize the teams with nothing.
            InitializeTeams(new Dictionary<int, string>());
        }

        private void UnInitializeTeams()
        {
            CurrentPlayerGroup = null;

            _didInitializeTeams = false;
            _playerPlayerGroups?.Clear();
            _playerGroupLookup?.Clear();
            _playerGroupPlayers?.Clear();
        }

        private void RequestPlayerGroups(bool hostOnly = true)
        {
            if (_didInitializeTeams)
                return;

            if (_networking.IsHost && _networking.PlayerCount <= 1)
            {
                InitializeTeams(new Dictionary<int, string>());
                return;
            }

            _isRequestingPlayerGroups = true;
            _networking.InvokeEvent((short)NetworkEventType.JoiningClientWantsAllPlayerGroupData, hostOnly ? NetworkMessageReceivers.Host : NetworkMessageReceivers.All);
        }

        private void InitializeTeams(Dictionary<int, string> playerGroups)
        {
            if (_didInitializeTeams)
                return;

            _isRequestingPlayerGroups = false;
            _didInitializeTeams = true;

            int[] validPlayers = _networking.GetPlayerIDs();

            foreach (KeyValuePair<int, string> playerGroup in playerGroups)
            {
                if (playerGroup.Key == _networking.LocalPlayerID)
                    continue;

                if (!validPlayers.Contains(playerGroup.Key))
                    continue;

                string playerGroupID = playerGroup.Value;

                if (!string.IsNullOrEmpty(playerGroupID) && !IsInPlayerGroup(playerGroup.Key, playerGroupID))
                    SetPlayerGroupInternal(GetPlayerGroup(playerGroupID), playerGroup.Key);
            }

            if (_playerGroupOptions.AutoSelectPlayerGroup && CurrentPlayerGroup == null)
                TryJoinNextAvailablePlayerGroup();
        }

        private void UnsetPlayerGroupInternal(int playerID)
        {
            if (!TryGetPlayerPlayerGroup(playerID, out PlayerGroup playerGroup) ||
                !_playerGroupPlayers.TryGetValue(playerGroup.identifier, out List<int> players) ||
                players == null)
                return;

            if (!_playerPlayerGroups.Remove(playerID))
                return;

            if (!players.Remove(playerID))
                return;

            if (playerID == _networking.LocalPlayerID)
                CurrentPlayerGroup = null;

            PlayerLeftPlayerGroup?.Invoke(playerGroup, playerID);

            _logger?.Log($"[PLAYER_GROUP_SERVICE] Player {playerID} left player group {playerGroup.identifier}.");
        }

        private void SetPlayerGroupInternal(PlayerGroup playerGroup, int playerID)
        {
            UnsetPlayerGroupInternal(playerID);

            if (playerID == _networking.LocalPlayerID)
            {
                CurrentPlayerGroup = playerGroup;
            }

            _playerPlayerGroups[playerID] = playerGroup;

            if (!_playerGroupPlayers.TryGetValue(playerGroup.identifier, out List<int> playerList))
                _playerGroupPlayers[playerGroup.identifier] = playerList = new List<int>();

            playerList.Add(playerID);

            _logger?.Log($"[PLAYER_GROUP_SERVICE] Player {playerID} joined player group {playerGroup.identifier}.");

            PlayerJoinedPlayerGroup?.Invoke(playerGroup, playerID);
        }

        private void RPC_RequestPlayerGroupData(short eventID, int sendingPlayerID, object content)
        {
            _networking.InvokeEvent(
                (short)NetworkEventType.SomeoneSendingYouAllThePlayerGroupData,
                sendingPlayerID,
                content: _playerPlayerGroups.ToDictionary(x => x.Key, y => y.Value.identifier));
        }

        private void RPC_HostSendingPlayerGroups(short eventID, int sendingPlayerID, object content)
        {
            if (_didInitializeTeams || !_isRequestingPlayerGroups)
                return;

            if ((!_networking.IsHost && !_networking.IsHostPlayer(sendingPlayerID)))
                return;

            if (content is not Dictionary<int, string> playerGroupData)
                return;

            InitializeTeams(playerGroupData);
        }

        private void RPC_NotifySetPlayerGroup(short eventID, int sendingPlayerID, object content)
        {
            if (content is not object[] { Length: 2 } data)
                return;

            int playerID = (int)data[0];
            string id = (string)data[1];
            PlayerGroup group = GetPlayerGroup(id);
            if (group == null)
            {
                MetaverseProgram.Logger.LogError("Failed to find group with ID: " + id);
                return;
            }

            SetPlayerGroupInternal(GetPlayerGroup(id), playerID);
        }
    }
}