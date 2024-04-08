using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Video;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [Experimental]
    [HideMonoScript]
    [RequireComponent(typeof(VideoPlayer))]
    public class NetworkVideoPlayer : NetworkObjectBehaviour
    {
        [Range(0.1f, 10f)] public float maxDelay = 0.5f;
    }
}