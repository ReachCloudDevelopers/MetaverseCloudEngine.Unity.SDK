using System;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public class VolumeControl : MonoBehaviour
    {
        private static float _voiceVolume;
        private static float _gameVolume;

        public UnityEvent<float> onGameVolumeChanged;
        public UnityEvent<float> onVoiceVolumeChanged;

        public static event Action<float> GameVolumeChanged;
        public static event Action<float> VoiceVolumeChanged;

        public static float VoiceVolume
        {
            get => _voiceVolume;
            set {
                value = Mathf.Clamp01(value);
                if (Math.Abs(_voiceVolume - value) <= Mathf.Epsilon)
                    return;
                _voiceVolume = value;
                MetaverseProgram.Prefs.SetFloat(nameof(VoiceVolume), value);
                VoiceVolumeChanged?.Invoke(value);
            }
        }

        public static float GameVolume {
            get => AudioListener.volume;
            set {
                value = Mathf.Clamp01(value);
                if (Math.Abs(_gameVolume - value) <= Mathf.Epsilon)
                    return;
                _gameVolume = value;
                AudioListener.volume = value;
                MetaverseProgram.Prefs.SetFloat(nameof(GameVolume), value);
                GameVolumeChanged?.Invoke(value);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            MetaverseProgram.OnInitialized(() =>
            {
                GameVolume = AudioListener.volume = MetaverseProgram.Prefs.GetFloat(nameof(GameVolume), 1f);
                VoiceVolume = _voiceVolume = MetaverseProgram.Prefs.GetFloat(nameof(VoiceVolume), 1f); 
            });
        }

        private void OnEnable()
        {
            onGameVolumeChanged?.Invoke(GameVolume);
            onVoiceVolumeChanged?.Invoke(VoiceVolume);

            GameVolumeChanged += OnGameVolumeChanged;
            VoiceVolumeChanged += OnVoiceVolumeChanged;
        }

        private void OnDisable()
        {
            GameVolumeChanged -= OnGameVolumeChanged;
            VoiceVolumeChanged -= OnVoiceVolumeChanged;
        }

        private void OnGameVolumeChanged(float volume) => onGameVolumeChanged?.Invoke(volume);

        private void OnVoiceVolumeChanged(float volume) => onVoiceVolumeChanged?.Invoke(volume);

        public void SetVoiceVolume(float volume) => VoiceVolume = volume;

        public void SetGameVolume(float volume) => GameVolume = volume;
    }
}
