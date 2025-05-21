using System;
using System.Linq;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Services.Abstract;
using MetaverseCloudEngine.Unity.Services.Options;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using MetaverseCloudEngine.Unity.Networking.Components;
using UnityEngine;
using UnityEngine.Events;


namespace MetaverseCloudEngine.Unity.Services.Implementation
{
    public class PlayerSpawnService : IPlayerSpawnService
    {
        private readonly IMetaSpaceNetworkingService _networking;
        private readonly IMetaSpaceStateService _metaSpaceState;
        private readonly IPlayerGroupsService _playerGroups;
        private readonly IPlayerSpawnOptions _spawnOptions;
        private readonly IMetaSpaceResources _metaSpaceResources;
        private readonly IDebugLogger _logger;
        private bool _isSpawned;

        public event Action<GameObject> LocalPlayerSpawned;
        public event Action LocalPlayerDeSpawned;
        public Transform LocalPlayerSpawnPoint { get; private set; }

        public PlayerSpawnService(
            IMetaSpaceNetworkingService networking,
            IMetaSpaceStateService metaSpaceState,
            IPlayerGroupsService playerGroups,
            IPlayerSpawnOptions spawnOptions,
            IMetaSpaceResources metaSpaceResources = null,
            IDebugLogger logger = null)
        {
            _networking = networking;
            _metaSpaceState = metaSpaceState;
            _playerGroups = playerGroups;
            _spawnOptions = spawnOptions;
            _metaSpaceResources = metaSpaceResources;
            _logger = logger;

            networking.AddEventHandler((short)NetworkEventType.HostSayingAnotherClientWantsToSpawnYourPlayer, OnSpawnPlayerEvent);
            networking.AddEventHandler((short)NetworkEventType.HostSayingAnotherClientWantsToDeSpawnYourPlayer, OnDeSpawnPlayerEvent);
            metaSpaceState.MetaSpaceStarted += OnGameStarted;
            playerGroups.PlayerJoinedPlayerGroup += OnPlayerJoinedPlayerGroup;
        }

        public GameObject SpawnedPlayerObject { get; private set; }

        public void Initialize()
        {
        }

        public void Dispose()
        {
            _networking.RemoveEventHandler((short)NetworkEventType.HostSayingAnotherClientWantsToSpawnYourPlayer, OnSpawnPlayerEvent);
            _networking.RemoveEventHandler((short)NetworkEventType.HostSayingAnotherClientWantsToDeSpawnYourPlayer, OnDeSpawnPlayerEvent);
            _metaSpaceState.MetaSpaceStarted -= OnGameStarted;
            _playerGroups.PlayerJoinedPlayerGroup -= OnPlayerJoinedPlayerGroup;
            _playerGroups.PlayerLeftPlayerGroup -= OnPlayerLeftPlayerGroup;
        }

        private void OnGameStarted()
        {
            if (_spawnOptions.AutoSpawnPlayer && !SpawnedPlayerObject && _playerGroups.CurrentPlayerGroup != null)
                TrySpawnPlayer(_networking.LocalPlayerID);
        }

        private void OnPlayerLeftPlayerGroup(PlayerGroup playerGroup, int playerID)
        {
            if (_networking.LocalPlayerID != playerID)
                return;

            if (SpawnedPlayerObject)
                TryDeSpawnPlayer(playerID);
        }

        private void OnPlayerJoinedPlayerGroup(PlayerGroup playerGroup, int playerID)
        {
            if (!_metaSpaceState.IsStarted)
                return;
            if (_networking.LocalPlayerID != playerID)
                return;

            if (SpawnedPlayerObject)
                TryDeSpawnPlayer(playerID);
            if (_spawnOptions.AutoSpawnPlayer)
                TrySpawnPlayer(playerID);
        }

        private void OnSpawnPlayerEvent(short eventId, int sendingPlayerID, object content)
        {
            TrySpawnPlayer(_networking.LocalPlayerID);
        }

        private void OnDeSpawnPlayerEvent(short eventId, int sendingPlayerID, object content)
        {
            TryDeSpawnPlayer(_networking.LocalPlayerID);
        }

        public void TrySpawnPlayer(int playerID, Transform spawnPoint = null)
        {
            var playerControllerPrefab = GetPlayerControllerPrefab(playerID);
            if (!playerControllerPrefab)
            {
                _logger?.Log($"Cannot spawn player '{playerID}'. " +
                             "Either the player is not in a player " +
                             "group, or there is no player controller " +
                             "defined. This might be fine if a " +
                             "custom player controller is being used.");
                return;
            }

            if (_networking.LocalPlayerID != playerID)
            {
                if (_networking.IsHost)
                {
                    _networking.InvokeEvent((short)NetworkEventType.HostSayingAnotherClientWantsToSpawnYourPlayer, playerID);
                    return;
                }

                _logger?.LogError("You cannot spawn the player unless you are the server or the local client himself.");
                return;
            }
            
            if (!spawnPoint)
            {
                var newSpawnPoint = LocalPlayerSpawnPoint;
                if (newSpawnPoint)
                {
                    if (!newSpawnPoint.gameObject.activeInHierarchy ||
                        newSpawnPoint.TryGetComponent(out MetaSpaceSpawnPoint point) &&
                        (!point.IsPlayerGroupAllowed(_playerGroups.CurrentPlayerGroup) || !point.isActiveAndEnabled))
                        newSpawnPoint = null;
                }
                spawnPoint = newSpawnPoint;
            }

            const float spawnDelay = 0.1f;
            if (SpawnedPlayerObject is not null)
            {
                _networking.RoundTrip(() =>
                {
                    if (!TryDeSpawnPlayer(playerID)) return;
                    MetaverseDispatcher.WaitForSeconds(spawnDelay, () => SpawnPlayerNow(playerID, spawnPoint, playerControllerPrefab));
                });
                return;
            }

            MetaverseDispatcher.WaitForSeconds(spawnDelay, () => SpawnPlayerNow(playerID, spawnPoint, playerControllerPrefab));
        }

        public bool TryDeSpawnPlayer(int playerID)
        {
            if (_networking.LocalPlayerID != playerID)
            {
                if (_networking.IsHost)
                {
                    _networking.InvokeEvent((short)NetworkEventType.HostSayingAnotherClientWantsToDeSpawnYourPlayer, playerID);
                    return true;
                }

                _logger?.LogError(
                    "You cannot de-spawn the player unless you are the server or the local client himself.");
                return false;
            }

            _isSpawned = false;

            if (!SpawnedPlayerObject)
                return true;

            var oldPlayerObj = SpawnedPlayerObject;
            LocalPlayerSpawnPoint = null;
            SpawnedPlayerObject = null;
            LocalPlayerDeSpawned?.Invoke();
            _logger?.Log($"Player {oldPlayerObj.name} de-spawned.");
            UnityEngine.Object.Destroy(oldPlayerObj);
            MetaverseCursorAPI.UnlockCursor();
            return true;
        }

        private void SpawnPlayerNow(int playerID, Transform targetSpawnPoint, GameObject prefab)
        {
            if (_isSpawned)
                return;

            _isSpawned = true;

            if (!targetSpawnPoint)
                _playerGroups.CurrentPlayerGroup.FindSpawnPoint(
                    playerID,
                    point => SpawnAtPoint(prefab, point),
                    () =>
                    {
                        _isSpawned = false;
                        _logger.LogWarning("Finding spawn point for player failed.");
                    });
            else
                SpawnAtPoint(prefab, targetSpawnPoint);
        }

        private GameObject GetPlayerControllerPrefab(int playerID)
        {
            if (!_playerGroups.TryGetPlayerPlayerGroup(playerID, out var playerGroup))
                return null;

            if (playerGroup.playerPrefab)
                return playerGroup.playerPrefab;

            if (_spawnOptions.DefaultPlayerPrefab)
                return _spawnOptions.DefaultPlayerPrefab;

            return _metaSpaceResources?.GetEmbeddedPrefabWithName(MetaverseConstants.Resources.DefaultPlayer.GetFileName());
        }

        private void SpawnAtPoint(GameObject playerControllerPrefab, Transform spawnPoint)
        {
            if (playerControllerPrefab.GetComponent<NetworkObject>())
            {
                _networking.SpawnGameObject(
                    playerControllerPrefab, no =>
                    {
                        OnPlayerObject(no.GameObject);
                    },
                    spawnPoint.position, spawnPoint.rotation, false);
                return;
            }

            OnPlayerObject(UnityEngine.Object.Instantiate(playerControllerPrefab, spawnPoint.position, spawnPoint.rotation));

            return;

            void OnPlayerObject(GameObject obj)
            {
                SpawnedPlayerObject = obj;
                
                if (!SpawnedPlayerObject)
                {
                    _logger?.LogWarning("Failed to spawn player object because the spawner returned nothing.");
                    return;
                }

                RegisterEvents();

                spawnPoint.SendMessage("OnPlayerSpawned", SpawnedPlayerObject, SendMessageOptions.DontRequireReceiver);

                var spawners = SpawnedPlayerObject
                    .GetMetaPrefabSpawners(requireLoadOnStart: false)
                    .Where(x => (x.IsLoading || x.spawnOnStart) && x.isActiveAndEnabled)
                    .ToArray();

                if (spawners.Length > 0 && spawners.Any(x => !x.SpawnedPrefab))
                    WaitForPlayerSpawnersToFinish(spawners, OnFinished);
                else
                    OnFinished();

                _logger?.Log($"Spawned Player: {SpawnedPlayerObject.name}");
            }

            void OnFinished()
            {
                LocalPlayerSpawnPoint = spawnPoint;
                LocalPlayerSpawned?.Invoke(SpawnedPlayerObject);

                SpawnAddonsAsChildOfPlayer();
            }
        }

        private static void WaitForPlayerSpawnersToFinish(MetaPrefabSpawner[] playerObjectSpawners, Action onFinished)
        {
            foreach (var spawner in playerObjectSpawners)
            {
                void OnFinishedLoading()
                {
                    spawner.events.onFinishedLoading.RemoveListener(OnFinishedLoading);
                    if (playerObjectSpawners.All(x => !x.IsLoading))
                        onFinished?.Invoke();
                }
                spawner.events.onFinishedLoading.AddListener(OnFinishedLoading);
            }
        }

        private void RegisterEvents()
        {
            var originallySpawnedPlayer = SpawnedPlayerObject;
            var unityEventCallbacks = SpawnedPlayerObject.AddComponent<UnityEventCallbacks>();
            unityEventCallbacks.events = new UnityEventCallbacks.Events { onDestroy = new UnityEvent() };
            unityEventCallbacks.events.onDestroy.AddListener(() =>
            {
                if (SpawnedPlayerObject == originallySpawnedPlayer)
                    LocalPlayerDeSpawned?.Invoke();
            });
            unityEventCallbacks.hideFlags = HideFlags.HideInInspector;

            const float nanPositionCheckIntervalSeconds = 5f;
            var teleported = false;
            var tr = originallySpawnedPlayer.transform;
            MetaverseDispatcher.WaitForSeconds(nanPositionCheckIntervalSeconds, TeleportIfNaNPosition);
            return;
            void TeleportIfNaNPosition()
            {
                if (teleported || !originallySpawnedPlayer || originallySpawnedPlayer != SpawnedPlayerObject)
                    return;
                if (tr.position.IsNaN())
                {
                    MetaverseProgram.Logger.Log(
                        "Player position is NaN or too far away. Teleporting to spawn point.");
                    TrySpawnPlayer(_networking.LocalPlayerID, LocalPlayerSpawnPoint);
                    teleported = true;
                    return;
                }
                MetaverseDispatcher.WaitForSeconds(nanPositionCheckIntervalSeconds, TeleportIfNaNPosition);
            }
        }

        private void SpawnAddonsAsChildOfPlayer()
        {
            var addons = _spawnOptions.Addons?.ToList() ?? new List<GameObject>();
            var group = _playerGroups.CurrentPlayerGroup;
            if (group is { playerAddons: not null })
                addons.AddRange(group.playerAddons);

            foreach (var addon in addons.Where(addon => addon))
            {
                var networkAddon = addon.GetComponent<NetworkObject>();
                if (networkAddon && 
                    !SpawnedPlayerObject.GetComponent<NetworkObject>() && 
                    !SpawnedPlayerObject.GetComponent<NetworkTransform>())
                {
                    _logger.LogError("A Networked addon '" + addon.name + "' failed to spawn because the player does not have a network object on it!");
                    continue;
                }

                if (networkAddon)
                {
                    _networking.SpawnGameObject(addon, no =>
                    {
                        ParentInst(no.GameObject);
                        
                    }, Vector3.zero, Quaternion.identity, false);
                }
                else
                {
                    ParentInst(UnityEngine.Object.Instantiate(addon, Vector3.zero, Quaternion.identity));
                }

                return;
                
                void ParentInst(GameObject inst)
                {
                    if (!SpawnedPlayerObject)
                    {
                        UnityEngine.Object.Destroy(inst);
                        return;
                    }
                    if (!inst) return;
                    inst.transform.SetParent(SpawnedPlayerObject.transform, false);
                    inst.transform.ResetLocalTransform(scale: false);
                }
            }
        }
    }
}