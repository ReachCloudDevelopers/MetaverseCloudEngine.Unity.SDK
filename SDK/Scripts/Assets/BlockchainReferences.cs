using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets
{
    [System.Serializable]
    public class BlockchainReferences
    {
        [SerializeField] private BlockchainReferenceCategory[] categories;
        [SerializeField] private BlockchainReferenceAsset[] assets;

        public BlockchainReferenceCategory[] Categories
        {
            get => categories;
            set => categories = value;
        }

        public BlockchainReferenceAsset[] Assets
        {
            get => assets;
            set => assets = value;
        }
    }
}