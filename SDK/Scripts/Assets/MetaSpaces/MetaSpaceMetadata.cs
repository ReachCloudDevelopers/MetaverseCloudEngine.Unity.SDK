using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Common.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    /// <summary>
    /// Class containing metadata for a Meta Space.
    /// </summary>
    [Serializable]
    public class MetaSpaceMetadata : AssetMetadata
    {
        [Title("User Tracking")]
        [Tooltip("Tracking details that are required from the user in order to even join the space.")]
        [SerializeField] private SystemUserTrackingDetails requiredTrackingDetails = SystemUserTrackingDetails.None;

        [Title("Join-ability")]
        [Tooltip("Specifies the behavior when joining the Meta Space.")]
        [SerializeField] private MetaSpaceJoinBehaviour joinBehavior = MetaSpaceJoinBehaviour.AlwaysJoinPublic;
        [Tooltip("Specifies the requirements for joining the Meta Space.")]
        [SerializeField] private MetaSpaceJoinRequirements joinRequirements = MetaSpaceJoinRequirements.None;
        [Tooltip("If true, users can join this space even if they're already in another space at the same time.")]
        [SerializeField] private bool allowConcurrentLogins;

        [Title("Prefabs")]
        [Tooltip("List of prefabs to be loaded when the Meta Space starts.")]
        [SerializeField] private List<MetaPrefabToLoadOnStart> loadOnStartPrefabs = new();

        [Title("AR / VR Support")]
        [Tooltip("Specifies the level of VR support for the Meta Space.")]
        [SerializeField] private XrSupportOption vrSupport;
        [Tooltip("Specifies whether AR support is required for the Meta Space.")]
        [SerializeField] private bool arRequired;
        
        [Title("Categorization")]
        [Tooltip("Specifies the tags for the Meta Space.")]
        [SerializeField] private MetaSpaceTags tags = MetaSpaceTags.Uncategorized;

        /// <summary>
        /// The level of VR support for the Meta Space.
        /// </summary>
        public XrSupportOption VRSupport {
            get => vrSupport;
            set => vrSupport = value;
        }

        /// <summary>
        /// Whether AR support is required for the Meta Space.
        /// </summary>
        public bool ARRequired {
            get => arRequired;
            set => arRequired = value;
        }

        /// <summary>
        /// Whether the Meta Space is related to cryptocurrency or NFTs. Changing this property in source may result
        /// in your account being suspended.
        /// </summary>
        public bool CryptoRelated => BlockchainReferences.Assets.Length > 0 || BlockchainReferences.Categories.Length > 0;

        /// <summary>
        /// The list of prefabs to be loaded when the Meta Space starts.
        /// </summary>
        public IEnumerable<MetaPrefabToLoadOnStart> LoadOnStartPrefabs {
            get => loadOnStartPrefabs;
            set => loadOnStartPrefabs = value.ToList();
        }

        /// <summary>
        /// The behavior when joining the Meta Space.
        /// </summary>
        public MetaSpaceJoinBehaviour JoinBehavior {
            get => joinBehavior;
            set => joinBehavior = value;
        }

        /// <summary>
        /// The requirements for joining the Meta Space.
        /// </summary>
        public MetaSpaceJoinRequirements JoinRequirements {
            get => joinRequirements;
            set => joinRequirements = value;
        }

        public SystemUserTrackingDetails RequiredTrackingDetails {
            get => requiredTrackingDetails;
            set => requiredTrackingDetails = value;
        }

        public bool AllowConcurrentLogins {
            get => allowConcurrentLogins;
            set => allowConcurrentLogins = value;
        }
        
        public MetaSpaceTags Tags {
            get => tags;
            set => tags = value;
        }
    }
}
