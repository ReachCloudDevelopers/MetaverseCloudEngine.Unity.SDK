using UnityEngine;
using MetaverseCloudEngine.Unity.Components;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using System;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Assets
{
    /// <summary>
    /// A list of all the platforms that an asset can be built for. Not to be confused with
    /// <see cref="Platform"/> which is specific to the API (even though they have the same values).
    /// </summary>
    [Flags]
    public enum AssetBuildPlatform
    {
        StandaloneWindows64 = 1,
        Android = 2,
        iOS = 4,
        StandaloneOSX = 8,
        WebGL = 16,
        StandaloneLinux64 = 32,
        [InspectorName("Android VR (Oculus Quest)")]
        AndroidVR = 64,
    }

    /// <summary>
    /// A base class for all asset types within the Metaverse. An asset is a piece of content that can be
    /// owned by a user and can be used in the Metaverse.
    /// </summary>
    /// <typeparam name="TMetaData">The type of metadata associated with this asset.</typeparam>
    [HideMonoScript]
    [DisallowMultipleComponent]
    [DeclareFoldoutGroup("Meta Data")]
    public abstract class Asset<TMetaData> : MetaverseBehaviour, IAssetReference where TMetaData : AssetMetadata, new()
    {
        public enum PublishMode
        {
            Auto,
            [InspectorName("Don't Publish")]
            DontPublish,
            Publish,
        }

        [SerializeField, HideInInspector] private AssetBuildPlatform supportedPlatforms = (AssetBuildPlatform)~0;
        [SerializeField, HideInInspector] private PublishMode publish = PublishMode.Auto;
        [SerializeField, HideInInspector] protected string id;
        [SerializeField, HideInInspector] protected string blockchainSource;
        [SerializeField, HideInInspector] protected BlockchainType blockchainType;
        [Group("Meta Data"), InlineProperty(LabelWidth = 0), LabelText(""), SerializeField] private TMetaData metaData;

        // Retry mechanism fields
        [SerializeField, HideInInspector] private bool lastUploadFailed;
        [SerializeField, HideInInspector] private string[] lastBundlePlatforms = System.Array.Empty<string>();
        [SerializeField, HideInInspector] private string[] lastBundlePaths = System.Array.Empty<string>();

        /// <summary>
        /// The ID of the asset. This is a unique identifier that is used to identify the asset in the
        /// Metaverse. If the asset is not yet uploaded, this will be null. Also, just because this is
        /// not null, it does not mean that the asset has been uploaded.
        /// </summary>
        public Guid? ID
        {
            get => Guid.TryParse(id, out var assetId) ? assetId : (Guid?) null;
            set => id = value?.ToString();
        }

        /// <summary>
        /// The same as <see cref="ID"/> but as a string.
        /// </summary>
        public string IDString
        {
            get => id;
            set => id = value;
        }

        /// <summary>
        /// The blockchain source of the asset. This is the blockchain asset ID that this asset is
        /// associated with. This is only relevant if you want the "ID" of the asset to be the
        /// asset ID on the blockchain. Note that permissions will not be handled by the API if you
        /// use this. It will be up to you to handle permissions on the blockchain.
        /// </summary>
        public string BlockchainSource
        {
            get => blockchainSource;
            set => blockchainSource = value;
        }

        /// <summary>
        /// The blockchain type of the asset. This is the blockchain type that the asset is associated
        /// with. See <see cref="BlockchainSource"/> for more information.
        /// </summary>
        public BlockchainType BlockchainSourceType
        {
            get => blockchainType;
            set => blockchainType = value;
        }

        /// <summary>
        /// The same as <see cref="BlockchainSourceType"/> but as a string. (For easier access from Unity Events)
        /// </summary>
        public string BlockchainSourceTypeString
        {
            get => blockchainType.ToString();
            set => blockchainType = Enum.TryParse(value, out BlockchainType type) ? type : blockchainType;
        }

        /// <summary>
        /// Whether or not the asset should be automatically published when it is uploaded.
        /// </summary>
        public bool? Publish
        {
            get => publish switch
            {
                PublishMode.Auto => null,
                PublishMode.DontPublish => false,
                PublishMode.Publish => true,
                _ => null
            };
            set => publish = value switch
            {
                null => PublishMode.Auto,
                false => PublishMode.DontPublish,
                true => PublishMode.Publish,
            };
        }

        /// <summary>
        /// The asset's metadata. This is the data that is used to describe the asset. This is
        /// not stored on the blockchain, but is stored in the cloud.
        /// </summary>
        public TMetaData MetaData
        {
            get => metaData ??= new TMetaData();
            set => metaData = value;
        }
        
        public AssetBuildPlatform SupportedBuildPlatforms
        {
            get => supportedPlatforms;
            set => supportedPlatforms = value;
        }

        /// <summary>
        /// The platforms that this asset is supported on. This is used to determine which platforms
        /// the asset can be built for. If you want to build for all platforms, set this to
        /// <see cref="Platform.All"/>.
        /// </summary>
        public Platform SupportedPlatforms
        {
            get
            {
                if (supportedPlatforms < 0) // "Everything" is set.
                {
                    // Sum all the values that are set.
                    Platform p = 0;
                    if ((supportedPlatforms & AssetBuildPlatform.StandaloneWindows64) != 0)
                        p |= Platform.StandaloneWindows64;
                    if ((supportedPlatforms & AssetBuildPlatform.Android) != 0)
                        p |= Platform.Android;
                    if ((supportedPlatforms & AssetBuildPlatform.WebGL) != 0)
                        p |= Platform.WebGL;
                    if ((supportedPlatforms & AssetBuildPlatform.AndroidVR) != 0)
                        p |= Platform.AndroidVR;
                    if ((supportedPlatforms & AssetBuildPlatform.StandaloneLinux64) != 0)
                        p |= Platform.StandaloneLinux64;
                    if ((supportedPlatforms & AssetBuildPlatform.StandaloneOSX) != 0)
                        p |= Platform.StandaloneOSX;
                    if ((supportedPlatforms & AssetBuildPlatform.iOS) != 0)
                        p |= Platform.iOS;
                    return p;
                }

                return (Platform)supportedPlatforms;
            }
            set => supportedPlatforms = (AssetBuildPlatform)value;
        }

        /// <summary>
        /// Gets or sets whether the last upload attempt failed.
        /// </summary>
        public bool LastUploadFailed
        {
            get => lastUploadFailed;
            set => lastUploadFailed = value;
        }

        /// <summary>
        /// Gets the platforms for the last failed upload.
        /// </summary>
        public string[] LastBundlePlatforms
        {
            get => lastBundlePlatforms ?? System.Array.Empty<string>();
            set => lastBundlePlatforms = value;
        }

        /// <summary>
        /// Gets the file paths for the last failed upload.
        /// </summary>
        public string[] LastBundlePaths
        {
            get => lastBundlePaths ?? System.Array.Empty<string>();
            set => lastBundlePaths = value;
        }

        protected virtual void Reset()
        {
            metaData = new TMetaData();
            metaData.Reset();
        }

        protected virtual void OnValidate()
        {
            metaData?.OnValidate();
        }

        /// <summary>
        /// Updates the asset from the given DTO.
        /// </summary>
        /// <param name="dto">The data transfer object to update from.</param>
        public void UpdateFromDto(AssetDto dto)
        {
            id = dto.Id.ToString();
            blockchainSource = dto.BlockchainSource;
            blockchainType = dto.BlockchainSourceType;
            MetaData = new TMetaData
            {
                BlockchainReferences = new BlockchainReferences
                {
                    Assets = dto.BlockchainReferences?.Assets.Select(x => new BlockchainReferenceAsset
                    {
                        asset = x.Asset,
                        type = x.Type
                    }).ToArray(),
                    Categories = dto.BlockchainReferences?.Categories.Select(x => new BlockchainReferenceCategory
                    { 
                        category = x.Category,
                        type = x.Type
                    
                    }).ToArray()
                },
                Name = dto.Name,
                Description = dto.Description,
                Listings = dto.Listings,
                Private = dto.Private,
            };

            UpdateFromDtoInternal(MetaData, dto);
        }

        /// <summary>
        /// If you have any custom metadata, you can override this method to update it from the DTO.
        /// </summary>
        /// <param name="md">The metadata to update.</param>
        /// <param name="dto">The data transfer object to update from.</param>
        protected virtual void UpdateFromDtoInternal(TMetaData md, AssetDto dto)
        {
        }
    }
}
