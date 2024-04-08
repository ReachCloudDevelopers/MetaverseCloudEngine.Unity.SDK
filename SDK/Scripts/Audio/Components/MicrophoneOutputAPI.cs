using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    public partial class MicrophoneOutputAPI : TriInspectorMonoBehaviour
    {
        [SerializeField] private MicrophoneOutputSource voiceOutput;
        [SerializeField] private MicrophoneEvents events;

        public MicrophoneEvents Events => events;

        private void Reset()
        {
            FindVoiceOutput();
        }

        private void FindVoiceOutput()
        {
            if (voiceOutput == null)
                voiceOutput = gameObject.AddComponent<MicrophoneOutputSource>();
        }
    }
}
