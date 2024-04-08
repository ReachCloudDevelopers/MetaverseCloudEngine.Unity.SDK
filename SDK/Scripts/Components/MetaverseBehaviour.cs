using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    public abstract class MetaverseBehaviour : TriInspectorMonoBehaviour
    {
        private bool _initialized;

        protected virtual void Awake()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                if (MetaverseProgram.AppUpdateRequired)
                    return;

                _initialized = true;
                OnMetaverseBehaviourInitialize(MetaverseProgram.RuntimeServices);
            });
        }

        protected virtual void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
            if (!_initialized) return;
            OnMetaverseBehaviourUnInitialize();
            _initialized = false;
        }

        protected virtual void OnMetaverseBehaviourInitialize(MetaverseRuntimeServices services)
        {
        }

        protected virtual void OnMetaverseBehaviourUnInitialize()
        {
        }
    }
}