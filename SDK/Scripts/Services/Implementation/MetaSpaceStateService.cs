using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Services.Options;
using System;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Enumerations;

namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    public class MetaSpaceStateService : IMetaSpaceStateService
    {
        public event Action MetaSpaceStarted;
        public event Action MetaSpaceEnded;

        private bool _isStarted;
        private bool _hasMasterClientStarted;
        private bool _isInitialized;

        private readonly IMetaSpaceNetworkingService _networkingService;
        private readonly IPlayerGroupsService _playerGroupService;
        private readonly IMetaSpaceStateOptions _gameStateOptions;
        private readonly IDebugLogger _logger;

        public MetaSpaceStateService(
            IMetaSpaceNetworkingService networkingService,
            IPlayerGroupsService playerGroupService,
            IMetaSpaceStateOptions gameStateOptions,
            IDebugLogger logger = null)
        {
            _networkingService = networkingService;
            _playerGroupService = playerGroupService;
            _gameStateOptions = gameStateOptions;
            _logger = logger;

            networkingService.AddEventHandler((short)NetworkEventType.HostSayingGameStarted, OnGameStarted);
            networkingService.AddEventHandler((short)NetworkEventType.HostSayingGameEnded, OnGameEnded);
            networkingService.HostChanged += OnHostChanged;
            networkingService.PlayerJoined += OnPlayerJoined;
            playerGroupService.PlayerJoinedPlayerGroup += OnPlayerGroupJoined;
            playerGroupService.PlayerLeftPlayerGroup += OnPlayerGroupLeft;
        }

        public bool IsStarted {
            get => _isStarted;
            private set {

                if (_isStarted == value) return;
                _isStarted = value;

                if (value)
                {
                    _logger?.Log("Meta space started.");
                    MetaSpaceStarted?.Invoke();
                }
                else
                {
                    _logger?.Log("Meta space ended.");
                    MetaSpaceEnded?.Invoke();
                }

                if (_networkingService.IsHost)
                {
                    NetworkEventType eventId = value ? NetworkEventType.HostSayingGameStarted : NetworkEventType.HostSayingGameEnded;
                    _networkingService.InvokeEvent((short)eventId, NetworkMessageReceivers.Others);
                }
            }
        }

        public bool CanStartGame => _playerGroupService.MeetsMinimumRequirements;

        public void Initialize()
        {
            _isInitialized = true;
            if (_hasMasterClientStarted)
                IsStarted = true;
            else if (_gameStateOptions.AutoStart)
                TryStartGame();
        }

        public void Dispose()
        {
            _networkingService.RemoveEventHandler((short)NetworkEventType.HostSayingGameStarted, OnGameStarted);
            _networkingService.RemoveEventHandler((short)NetworkEventType.HostSayingGameEnded, OnGameEnded);
            _networkingService.HostChanged -= OnHostChanged;
            _networkingService.PlayerJoined -= OnPlayerJoined;
            _playerGroupService.PlayerJoinedPlayerGroup -= OnPlayerGroupJoined;
            _playerGroupService.PlayerLeftPlayerGroup -= OnPlayerGroupLeft;
        }

        public bool TryStartGame()
        {
            if (!_isInitialized) return false;
            if (IsStarted) return true;
            if (!CanStartGame) return false;
            IsStarted = true;
            return true;
        }

        public bool TryEndGame()
        {
            if (!IsStarted) return true;
            if (CanStartGame) return false;
            if (!_networkingService.IsHost) return false;
            IsStarted = false;
            return true;
        }

        private void OnHostChanged()
        {
            // When the host migrates before auto-starting the game,
            // we want to make sure to start it.
            if (_gameStateOptions.AutoStart && _networkingService.IsHost && !_isStarted)
                TryStartGame();
        }

        private void OnGameStarted(short eventId, int sendingPlayerID, object content)
        {
            _hasMasterClientStarted = true;
            if (_networkingService.IsHost) return;
            if (!_isInitialized) return;
            IsStarted = true;
        }

        private void OnGameEnded(short eventId, int sendingPlayerID, object content)
        {
            _hasMasterClientStarted = false;
            if (_networkingService.IsHost) return;
            IsStarted = false;
        }

        private void OnPlayerGroupJoined(PlayerGroup playerGroup, int playerID)
        {
            if (_gameStateOptions.AutoStart)
                TryStartGame();
        }

        private void OnPlayerGroupLeft(PlayerGroup playerGroup, int playerID)
        {
            if (_gameStateOptions.AutoEnd)
                TryEndGame();
        }

        private void OnPlayerJoined(int playerID)
        {
            if (IsStarted && _networkingService.IsHost)
                _networkingService.InvokeEvent((short)NetworkEventType.HostSayingGameStarted, playerID);
        }
    }
}
