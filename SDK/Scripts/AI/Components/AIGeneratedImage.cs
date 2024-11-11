using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    /// <summary>
    /// The component that generates an image from a prompt using AI.
    /// </summary>
    [HideMonoScript]
    public partial class AIGeneratedImage : TriInspectorMonoBehaviour
    {
        [Tooltip("Whether to generate an image on start.")]
        [SerializeField] private bool generateOnStart = true;
        [Tooltip("The prompt to generate an image from.")]
        [SerializeField] private string prompt = "";
        [Tooltip("The action to invoke when the image generation starts.")]
        [SerializeField] private UnityEvent onStarted = new();
        [Tooltip("The action to invoke when the image generation finishes, either successfully or not.")]
        [SerializeField] private UnityEvent onFinished = new();
        [Tooltip("The action to invoke when the image is generated successfully.")]
        [SerializeField] private UnityEvent<Texture2D> onGenerated = new();
        [Tooltip("The action to invoke when the image is generated successfully.")]
        [SerializeField] private UnityEvent<Sprite> onGeneratedSprite = new();
        [Tooltip("The action to invoke when the image generation fails.")]
        [SerializeField] private UnityEvent<string> onFailed = new();

        /// <summary>
        /// The prompt to generate an image from.
        /// </summary>
        public string Prompt
        {
            get => prompt;
            set
            {
                if (value == prompt) return;
                prompt = value;
                Generate();
            }
        }
        
        /// <summary>
        /// The action to invoke when the image generation starts.
        /// </summary>
        public UnityEvent OnStarted => onStarted;
        /// <summary>
        /// The action to invoke when the image generation finishes, either successfully or not.
        /// </summary>
        public UnityEvent OnFinished => onFinished;
        /// <summary>
        /// The action to invoke when the image is generated successfully.
        /// </summary>
        public UnityEvent<Texture2D> OnGenerated => onGenerated;
        /// <summary>
        /// The action to invoke when the image generation fails.
        /// </summary>
        public UnityEvent<string> OnFailed => onFailed;

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        /// <summary>
        /// Generates an image from the prompt.
        /// </summary>
        public void Generate()
        {
            GenerateInternal();
        }
        
        partial void GenerateInternal();
    }
}
