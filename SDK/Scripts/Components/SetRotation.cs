using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component for applying rotation to a game object within the world.
    /// </summary>
    public class SetRotation : TriInspectorMonoBehaviour
    {
        [Tooltip("The target to apply the rotation to. If null, will use this game object.")]
        [Required]
        [SerializeField] private Transform target;
        [Tooltip("The rotation to apply.")]
        [SerializeField] private Vector3 rotation;
        [Tooltip("Whether to use local space or world space. If true - adds the parent's (if any) rotation to the Rotation value, otherwise uses the raw Rotation value.")]
        [SerializeField] private bool local = true;
        [Tooltip("What coordinate system to use when applying the rotation. If World - will apply the rotation relative to the parent (if parented) or world space (if unparented). If Self - adds the rotation to our existing rotation.")]
        [SerializeField] private Space space = Space.World;
        [Tooltip("If true, will apply the rotation in degrees per second every frame.")]
        [SerializeField] private bool perSecond;
        
        [Header("Networking")]
        [Tooltip("If true, broadcasts the rotation to all clients.")]
        [SerializeField] private bool networked;
        [ShowIf(nameof(networked))]
        [Required]
        [SerializeField] private NetworkObject networkObject;
        [ShowIf(nameof(networked))]
        [SerializeField, ReadOnly] private short networkID;

        /// <summary>
        /// Gets or sets the rotation of this object.
        /// </summary>
        public Quaternion RotationQ
        {
            get => Quaternion.Euler(rotation);
            set => rotation = value.eulerAngles;
        }

        /// <summary>
        /// Gets or sets the rotation of this object.
        /// </summary>
        public Vector3 Rotation {
            get => rotation;
            set => rotation = value;
        }

        private void OnValidate()
        {
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
                networkObject.RegisterRPC((short)NetworkRpcType.SetPosition, RPC_SetRotation);
            }
        }

        private void OnDestroy()
        {
            if (networked && networkObject)
            {
                networkObject.UnregisterRPC((short)NetworkRpcType.SetPosition, RPC_SetRotation);
            }
        }
        
        private void Reset()
        {
            target = transform;
        }

        private void Update()
        {
            if (perSecond && !networked)
                UpdateRotation();
        }
        
        /// <summary>
        /// Updates the rotation.
        /// </summary>
        /// <param name="rot">The rotation to apply.</param>
        public void UpdateRotation(Quaternion rot)
        {
            RotationQ = rot;
            UpdateRotation();
        }
        
        /// <summary>
        /// Updates the rotation.
        /// </summary>
        /// <param name="rot">The rotation to apply.</param>
        public void UpdateRotation(Vector3 rot)
        {
            Rotation = rot;
            UpdateRotation();
        }

        /// <summary>
        /// Updates the rotation.
        /// </summary>
        public void UpdateRotation()
        {
            if (!isActiveAndEnabled)
                return;

            if (networked && networkObject)
            {
                networkObject.InvokeRPC(
                    (short)NetworkRpcType.SetPosition,
                    NetworkMessageReceivers.All,
                    new object[]
                    {
                        networkID, 
                        rotation 
                    });
                return;
            }

            ApplyRotationInternal();
        }

        private void ApplyRotationInternal()
        {
            if (!isActiveAndEnabled) return;
            var delta = perSecond ? Time.deltaTime : 1f;
            if (space == Space.World)
            {
                if (local) target.localEulerAngles = rotation * delta;
                else target.eulerAngles = rotation * delta;
            }
            else
            {
                if (local || !target.parent) target.Rotate(rotation * delta, Space.Self);
                else target.rotation *= target.parent.rotation * Quaternion.Euler(delta * rotation);
            }
        }

        private void RPC_SetRotation(short procedureId, int senderId, object content)
        {
            if (content is not object[] { Length: 2 } args || 
                args[0] is not short id ||
                args[1] is not Quaternion rot) return;
            
            if (id != networkID)
                return;

            RotationQ = rot;
            ApplyRotationInternal();
        }
    }
}
