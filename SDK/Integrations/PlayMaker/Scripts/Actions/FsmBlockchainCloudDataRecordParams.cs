#if PLAYMAKER && METAVERSE_CLOUD_ENGINE
using HutongGames.PlayMaker;
using MetaverseCloudEngine.Common.Enumerations;
using System;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [Serializable]
    public class FsmBlockchainCloudDataRecordParams
    {
        public FsmString blockchainSource;
        [ObjectType(typeof(BlockchainType))]
        public FsmEnum blockchainType;
    }
}
#endif