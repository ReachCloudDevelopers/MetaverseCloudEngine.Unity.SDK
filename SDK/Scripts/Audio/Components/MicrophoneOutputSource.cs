using UnityEngine;
using MetaverseCloudEngine.Unity.Networking.Components;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    [DisallowMultipleComponent]
    public partial class MicrophoneOutputSource : NetworkObjectBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private MicrophoneEvents events;

        public MicrophoneEvents Events => events;
    }
}