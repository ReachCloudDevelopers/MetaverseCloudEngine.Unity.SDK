#if PLAYMAKER && METAVERSE_CLOUD_ENGINE
using HutongGames.PlayMaker;
using System;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [Serializable]
    public class FsmLandPlotCloudDataRecordParams
    {
        public FsmBool appendBuildableIDToKey = true;
        public FsmBool deleteOnBuildableRemoved = true;
    }
}
#endif