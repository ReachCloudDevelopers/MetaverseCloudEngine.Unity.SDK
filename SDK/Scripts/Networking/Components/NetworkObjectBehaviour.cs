using System.Linq;

using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Unity.Networking.Abstract;

using TriInspectorMVCE;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    /// <summary>
    /// A base class for network behaviours that are attached to a network object and
    /// reacts to network events. Also provides access to the network object and useful
    /// network properties.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.Finalize)]
    public abstract class NetworkObjectBehaviour : MetaSpaceBehaviour
    {
        [Tooltip("Will automatically assign the network object field if it's null.")]
        [SerializeField] [HideInInspector] private bool autoAssignNetworkObject = true;
        [HideIf(nameof(autoAssignNetworkObject))]
        [Tooltip("The network object that this behaviour is attached to.")]
        [SerializeField] [HideInInspector] private NetworkObject networkObject;

        private bool _foundNetworkObject;
        private IMetaSpaceNetworkingService _networking;

        /// <summary>
        /// The network object that this behaviour is attached to.
        /// </summary>
        public NetworkObject NetworkObject
        {
            get
            {
                EnsureNetworkObject();
                return networkObject;
            }
        }
        /// <summary>
        /// The ID of the network object that this behaviour is attached to.
        /// </summary>
        public int NetworkID => !NetworkObject ? -1 : NetworkObject.NetworkID;
        /// <summary>
        /// True if this client has state authority over the network object.
        /// </summary>
        /// <remarks>
        /// State authority refers to the authority to update the state of the network object (position, rotation, etc).
        /// </remarks>
        public bool IsStateAuthority => !NetworkObject || NetworkObject.IsStateAuthority;
        /// <summary>
        /// True if this client has input authority over the network object.
        /// </summary>
        /// <remarks>
        /// Input authority refers to the authority to send input to the state authority so that
        /// the state authority can update the state of the network object.
        /// </remarks>
        public bool IsInputAuthority => !NetworkObject || NetworkObject.IsInputAuthority;
        /// <summary>
        /// The networking service of the currently active meta space.
        /// </summary>
        public IMetaSpaceNetworkingService MetaSpaceNetworkingService
        {
            get
            {
                if (_networking != null)
                    return _networking;
                if (MetaSpace)
                    _networking = MetaSpace.GetService<IMetaSpaceNetworkingService>();
                return _networking;
            }
        }

        private void Reset()
        {
            EnsureNetworkObject();
        }

        protected virtual void OnValidate()
        {
            if (!Application.isPlaying && autoAssignNetworkObject)
                networkObject = null;
        }

        protected override void Awake()
        {
            base.Awake();

            EnsureNetworkObject();

            if (networkObject)
            {
                RegisterNetworkRPCs();
                networkObject.AddBehaviour(this);
            }
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            
            base.OnDestroy();

            if (networkObject)
            {
                UnRegisterNetworkRPCs();
                networkObject.RemoveBehaviour(this);
            }
        }

        /// <summary>
        /// This method is called when it's time to register your RPC callbacks.
        /// </summary>
        protected virtual void RegisterNetworkRPCs()
        {
        }

        /// <summary>
        /// This method is called when it's time to unregister RPC callbacks.
        /// </summary>
        protected virtual void UnRegisterNetworkRPCs()
        {
        }

        private void EnsureNetworkObject()
        {
            if (_foundNetworkObject)
                return;

            if (!autoAssignNetworkObject)
                return;

            if (!networkObject) networkObject = GetComponent<NetworkObject>();
            if (!networkObject) networkObject = GetComponentsInParent<NetworkObject>(true).FirstOrDefault();

            if (Application.isPlaying)
                _foundNetworkObject = networkObject;
        }

        public virtual void OnNetworkReady(bool offline)
        {
        }

        public virtual void OnLocalStateAuthority()
        {
        }

        public virtual void OnRemoteStateAuthority()
        {
        }

        public virtual void OnLocalInputAuthority()
        {
        }

        public virtual void OnRemoteInputAuthority()
        {
        }
    }
}