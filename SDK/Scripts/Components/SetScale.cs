using System;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Networking.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component to set the scale of a transform.
    /// </summary>
    [HideMonoScript]
    public class SetScale : TriInspectorMonoBehaviour
    {
        [Tooltip("The transform to set the scale of. If null, the transform of this game object will be used.")]
        [Required]
        [SerializeField] private Transform target;
        [Tooltip("The scale to set.")]
        [SerializeField] private Vector3 scale = Vector3.one;
        [Tooltip("Whether to use local space or world space. If true - adds the parent's (if any) scale to the Scale value, otherwise uses the raw Scale value.")]
        [SerializeField] private bool local = true;
                
        [Header("Networking")]
        [Tooltip("If true, broadcasts the scale to all clients.")]
        [SerializeField] private bool networked;
        [ShowIf(nameof(networked))]
        [Required]
        [SerializeField] private NetworkObject networkObject;
        [ShowIf(nameof(networked))]
        [SerializeField, ReadOnly] private short networkID;

        /// <summary>
        /// Gets or sets the scale of this object.
        /// </summary>
        public Vector3 Scale {
            get => scale;
            set => scale = value;
        }

        /// <summary>
        /// The length of the scale vector.
        /// </summary>
        public float ScaleLength {
            get => scale.magnitude;
            set => scale = Vector3.one * value;
        }
        
        /// <summary>
        /// Gets or sets the x scale of this object.
        /// </summary>
        public float ScaleX {
            get => scale.x;
            set => scale = new Vector3(value, scale.y, scale.z);
        }
        
        /// <summary>
        /// Gets or sets the y scale of this object.
        /// </summary>
        public float ScaleY {
            get => scale.y;
            set => scale = new Vector3(scale.x, value, scale.z);
        }
        
        /// <summary>
        /// Gets or sets the z scale of this object.
        /// </summary>
        public float ScaleZ {
            get => scale.z;
            set => scale = new Vector3(scale.x, scale.y, value);
        }

        private void Awake()
        {
            if (!target) target = transform;
            if (networked && networkObject)
            {
                networkObject.RegisterRPC((short)NetworkRpcType.SetScale, RPC_SetScale);
            }
        }

        private void Start() { /* for enabling/disabling */ }

        private void OnDestroy()
        {
            if (networked && networkObject)
            {
                networkObject.UnregisterRPC((short)NetworkRpcType.SetScale, RPC_SetScale);
            }
        }

        private void Reset()
        {
            target = transform;
        }

        private void OnValidate()
        {
            if (networked && networkObject)
            {
                networkID = networkObject.GetNetworkObjectBehaviorID(this);
            }
        }

        /// <summary>
        /// Applies the scale to the target transform. If the target is null, will use the transform of this game object.
        /// </summary>
        /// <param name="value">The scale to apply.</param>
        public void ApplyScale(float value)
        {
            ScaleLength = value;
            ApplyScale();
        }
        
        /// <summary>
        /// Applies the scale to the target transform. If the target is null, will use the transform of this game object.
        /// </summary>
        /// <param name="value">The scale to apply.</param>
        public void ApplyScale(Vector3 value)
        {
            Scale = value;
            ApplyScale();
        }

        /// <summary>
        /// Applies the scale to the target transform. If the target is null, will use the transform of this game object.
        /// </summary>
        public void ApplyScale()
        {
            if (!isActiveAndEnabled) return;
            if (networked && networkObject)
            {
                networkObject.InvokeRPC((short)NetworkRpcType.SetScale, NetworkMessageReceivers.All, new object[] { networkID, scale });
                return;
            }
            
            ApplyScaleInternal();
        }

        [Obsolete("Use ApplyScale() instead.")]
        public void UpdateScale()
        {
            ApplyScale();
        }

        private void ApplyScaleInternal()
        {
            if (!isActiveAndEnabled) return;
            if (local) target.localScale = scale;
            else target.localScale = target.parent ? target.parent.InverseTransformVector(scale) : scale;
        }

        private void RPC_SetScale(short procedureId, int senderId, object content)
        {
            if (content is not object[] { Length: 2 } args || 
                args[0] is not short id ||
                args[1] is not Vector3 scl) return;
            
            if (id != networkID)
                return;

            Scale = scl;
            ApplyScaleInternal();
        }
    }
}
