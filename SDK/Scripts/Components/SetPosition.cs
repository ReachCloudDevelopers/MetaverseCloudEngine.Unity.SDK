using System;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component for applying position to a game object within the world.
    /// </summary>
    [HideMonoScript]
    [Experimental]
    public class SetPosition : TriInspectorMonoBehaviour
    {
        [Tooltip("The target to apply the position to. If null, will use this game object.")]
        [Required]
        [SerializeField] private Transform target;
        [Tooltip("The position to apply.")]
        [SerializeField] private Vector3 position;
        [Tooltip("Whether to use local space or world space. If true - adds the parent's (if any) position to the Position value, otherwise uses the raw Position value.")]
        [SerializeField] private bool local = true;
        [Tooltip("What coordinate system to use when applying the position. If World - will apply the position relative to the parent (if parented) or world space (if unparented). If Self - adds the position to our existing position.")]
        [SerializeField] private Space space = Space.World;
        [HideIf(nameof(networked))]
        [Tooltip("If true, will apply the position in meters per second every frame.")]
        [SerializeField] private bool perSecond;
        [ShowIf(nameof(perSecond))]
        [SerializeField] private bool useFixedUpdate;
        
        [Header("Networking")]
        [Tooltip("If true, broadcasts the position to all clients.")]
        [SerializeField] private bool networked;
        [ShowIf(nameof(networked))]
        [Required]
        [SerializeField] private NetworkObject networkObject;
        [ShowIf(nameof(networked))]
        [SerializeField, ReadOnly] private short networkID;

        /// <summary>
        /// Gets or sets the position of the game object.
        /// </summary>
        public Vector3 Position {
            get => position;
            set => position = value;
        }
        
        /// <summary>
        /// Gets or sets the x position of the game object.
        /// </summary>
        public float PositionX {
            get => position.x;
            set => position = new Vector3(value, position.y, position.z);
        }
        
        /// <summary>
        /// Gets or sets the y position of the game object.
        /// </summary>
        public float PositionY {
            get => position.y;
            set => position = new Vector3(position.x, value, position.z);
        }
        
        /// <summary>
        /// Gets or sets the z position of the game object.
        /// </summary>
        public float PositionZ {
            get => position.z;
            set => position = new Vector3(position.x, position.y, value);
        }

        /// <summary>
        /// Gets or sets the target object to set the position of.
        /// </summary>
        public Transform Target {
            get => target;
            set => target = value;
        }

        private void OnValidate()
        {
            if (!target) target = transform;
            
            networkID = networkObject.GetNetworkObjectBehaviorID(this);
            if (networked)
            {
                perSecond = false;
            }
        }
        
        private void Awake()
        {
            if (!target) target = transform;
            if (networked && networkObject)
            {
                networkObject.RegisterRPC((short)NetworkRpcType.SetPosition, RPC_SetPosition);
            }
        }

        private void OnDestroy()
        {
            if (networked && networkObject)
            {
                networkObject.UnregisterRPC((short)NetworkRpcType.SetPosition, RPC_SetPosition);
            }
        }

        private void Reset()
        {
            target = transform;
        }

        private void Update()
        {
            if (perSecond && !networked && !useFixedUpdate)
                UpdatePosition();
        }

        private void FixedUpdate()
        {
            if (perSecond && !networked && useFixedUpdate)
                UpdatePosition();
        }

        /// <summary>
        /// Update the position.
        /// </summary>
        /// <param name="pos">The position to apply.</param>
        public void UpdatePosition(Vector3 pos)
        {
            position = pos;
            UpdatePosition();
        }
        
        /// <summary>
        /// Update the position.
        /// </summary>
        public void UpdatePosition()
        {
            if (!isActiveAndEnabled)
                return;

            if (networked && networkObject)
            {
                networkObject.InvokeRPC((short)NetworkRpcType.SetPosition, NetworkMessageReceivers.All, new object[] { networkID, position });
                return;
            }

            ApplyPositionInternal();
        }

        private void ApplyPositionInternal()
        {
            if (!isActiveAndEnabled) return;
            float delta = perSecond ? Time.deltaTime : 1f;
            if (space == Space.World)
            {
                if (local) target.localPosition = position * delta;
                else target.position = position * delta;
            }
            else
            {
                if (local || !target.parent) target.Translate(position * delta, Space.Self);
                else target.position += target.parent.rotation * (delta * position);
            }
        }

        private void RPC_SetPosition(short procedureId, int senderId, object content)
        {
            if (content is not object[] { Length: 2 } args || 
                args[0] is not short id ||
                args[1] is not Vector3 pos) return;
            
            if (id != networkID)
                return;
                
            position = pos;
            ApplyPositionInternal();
        }
    }
}
