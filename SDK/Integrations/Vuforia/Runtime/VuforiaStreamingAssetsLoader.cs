using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: Preserve]

namespace MetaverseCloudEngine.Unity.Vuforia
{
    [DefaultExecutionOrder(-int.MaxValue)]
    public class VuforiaStreamingAssetsLoader : MonoBehaviour
    {
        [UsedImplicitly]
        public VuforiaStreamingAssets vuforiaStreamingAssets;
        
        private void Awake()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            if (vuforiaStreamingAssets)
                vuforiaStreamingAssets.Dump();
#endif
        }

        private void OnDestroy()
        {
#if METAVERSE_CLOUD_ENGINE_INTERNAL
            // Clean out the StreamingAssets folder
            VuforiaStreamingAssets.Clear();
#endif
        }
    }
}