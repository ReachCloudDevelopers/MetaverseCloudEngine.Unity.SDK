using System;
using System.Linq;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    /// <summary>
    /// A component that can be added to a NetworkObject to allow it to receive RPCs from the server and other clients.
    /// </summary>
    [AddComponentMenu(MetaverseConstants.ProductName + "/Networking/Network Object RPC")]
    [HideMonoScript]
    public class NetworkObjectRpc : NetworkObjectBehaviour
    {
        public enum RpcReceiver
        {
            /// <summary>
            /// The RPC will be sent to all clients.
            /// </summary>
            All,
            /// <summary>
            /// The RPC will be sent to all clients except this one.
            /// </summary>
            Others,
            /// <summary>
            /// The RPC will be sent to the server / host.
            /// </summary>
            Host,
            /// <summary>
            /// The RPC will be sent to the client that has state authority over this NetworkObject.
            /// </summary>
            StateAuthority,
            /// <summary>
            /// The RPC will be sent to the client that has input authority over this NetworkObject.
            /// </summary>
            InputAuthority,
        }
        
        [Attributes.ReadOnly, HideInInspector] public string rpcID;

        [Tooltip("The receivers of this RPC.")]
        public RpcReceiver receivers = RpcReceiver.All;
        [Tooltip("The event that will be invoked when this RPC is received.")]
        public UnityEvent onReceive;

        private Guid? _rpcID;
        private Guid RpcID
        {
            get
            {
                if (_rpcID != null) return _rpcID.GetValueOrDefault();
                if (!Guid.TryParse(rpcID, out var id)) return default;
                _rpcID = id;
                return _rpcID.Value;
            }
        }

        private void EnsureUniqueRpcId()
        {
            if (!string.IsNullOrEmpty(rpcID) && !IsRpcIdCopied())
                return;

            rpcID = Guid.NewGuid().ToString();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureUniqueRpcId();
        }

        private void Reset()
        {
            EnsureUniqueRpcId();
        }

        protected override void RegisterNetworkRPCs()
        {
            base.RegisterNetworkRPCs();
            NetworkObject.RegisterRPC((short)NetworkRpcType.NetworkObjectRpc, RPC_OnRpc, @override: false);
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;

            base.OnDestroy();

            if (NetworkObject)
                NetworkObject.UnregisterRPC((short)NetworkRpcType.NetworkObjectRpc, RPC_OnRpc);
        }

        /// <summary>
        /// Sends this RPC to the receivers specified in the <see cref="receivers"/> field.
        /// </summary>
        public void SendRpc()
        {
            if (string.IsNullOrEmpty(rpcID)) return;
            switch (receivers)
            {
                case RpcReceiver.StateAuthority:
                {
                    if (NetworkObject.StateAuthorityID != -1)
                        NetworkObject.InvokeRPC((short)NetworkRpcType.NetworkObjectRpc, NetworkObject.StateAuthorityID, rpcID);
                    break;
                }
                case RpcReceiver.InputAuthority:
                {
                    if (NetworkObject.InputAuthorityID != -1)
                        NetworkObject.InvokeRPC((short)NetworkRpcType.NetworkObjectRpc, NetworkObject.InputAuthorityID, rpcID);
                    break;
                }
                default:
                    NetworkObject.InvokeRPC((short)NetworkRpcType.NetworkObjectRpc, (NetworkMessageReceivers)receivers, rpcID);
                    break;
            }
        }

        private void RPC_OnRpc(short procedureID, int sendingPlayer, object content)
        {
            if (content is string rpcIDString && Guid.TryParse(rpcIDString, out var id) && id == RpcID)
                onReceive?.Invoke();
        }

        private bool IsRpcIdCopied()
        {
            if (!NetworkObject) return false;
            return NetworkObject.gameObject
                .GetTopLevelComponentsInChildrenOrdered<NetworkObjectRpc, NetworkObject>()
                .Any(x => x != this && x.rpcID == rpcID);
        }
    }
}
