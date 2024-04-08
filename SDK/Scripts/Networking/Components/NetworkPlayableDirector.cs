using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Playables;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [HideMonoScript]
    [RequireComponent(typeof(PlayableDirector))]
    public class NetworkPlayableDirector : NetworkObjectBehaviour
    {
        [Range(0.1f, 10f)] public float maxDelay = 0.5f;
    }
}