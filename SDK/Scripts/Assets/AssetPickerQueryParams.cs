using System;
using MetaverseCloudEngine.Common.Enumerations;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Assets
{
    [Serializable]
    public class AssetPickerQueryParams
    {
        [Serializable]
        public class AssetPickerBlockchainQueryParams
        {
            public BlockchainReferenceAsset[] assets = Array.Empty<BlockchainReferenceAsset>();
            public BlockchainReferenceCategory[] categories = Array.Empty<BlockchainReferenceCategory>();
        }

        [Header("Default")]
        public bool canSearch = true;
        public string searchFilter;
        public string descriptionFilter;

        [Header("Blockchain")]
        public bool canChangeBlockchainType = true;
        public BlockchainType blockchainType;
        public AssetPickerBlockchainQueryParams blockchainParams = new();
        
        [Header("Contributor")]
        public string contributorName;
    }
}