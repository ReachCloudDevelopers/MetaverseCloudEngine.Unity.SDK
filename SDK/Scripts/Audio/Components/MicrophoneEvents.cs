using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    [System.Serializable]
    public class MicrophoneEvents
    {
        public UnityEvent onVoiceMuted;
        public UnityEvent onVoiceUnmuted;

        public void Combine(MicrophoneEvents events)
        {
            onVoiceMuted.AddListener(() => events.onVoiceMuted?.Invoke());
            onVoiceUnmuted.AddListener(() => events.onVoiceUnmuted?.Invoke());
        }
    }
}
