#pragma warning disable CS0067

using System;
using System.Collections.Generic;
using System.Linq;

using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using MetaverseCloudEngine.Unity.Services.Abstract;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Impl
{
    public partial class MetaSpaceNetworkingService : IMetaSpaceNetworkingService
    {
        public event MetaSpaceNetworkPlayerDelegate PlayerJoined;
        public event MetaSpaceNetworkPlayerDelegate PlayerLeft;
        public event Action Ready;
        public event Action UnReady;
        public event Action HostChanged;

        private readonly Dictionary<short, List<MetaSpaceNetworkEventDelegate>> _eventHandlers = new();

        private readonly IMetaSpaceNetworkOptions _networking;
        private readonly IMetaSpaceResources _resources;
        private readonly IDebugLogger _logger;

        public MetaSpaceNetworkingService(IMetaSpaceNetworkOptions networking, IMetaSpaceResources resources, IDebugLogger logger = null)
        {
            _networking = networking;
            _resources = resources;
            _logger = logger;

            RegisterAllSceneObjects();
            CtorInternal();
        }

        public bool IsOfflineMode
        {
            get
            {
                var value = true;
                IsOfflineModeInternal(ref value);
                return value;
            }
        }

        public bool Private
        {
            get
            {
                var value = true;
                IsPrivateModeInternal(ref value);
                return value;
            }
        }

        public int LocalPlayerID
        {
            get
            {
                if (IsOfflineMode)
                    return 1;
                var value = -1;
                GetLocalPlayerIDInternal(ref value);
                return value;
            }
        }

        public bool IsHost
        {
            get
            {
                if (IsOfflineMode)
                    return true;
                var value = false;
                GetIsServerInternal(ref value);
                return value;
            }
        }

        public int PlayerCount
        {
            get
            {
                if (IsOfflineMode)
                    return 1;
                var value = 1;
                GetPlayerCountInternal(ref value);
                return value;
            }
        }

        private bool _canJoin = true;

        public bool CanJoin
        {
            get
            {
                var value = _canJoin;
                GetCanJoinInternal(ref value);
                return value;
            }
            set => SetCanJoinInternal(_canJoin = value);
        }

        public bool IsReady
        {
            get
            {
                var value = true;
                IsReadyInternal(ref value);
                return value;
            }
        }

        public string InstanceID {
            get {
                var value = "Offline";
                GetInstanceIDInternal(ref value);
                return value;
            }
        }

        public int HostID {
            get {
                var value = LocalPlayerID;
                GetHostIDInternal(ref value);
                return value;
            }
        }

        public void Initialize()
        {
            if (IsOfflineMode)
            {
                Ready?.Invoke();
                _logger?.Log("Currently in offline mode.");
            }

            InitializeInternal();

            Application.focusChanged += OnFocusChanged;
        }

        public void Dispose()
        {
            DisposeInternal();

            Application.focusChanged -= OnFocusChanged;
        }

        public void InvokeEvent(short eventID, NetworkMessageReceivers receivers, bool buffered = false, object content = null)
        {
            if (IsOfflineMode)
            {
                if (receivers != NetworkMessageReceivers.Others)
                    InvokeHandlers(eventID, LocalPlayerID, content);
            }
            else
            {
                InvokeEventInternal(eventID, receivers, content, buffered);
            }
        }

        public void InvokeEvent(short eventID, int playerID, object content = null)
        {
            if (IsOfflineMode)
            {
                if (playerID == LocalPlayerID)
                    InvokeHandlers(eventID, LocalPlayerID, content);
            }
            else
            {
                InvokeEventInternal(eventID, playerID, content);
            }
        }

        public void RemoveEventFromBuffer(short eventID, object content = null)
        {
            RemoveEventFromBufferInternal(eventID, content);
        }

        public void AddEventHandler(short eventID, MetaSpaceNetworkEventDelegate handler)
        {
            if (!_eventHandlers.TryGetValue(eventID, out var handlers))
                _eventHandlers[eventID] = handlers = new List<MetaSpaceNetworkEventDelegate>();
            handlers.Add(handler);
        }

        public void RemoveEventHandler(short eventID, MetaSpaceNetworkEventDelegate handler)
        {
            if (!_eventHandlers.TryGetValue(eventID, out var handlers))
                return;

            handlers.Remove(handler);

            if (handlers.Count == 0)
                _eventHandlers.Remove(eventID);
        }

        [Obsolete]
        public GameObject SpawnGameObject(GameObject prefab, Vector3 position, Quaternion rotation, bool serverOwned, byte channel = 0, params object[] instantiationData)
        {
            GameObject output = null;
            // Assumes a synchronous call
            SpawnGameObject(prefab, o => output = o.GameObject, position, rotation, serverOwned, channel: channel, instantiationData: instantiationData);
            return output;
        }

        public void SpawnGameObject(GameObject prefab, Action<NetworkSpawnedObject> onSpawned, Vector3 position, Quaternion rotation, bool serverOwned, byte channel = 0, params object[] instantiationData)
        {
            if (IsOfflineMode)
            {
                var networkSpawnedObject = new NetworkSpawnedObject(UnityEngine.Object.Instantiate(prefab, position, rotation));
                onSpawned?.Invoke(networkSpawnedObject);
                if (networkSpawnedObject.IsStale)
                    UnityEngine.Object.Destroy(networkSpawnedObject.GameObject);
                return;
            }
            
            SpawnGameObjectInternal(prefab, position, rotation, serverOwned, onSpawned, channel: channel, instantiationData: instantiationData);
        }

        public void GetPlayerName(int playerID, Action<string> onName, Action onFailed)
        {
            if (IsOfflineMode) onName?.Invoke($"Offline Mode Player {playerID}");
            else GetPlayerNameInternal(playerID, onName, onFailed);
        }

        public int[] GetPlayerIDs()
        {
            var val = Array.Empty<int>();
            GetPlayerIDsInternal(ref val);
            return val;
        }

        public void CheckJoinable()
        {
            if (IsHost && _networking.TotalMaxPlayers > 0)
                CanJoin = PlayerCount < _networking.TotalMaxPlayers;
        }

        public bool IsHostPlayer(int playerID)
        {
            var value = true;
            IsHostPlayerInternal(playerID, ref value);
            return value;
        }

        public void RoundTrip(Action then)
        {
            if (IsOfflineMode)
            {
                then?.Invoke();
                return;
            }

            RoundTripInternal(then);
        }
        
        private static void RegisterAllSceneObjects()
        {
            var allSceneObjects = MVUtils.FindObjectsOfTypeNonPrefabPooled<NetworkObject>(true)
                .Where(x => x.SceneID != -1)
                .ToArray();

            foreach (var obj in allSceneObjects)
                obj.Register();
        }

        private void InvokeHandlers(short eventID, int sendingPlayerID, object content = null)
        {
            if (!_eventHandlers.TryGetValue(eventID, out var handlers)) return;
            foreach (var handler in handlers)
                handler?.Invoke(eventID, sendingPlayerID, content);
        }

        private void OnFocusChanged(bool focused)
        {
            if (focused)
                CheckJoinable();
        }

        partial void CtorInternal();

        partial void InitializeInternal();

        partial void DisposeInternal();

        partial void IsReadyInternal(ref bool value);

        partial void IsOfflineModeInternal(ref bool value);

        partial void IsPrivateModeInternal(ref bool value);

        partial void InvokeEventInternal(short eventId, NetworkMessageReceivers targets, object content, bool buffered);

        partial void InvokeEventInternal(short eventId, int playerID, object content);

        partial void SpawnGameObjectInternal(GameObject prefab, Vector3 position, Quaternion rotation, bool serverOwned, Action<NetworkSpawnedObject> onSpawned, byte channel = 0, params object[] instantiationData);

        partial void GetLocalPlayerIDInternal(ref int value);

        partial void GetIsServerInternal(ref bool value);

        partial void GetCanJoinInternal(ref bool value);

        partial void SetCanJoinInternal(bool value);

        partial void GetPlayerNameInternal(int playerID, Action<string> onName, Action onFailed);

        partial void GetPlayerCountInternal(ref int value);

        partial void GetPlayerIDsInternal(ref int[] playerIDs);

        partial void GetInstanceIDInternal(ref string instanceID);

        partial void IsHostPlayerInternal(int playerID, ref bool value);

        partial void GetHostIDInternal(ref int value);
        
        partial void RemoveEventFromBufferInternal(short eventID, object content = null);
        
        partial void RoundTripInternal(Action then);
    }
}