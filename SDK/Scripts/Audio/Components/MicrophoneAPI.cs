using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Audio.Abstract;
using MetaverseCloudEngine.Unity.Components;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    public class MicrophoneAPI : MetaSpaceBehaviour
    {
        public MicrophoneEvents events;

        private bool _initialized;

        private IMicrophoneService _microphoneService;
        public IMicrophoneService MicrophoneService {
            get {
                if (_microphoneService == null)
                {
                    _microphoneService = MetaSpace ? MetaSpace.GetService<IMicrophoneService>() : null;
                    InitializeMicService(_microphoneService);
                }
                return _microphoneService;
            }
        }

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            InitializeMicService(MicrophoneService);
        }

        private void InitializeMicService(IMicrophoneService microphoneService)
        {
            if (_initialized)
                return;

            if (microphoneService != null)
            {
                _initialized = true;
                microphoneService.VoiceMuteChanged += OnVoiceMuteChanged;
                OnVoiceMuteChanged(microphoneService.IsLocallyMuted);
            }
            else
                MetaverseProgram.Logger.LogWarning("Microphone Service not found in scene.");
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            base.OnDestroy();
            
            if (_microphoneService != null)
                _microphoneService.VoiceMuteChanged -= OnVoiceMuteChanged;
        }

        private void OnVoiceMuteChanged(bool value)
        {
            if (value) events?.onVoiceMuted?.Invoke();
            else events?.onVoiceUnmuted?.Invoke();
        }

        public void SetMuted(bool value)
        {
            MetaSpace.OnReady(() =>
            {
                if (MicrophoneService == null)
                {
                    MetaverseProgram.Logger.LogWarning("Cannot mute/unmute because microphone service does not exist in the scene.");
                    return;
                }

                MicrophoneService.IsLocallyMuted = value;
            });
        }
    }
}
