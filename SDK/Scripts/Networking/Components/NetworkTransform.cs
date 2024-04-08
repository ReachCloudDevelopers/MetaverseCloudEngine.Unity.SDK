using System;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine;
 
namespace MetaverseCloudEngine.Unity.Networking.Components
{
    /// <summary>
    /// The Network Transform component allows an objects position, rotation, 
    /// scale, parent, and active state to be synchronized to other clients. 
    /// You can have up to 128 Network Transform components per <see cref="NetworkObject"/> 
    /// (this number can be exceeded however it's not recommended).
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Networking/Network Transform")]
    [HierarchyIcon("d_Transform Icon")]
    [HelpURL("https://reach-cloud.gitbook.io/reach-explorer-documentation/docs/development-guide/unity-engine-sdk/components/networking/network-transform")]
    public partial class NetworkTransform : NetworkObjectBehaviour
    {
        /// <summary>
        /// Synchronization options to use for the <see cref="Transform"/> component.
        /// </summary>
        [Flags]
        public enum SyncOptions
        {
            Position = 1,
            Rotation = 2,
            Scale = 4,
            Parent = 8,
            GameObjectActive = 16
        }

        [Tooltip("Use these options to have full control over what particular parts of the Transform are synchronized.")]
        public SyncOptions synchronizationOptions;
    }
}
