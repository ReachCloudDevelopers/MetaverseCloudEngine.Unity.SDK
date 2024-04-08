#if PLAYMAKER && METAVERSE_CLOUD_ENGINE
using HutongGames.PlayMaker;
using System;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [Serializable]
    public class FsmCloudDataRecordInputs
    {
        public FsmString setStringValue;
        public FsmBool setBoolValue;
        public FsmFloat setNumberValue;
    }
}
#endif