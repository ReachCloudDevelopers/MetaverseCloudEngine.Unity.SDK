using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    /// <summary>
    /// Represents a method that handles a remote procedure call (RPC) event.
    /// </summary>
    /// <param name="procedureID">The unique identifier of the procedure being called by the RPC.</param>
    /// <param name="content">The content of the RPC.</param>
    public delegate void RpcEventDelegate(short procedureID, int senderID, object content);

    /// <summary>
    /// The NetworkObject class represents a networked object in a Unity scene that can be synchronized over the network.
    /// It allows for the creation, ownership, and control of networked objects, as well as the communication of events 
    /// between networked objects and players. 
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecutionOrder.PostInitialization)]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Networking/Network Object")]
    [HelpURL("https://reach-cloud.gitbook.io/reach-explorer-documentation/docs/development-guide/unity-engine-sdk/components/networking/network-object")]
    [HierarchyIcon("Profiler.NetworkMessages@2x")]
    [HideMonoScript]
    public partial class NetworkObject : MetaSpaceBehaviour
    {
        #region Classes / Structs

        /// <summary>
        /// Represents a queued remote procedure call (RPC) that is waiting to be sent over the network.
        /// </summary>
        private class QueuedRpcCall
        {
            /// <summary>
            /// The content of the RPC.
            /// </summary>
            public readonly object Content;
            /// <summary>
            /// The receivers of the RPC.
            /// </summary>
            public readonly NetworkMessageReceivers Receivers;
            /// <summary>
            /// The unique identifier of the procedure being called by the RPC.
            /// </summary>
            public readonly short ProcedureID;
            /// <summary>
            /// A flag indicating whether the RPC should be buffered.
            /// </summary>
            public readonly bool Buffered;
            /// <summary>
            /// The unique identifier of the player the RPC is being sent to.
            /// </summary>
            public readonly int? PlayerID;

            /// <summary>
            /// Initializes a new instance of the <see cref="QueuedRpcCall"/> class.
            /// </summary>
            /// <param name="eventID">The unique identifier of the procedure being called by the RPC.</param>
            /// <param name="data">The content of the RPC.</param>
            private QueuedRpcCall(short eventID, object data)
            {
                ProcedureID = eventID;
                Content = data;
            }

            public QueuedRpcCall(short procedureID, int playerID, object content) : this(procedureID, content)
            {
                PlayerID = playerID;
            }

            public QueuedRpcCall(short procedureID, NetworkMessageReceivers receivers, object content, bool buffered) :
                this(procedureID, content)
            {
                Buffered = buffered;
                Receivers = receivers;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Invoked when the <see cref="NetworkObject"/> is initialized.
        /// </summary>
        public event Action Initialized;

        #endregion

        #region Inspector

        [Tooltip("A flag indicating whether the NetworkObject should be destroyed when the player who created it leaves the scene.")]
        [SerializeField] private bool destroyWhenCreatorLeaves;
        [Tooltip("When a collision occurs with another game object, ownership will automatically be transferred.")]
        [SerializeField] private bool transferAuthorityOnCollide;

        #region Hidden

        [Tooltip("An integer value that represents the unique identifier of the NetworkObject within the current scene.")]
        [HideInInspector, SerializeField] private int sceneID = -1;

        #endregion

        #region Deprecated

        [Obsolete]
        [HideInInspector]
        [SerializeField] private bool takeOwnershipOnCollision;

        #endregion

        #endregion

        #region Fields

        private static Dictionary<int, NetworkObject> _networkSceneObjectCache;
        private static Dictionary<int, NetworkObject> _networkObjectIDCache;

        private bool _isNotInMetaSpace;
        private IMetaSpaceNetworkingService _networking;
        private bool _isOnline;
        private List<NetworkObjectBehaviour> _behaviours;
        private Dictionary<short, List<RpcEventDelegate>> _queuedRpcHandlers;
        private Queue<QueuedRpcCall> _outgoingRPCQueue;
        private Dictionary<NetworkTransform, int> _childTransformIds;
        private Dictionary<int, NetworkTransform> _childTransforms;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a flag indicating whether ownership of this NetworkObject should be transferred when a collision occurs with another game object.
        /// </summary>
        public bool TransferAuthorityOnCollide => transferAuthorityOnCollide;

        /// <summary>
        /// Gets the IMetaSpaceNetworkingService instance that this NetworkObject is using.
        /// </summary>
        public IMetaSpaceNetworkingService Networking
        {
            get
            {
                if (_networking != null)
                    return _networking;
                if (!_isNotInMetaSpace)
                {
                    var metaSpace = MetaSpace.Instance;
                    if (metaSpace)
                        _networking = metaSpace.GetService<IMetaSpaceNetworkingService>();
                    else _isNotInMetaSpace = true;
                }
                return _networking;
            }
        }

        /// <summary>
        /// Gets the integer value that represents the unique identifier of this NetworkObject within the scene.
        /// </summary>
        public int SceneID => sceneID;

        /// <summary>
        /// Gets the integer value that represents the unique identifier of the player who owns this NetworkObject.
        /// </summary>
        public int InputAuthorityID
        {
            get
            {
                try
                {
                    if (Networking.IsOfflineMode)
                        return Networking.LocalPlayerID;
                    int ownerID = -1;
                    InputAuthorityIDInternal(ref ownerID);
                    return ownerID;
                }
                catch (Exception e)
                {
                    MetaverseProgram.Logger.LogError(e);
                    return -1;
                }
            }
        }

        /// <summary>
        /// Gets the integer value that represents the unique identifier of the player who is currently controlling this NetworkObject.
        /// </summary>
        public int StateAuthorityID
        {
            get
            {
                try
                {
                    if (Networking.IsOfflineMode)
                        return Networking.LocalPlayerID;
                    int controllerID = -1;
                    StateAuthorityIDInternal(ref controllerID);
                    return controllerID;
                }
                catch (Exception e)
                {
                    MetaverseProgram.Logger.LogError(e);
                    return -1;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the local player is currently controlling this NetworkObject.
        /// </summary>
        public bool IsStateAuthority => Networking == null || StateAuthorityID == Networking.LocalPlayerID;

        /// <summary>
        /// Gets a value indicating whether the local player is the owner of this NetworkObject.
        /// </summary>
        public bool IsInputAuthority => Networking == null || InputAuthorityID == Networking.LocalPlayerID;

        /// <summary>
        /// Gets the channel on which this NetworkObject is communicating.
        /// </summary>
        public byte Channel
        {
            get
            {
                byte val = 0;
                ChannelInternal(ref val);
                return val;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this NetworkObject has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this NetworkObject should be destroyed when the player who created it leaves the scene.
        /// </summary>
        public bool DestroyWhenCreatorLeave => destroyWhenCreatorLeaves;

        /// <summary>
        /// Gets an enumerable collection of all the NetworkObjects in the current scene.
        /// </summary>
        public static IEnumerable<NetworkObject> SceneObjects => _networkSceneObjectCache?.Values ?? (IReadOnlyCollection<NetworkObject>) Array.Empty<NetworkObject>();

        /// <summary>
        /// Gets the unique identifier of this NetworkObject within the network.
        /// </summary>
        public int NetworkID
        {
            get
            {
                int value = -1;
                NetworkIDInternal(ref value);
                return value;
            }
        }

        #endregion

        #region Unity Events

        private void OnValidate()
        {
            UpgradeFields();

            UpdateSceneID();
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;

            base.OnDestroy();

            if (sceneID >= 0)
                _networkSceneObjectCache?.Remove(sceneID);

            if (NetworkID >= 0)
                _networkObjectIDCache?.Remove(NetworkID);

            OnDestroyInternal();
        }

        #endregion

        #region Protected Methods

        protected override void OnMetaSpaceServicesRegistered()
        {
            if (IsInitialized)
                return;

            if (Networking == null)
            {
                enabled = false;
                return;
            }

            if (Networking.IsOfflineMode)
                InitializeOffline();
        }

        #endregion

        #region Public Methods

        public void AddBehaviour(NetworkObjectBehaviour behaviour)
        {
            if (_behaviours == null)
                _behaviours = new List<NetworkObjectBehaviour>();
            else if (_behaviours.Contains(behaviour))
                return;

            _behaviours.Add(behaviour);

            if (IsInitialized)
            {
                behaviour.OnNetworkReady(Networking == null || Networking.IsOfflineMode);
                if (IsStateAuthority) behaviour.OnLocalStateAuthority();
                else behaviour.OnRemoteStateAuthority();
                if (IsInputAuthority) behaviour.OnLocalInputAuthority();
                else behaviour.OnRemoteInputAuthority();
            }
        }

        public void RemoveBehaviour(NetworkObjectBehaviour behaviour)
        {
            _behaviours?.Remove(behaviour);
        }

        public void Register()
        {
            _networkSceneObjectCache ??= new Dictionary<int, NetworkObject>();
            if (sceneID >= 0)
                _networkSceneObjectCache[sceneID] = this;
        }

        [Obsolete("Please use '" + nameof(RequestAuthority) + "' instead.")]
        public void TakeOwnership() => RequestAuthority();

        public void RequestAuthority()
        {
#if UNITY_EDITOR
            MetaverseProgram.Logger.Log($"Requesting authority for {name} ({NetworkID})");
#endif
            RequestAuthorityInternal();
        }

        public void RegisterRPC(short procedureID, RpcEventDelegate handler, bool @override = true)
        {
            if (!_isOnline)
            {
                _queuedRpcHandlers ??= new Dictionary<short, List<RpcEventDelegate>>();

                if (!_queuedRpcHandlers.TryGetValue(procedureID, out List<RpcEventDelegate> handlers))
                    handlers = _queuedRpcHandlers[procedureID] = new List<RpcEventDelegate>();

                if (@override)
                {
                    handlers.Clear();
                    handlers.Add(handler);
                }
                else
                    handlers.Add(handler);
            }

            RegisterRPCInternal(procedureID, handler, @override);
        }

        public void UnregisterRPC(short procedureID, RpcEventDelegate handler)
        {
            if (_queuedRpcHandlers != null && _queuedRpcHandlers.TryGetValue(procedureID, out List<RpcEventDelegate> offlineHandlers))
                offlineHandlers.Remove(handler);

            UnregisterRPCInternal(procedureID, handler);
        }

        public void InvokeRPC(short procedureID, int playerID, object content)
        {
            if (Networking == null || Networking.IsOfflineMode)
            {
                if (!IsInitialized)
                {
                    QueueRPC(procedureID, playerID, content);
                }
                else if (_queuedRpcHandlers != null &&
                         _queuedRpcHandlers.TryGetValue(procedureID, out List<RpcEventDelegate> offlineHandlers))
                {
                    var handlers = offlineHandlers.ToArray();
                    foreach (RpcEventDelegate handler in handlers)
                        handler?.Invoke(procedureID, playerID, content);
                }

                return;
            }

            InvokeRPCInternal(procedureID, playerID, content);
        }

        public void InvokeRPC(short procedureID, NetworkMessageReceivers receivers, object content, bool buffered = false)
        {
            if (Networking == null || Networking.IsOfflineMode)
            {
                if (!IsInitialized)
                {
                    QueueRPC(procedureID, receivers, content, buffered);
                }
                else if (Networking != null &&
                         _queuedRpcHandlers != null &&
                         _queuedRpcHandlers.TryGetValue(procedureID, out List<RpcEventDelegate> offlineHandlers) &&
                         receivers != NetworkMessageReceivers.Others)
                {
                    var handlers = offlineHandlers.ToArray();
                    foreach (var handler in handlers)
                        handler?.Invoke(procedureID, Networking.LocalPlayerID, content);
                }

                return;
            }

            InvokeRPCInternal(procedureID, receivers, content, buffered);
        }

        public static bool TryGetSceneObject(int objID, out NetworkObject obj)
        {
            if (_networkSceneObjectCache != null)
                return _networkSceneObjectCache.TryGetValue(objID, out obj);
            obj = null;
            return false;
        }

        public static bool TryGetNetworkObject(int objID, out NetworkObject obj)
        {
            if (_networkObjectIDCache != null)
                return _networkObjectIDCache.TryGetValue(objID, out obj);
            obj = null;
            return false;
        }

        public int GetNetworkTransformId(NetworkTransform networkTransform)
        {
            InitChildTransforms();
            return _childTransformIds.TryGetValue(networkTransform, out int idx) ? idx : -1;
        }

        public NetworkTransform GetNetworkTransform(int id)
        {
            InitChildTransforms();
            return _childTransforms.TryGetValue(id, out NetworkTransform t) ? t : null;
        }

        public void Destroy()
        {
            if (Networking.IsOfflineMode)
            {
                Destroy(gameObject);
                return;
            }

            DestroyInternal();
        }

        #endregion

        #region Private Methods

        private void UpgradeFields()
        {
#pragma warning disable CS0612 // Type or member is obsolete
            if (takeOwnershipOnCollision)
            {
                transferAuthorityOnCollide = true;
                takeOwnershipOnCollision = false;
            }
#pragma warning restore CS0612 // Type or member is obsolete
        }

        private void InitChildTransforms()
        {
            if (_childTransformIds == null || _childTransforms == null)
            {
                _childTransformIds = new Dictionary<NetworkTransform, int>();
                _childTransforms = new Dictionary<int, NetworkTransform>();

                var childTransformsArray = gameObject.GetTopLevelComponentsInChildrenOrdered<NetworkTransform, NetworkObject>();
                if (childTransformsArray.Length > 0)
                {
                    for (int i = 0; i < childTransformsArray.Length; i++)
                    {
                        _childTransformIds[childTransformsArray[i]] = i;
                        _childTransforms[i] = childTransformsArray[i];
                    }
                }
            }
        }

        private void QueueRPC(short procedureID, NetworkMessageReceivers receivers, object content, bool buffered)
        {
            _outgoingRPCQueue ??= new Queue<QueuedRpcCall>();
            _outgoingRPCQueue.Enqueue(new QueuedRpcCall(procedureID, receivers, content, buffered));
        }

        private void QueueRPC(short procedureID, int playerID, object content)
        {
            _outgoingRPCQueue ??= new Queue<QueuedRpcCall>();
            _outgoingRPCQueue.Enqueue(new QueuedRpcCall(procedureID, playerID, content));
        }

        private void DequeueRPCs()
        {
            if (_outgoingRPCQueue == null)
                return;

            while (_outgoingRPCQueue.Count > 0)
            {
                QueuedRpcCall call = _outgoingRPCQueue.Dequeue();
                if (call.PlayerID != null)
                {
                    InvokeRPC(call.ProcedureID, call.PlayerID.Value, call.Content);
                }
                else
                {
                    InvokeRPC(call.ProcedureID, call.Receivers, call.Content, call.Buffered);
                }
            }
        }

        private void InitializeOffline()
        {
            if (IsInitialized)
                return;

            if (NetworkID >= 0)
            {
                _networkObjectIDCache ??= new();
                _networkObjectIDCache[NetworkID] = this;
            }

            IsInitialized = true;

            InitializeBehavioursOffline();

            DequeueRPCs();

            Initialized?.Invoke();
        }

        private void UpdateSceneID()
        {
            NetworkObject[] networkObjects = Resources.FindObjectsOfTypeAll<NetworkObject>();
            int maxID = networkObjects.Max(x => x.sceneID);

            if (gameObject.IsPrefab())
            {
                if (sceneID == -1) return;
                sceneID = -1;
                this.SetDirty();
            }
            else
            {
                if (sceneID == -1 || networkObjects.Any(x => x != this && x.sceneID == sceneID))
                    sceneID = maxID + 1;
            }
        }

        private void InitializeBehavioursOffline()
        {
            if (_behaviours == null) return;
            NetworkObjectBehaviour[] networkObjectBehaviours = _behaviours.Where(x => x).ToArray();
            foreach (NetworkObjectBehaviour beh in networkObjectBehaviours)
                if (beh)
                    beh.OnNetworkReady(true);
            foreach (NetworkObjectBehaviour beh in networkObjectBehaviours)
                if (beh)
                    beh.OnLocalStateAuthority();
            foreach (NetworkObjectBehaviour beh in networkObjectBehaviours)
                if (beh)
                    beh.OnLocalInputAuthority();
        }

        #endregion

        #region Partial Methods

        partial void NetworkIDInternal(ref int val);

        partial void StateAuthorityIDInternal(ref int val);

        partial void InputAuthorityIDInternal(ref int val);

        partial void ChannelInternal(ref byte val);

        partial void OnDestroyInternal();

        partial void RequestAuthorityInternal();

        partial void RegisterRPCInternal(short procedureID, RpcEventDelegate handler, bool @override = true);

        partial void UnregisterRPCInternal(short procedureID, RpcEventDelegate handler);

        partial void InvokeRPCInternal(short procedureID, int playerID, object content);

        partial void InvokeRPCInternal(short procedureID, NetworkMessageReceivers receivers, object content, bool buffered = false);

        partial void DestroyInternal();

        #endregion
    }
}