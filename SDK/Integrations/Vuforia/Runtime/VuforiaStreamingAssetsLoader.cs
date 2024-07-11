using JetBrains.Annotations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: Preserve]

namespace MetaverseCloudEngine.Unity.Vuforia
{
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-int.MaxValue)]
    [HideMonoScript]
    public class VuforiaStreamingAssetsLoader : MonoBehaviour
    {
        [UsedImplicitly]
        public VuforiaStreamingAssets vuforiaStreamingAssets;

        private bool _initialized;
        
        private void Awake()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (_initialized)
                return;
            
            if (vuforiaStreamingAssets)
                vuforiaStreamingAssets.Dump();
            
            MetaverseProgram.Logger.Log("VuforiaStreamingAssetsLoader: Awake()");

            _initialized = true;
#endif
        }

        private void OnDestroy()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            // Clean out the StreamingAssets folder
            VuforiaStreamingAssets.Clear();
#endif
        }

        public void RunOnAwake()
        {
            Awake();
        }
    }
}