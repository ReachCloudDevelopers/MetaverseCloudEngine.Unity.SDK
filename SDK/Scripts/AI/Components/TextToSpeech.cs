using System;
using MetaverseCloudEngine.Common.Enumerations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    [Experimental]
    public partial class TextToSpeech : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class TextPrompt
        {
            [LabelText("")]
            [TextArea(minLines: 5, maxLines: 5000)] 
            public string text;
        }

        [SerializeField] private TextPrompt text = new();
        [SerializeField] private bool playOnStart;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private TextToSpeechVoicePreset voicePreset;

        [Header("Events")]
        [SerializeField] private UnityEvent onLoadingStarted = new();
        [SerializeField] private UnityEvent onLoadingFinished = new();
        [SerializeField] private UnityEvent onPlayTts = new();
        [SerializeField] private UnityEvent onPlayTtsFinished = new();
        [SerializeField] private UnityEvent onPlayTtsFailed = new();

        public string Text
        {
            get => text?.text;
            set
            {
                text ??= new TextPrompt();
                text.text = value;
            }
        }

        public bool PlayOnStart
        {
            get => playOnStart;
            set => playOnStart = value;
        }
        
        public AudioSource VoiceSource
        {
            get => voiceSource;
            set => voiceSource = value;
        }
        
        public TextToSpeechVoicePreset VoicePreset
        {
            get => voicePreset;
            set => voicePreset = value;
        }
        
        public UnityEvent OnLoadingStarted => onLoadingStarted;
        
        public UnityEvent OnLoadingFinished => onLoadingFinished;
        
        public UnityEvent OnPlayTts => onPlayTts;
        
        public UnityEvent OnPlayTtsFailed => onPlayTtsFailed;
        
        public bool IsLoading { get; private set; }
        
        private void Start()
        {
            if (playOnStart)
                Play();
        }

        public void Play()
        {
            PlayInternal();
        }
        
        partial void PlayInternal();
    }
}