#if PLAYMAKER && METAVERSE_CLOUD_ENGINE
using HutongGames.PlayMaker;
using System;

namespace MetaverseCloudEngine.Unity.Integrations.PlayMaker.Actions
{
    [Serializable]
    public class FsmCloudDataRecordEvents
    {
        [UIHint(UIHint.Variable)]
        [Tooltip("An output value to store whether or not the operation succeeded.")]
        public FsmBool storeSucceeded;
        [Tooltip("Invoked when loading/saving the record data succeeded.")]
        public FsmEvent onSuccess;
        [Tooltip("Invoked when loading/saving the record data failed.")]
        public FsmEvent onFailed;
        [Tooltip("Invoked when loading/saving the record data has finished.")]
        public FsmEvent onFinished;
    }
}
#endif