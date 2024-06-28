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
            VuforiaStreamingAssets.Dump();
        }
    }
}