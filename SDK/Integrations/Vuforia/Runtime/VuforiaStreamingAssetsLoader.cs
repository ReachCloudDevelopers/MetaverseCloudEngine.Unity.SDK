using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vuforia
{
    [DefaultExecutionOrder(-int.MaxValue)]
    public class VuforiaStreamingAssetsLoader : MonoBehaviour
    {
        private void Awake()
        {
            VuforiaStreamingAssets.Dump();
        }
    }
}