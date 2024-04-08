using MetaverseCloudEngine.Common.Enumerations;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets
{
    /// <summary>
    /// Used by assets to store information about themselves such as name, description, and blockchain references, etc.
    /// </summary>
    [System.Serializable]
    public abstract class AssetMetadata
    {
        [Title("Details")]
        [Tooltip("The name of the asset.")]
        [SerializeField][Required] private string name;
        [Tooltip("A description of the asset.")]
        [SerializeField][TextArea(5, 15)] private string description;
        [Tooltip("The listings that the asset is available on.")]
        [SerializeField] private AssetListings listings = AssetListings.Unlisted;
        [Tooltip("Whether the asset is private or not. Private assets are only visible to contributors.")]
        [SerializeField] private bool @private;

        [Title("Blockchain")]
        [Tooltip("The blockchain references for the asset. Not to be confused with blockchain source. This allows you " +
                 "to let users have access to the content of the asset if they own a referenced token on the " +
                 "blockchain.")]
        [SerializeField] private BlockchainReferences blockchainReferences;

        /// <summary>
        /// The name of the asset.
        /// </summary>
        public string Name {
            get => name;
            set => name = value;
        }

        /// <summary>
        /// The description of the asset.
        /// </summary>
        public string Description {
            get => description;
            set => description = value;
        }

        /// <summary>
        /// The listings that the asset is available on.
        /// </summary>
        public AssetListings Listings {
            get => listings;
            set => listings = value;
        }

        /// <summary>
        /// Whether the asset is private or not. Private assets are only visible to contributors.
        /// </summary>
        public bool Private {
            get => @private;
            set => @private = value;
        }

        /// <summary>
        /// The blockchain references for the asset. Not to be confused with blockchain source. This allows you
        /// to let users have access to the content of the asset if they own a referenced token on the
        /// blockchain.
        /// </summary>
        public BlockchainReferences BlockchainReferences {
            get => blockchainReferences;
            set => blockchainReferences = value;
        }

        public virtual void Reset()
        {
        }

        public virtual void OnValidate()
        {
        }
    }
}
