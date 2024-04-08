using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Services.Abstract;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Services.Components
{
    // TODO: Refactor to PlayerSpawnAPI component.
    public class PlayerSpawnHelper : MetaSpaceBehaviour
    {
        public UnityEvent<GameObject> onLocalPlayerSpawned;
        public UnityEvent onLocalPlayerDeSpawned;

        private IPlayerSpawnService _playerSpawnService;
        private IMetaSpaceNetworkingService _metaSpaceNetworkingService;

        public IPlayerSpawnService PlayerSpawnService {
            get {
                if (_playerSpawnService == null)
                {
                    if (MetaSpace.Instance)
                        _playerSpawnService = MetaSpace.Instance.GetService<IPlayerSpawnService>();
                }

                return _playerSpawnService;
            }
        }

        public IMetaSpaceNetworkingService MetaSpaceNetworkingService {
            get {
                if (_metaSpaceNetworkingService == null)
                {
                    if (MetaSpace.Instance)
                        _metaSpaceNetworkingService = MetaSpace.Instance.GetService<IMetaSpaceNetworkingService>();
                }

                return _metaSpaceNetworkingService;
            }
        }

        protected override void OnMetaSpaceServicesRegistered()
        {
            PlayerSpawnService.LocalPlayerSpawned += OnLocalPlayerSpawned;
            PlayerSpawnService.LocalPlayerDeSpawned += OnLocalPlayerDeSpawned;

            if (PlayerSpawnService.SpawnedPlayerObject is not null)
                OnLocalPlayerSpawned(PlayerSpawnService.SpawnedPlayerObject);
            else
                OnLocalPlayerDeSpawned();
        }

        protected override void OnMetaSpaceBehaviourDestroyed()
        {
            if (PlayerSpawnService is null) return;
            PlayerSpawnService.LocalPlayerSpawned -= OnLocalPlayerSpawned;
            PlayerSpawnService.LocalPlayerDeSpawned -= OnLocalPlayerDeSpawned;
        }

        public void SpawnLocalPlayer()
        {
            if (MetaSpaceNetworkingService != null)
                PlayerSpawnService?.TrySpawnPlayer(MetaSpaceNetworkingService.LocalPlayerID);
        }

        public void DeSpawnLocalPlayer()
        {
            if (MetaSpaceNetworkingService != null)
                PlayerSpawnService?.TryDeSpawnPlayer(MetaSpaceNetworkingService.LocalPlayerID);
        }

        private void OnLocalPlayerSpawned(GameObject pObj) => onLocalPlayerSpawned?.Invoke(pObj);

        private void OnLocalPlayerDeSpawned() => onLocalPlayerDeSpawned?.Invoke();
    }
}
