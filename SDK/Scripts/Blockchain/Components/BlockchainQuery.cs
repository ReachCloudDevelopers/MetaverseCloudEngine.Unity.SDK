using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Blockchain.Components
{
    /// <summary>
    /// Allows you to query the blockchain for asset information.
    /// </summary>
    [Experimental]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Blockchain/Blockchain Query")]
    public partial class BlockchainQuery : TriInspectorMonoBehaviour
    {
        [System.Serializable]
        public class BlockchainQueryEvent
        {
            public UnityEvent onTrue = new();
            public UnityEvent onFalse = new();
        }

        [System.Serializable]
        public class BlockchainQueryStateEvents
        {
            public UnityEvent<string> onCalculateQueryValue = new();
            public UnityEvent onQueryBegin = new();
            public UnityEvent onQueryEnd = new();
        }

        [System.Serializable]
        public class BlockchainQueryEvents
        {
            public BlockchainQueryStateEvents queryCallbacks = new();
            [InfoBox("True if the blockchain asset exists. False otherwise.")]
            public BlockchainQueryEvent onExistsOnBlockchain = new();
            [InfoBox("True if the blockchain asset is owned by this user. False otherwise.")]
            public BlockchainQueryEvent onOwnedLocally = new();

            [Space]
            public UnityEvent<string> onNFTName = new();
            public UnityEvent<Sprite> onMediaSprite = new();
        }

        [InfoBox("NOTE: Functionality will not work in the editor. It will only operate in the built version of the scene.", TriMessageType.Error)]
        [SerializeField] private bool queryOnStart = true;
        public BlockchainQueryParams parameters = new();
        public BlockchainQueryEvents queryEvents = new();

        [Header("Meta Data (Asset Query Type Only)")]
        public BlockchainMetaData assetMetaData = new();

        public bool QueryOnStart {
            get => queryOnStart;
            set => queryOnStart = value;
        }

        public string QueryString {
            get => parameters.queryString;
            set => parameters.queryString = value;
        }

        protected virtual void Start()
        {
            if (queryOnStart)
                Query();
        }

        private void OnValidate()
        {
            if (parameters.blockchainType == Common.Enumerations.BlockchainType.All ||
                parameters.blockchainType == Common.Enumerations.BlockchainType.None)
            {
                parameters.blockchainType = Common.Enumerations.BlockchainType.Cardano;
            }
        }

        public void Query()
        {
            QueryInternal();
        }

        partial void QueryInternal();
    }
}
