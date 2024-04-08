using MetaverseCloudEngine.Common.Enumerations;
using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Blockchain.Components
{
    [Serializable]
    public class BlockchainQueryParams
    {
        public enum QueryType
        {
            Asset,
            Category
        }

        public string queryString;
        public bool convertToHex;
        public QueryType queryType;
        [Header("Blockchain Types 'All' and 'None' are not supported.")]
        public BlockchainType blockchainType;

        [Header("Formatting")]
        public string queryStringFormat = "{0}";
    }
}
