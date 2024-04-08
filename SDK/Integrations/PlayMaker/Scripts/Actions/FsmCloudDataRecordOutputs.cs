#if PLAYMAKER && METAVERSE_CLOUD_ENGINE
using HutongGames.PlayMaker;
using System;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [Serializable]
    public class FsmCloudDataRecordOutputs
    {
        [UIHint(UIHint.Variable)]
        public FsmString storeStringValue;
        [UIHint(UIHint.Variable)]
        public FsmBool storeBoolValue;
        [UIHint(UIHint.Variable)]
        public FsmFloat storeFloatValue;
        [UIHint(UIHint.Variable)]
        public FsmInt storeIntValue;
    }
}
#endif