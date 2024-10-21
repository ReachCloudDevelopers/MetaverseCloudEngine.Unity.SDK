using System;
using System.Collections.Generic;
using MetaverseCloudEngine.Common.Enumerations;
using UnityEngine;
using TriInspectorMVCE;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    /// <summary>
    /// The <see cref="AIAgent"/> component is used to define a general purpose artificial intelligence system.
    /// The engine will take the information from this component and use it to generate outputs and actions.
    /// </summary>
    [HideMonoScript]
    [DisallowMultipleComponent]
    [Experimental]
    public partial class AIAgent : TriInspectorMonoBehaviour
    {
        [Serializable]
        public class AIPrompt
        {
            [LabelText("")]
            [TextArea(minLines: 5, maxLines: 5000)] 
            public string text;
        }
        
        [Serializable]
        public class AIAgentAction
        {
            [Required]
            public string actionID;
            [TextArea]
            [Required]
            public string description;
            [Tooltip("If true, this is an action that is specifically for object targets.")]
            public bool requireTarget;
            public UnityEvent onAction;
            [ShowIf(nameof(requireTarget))]
            [FormerlySerializedAs("onActionGameObject")] public UnityEvent<GameObject> onActionTarget;

            public IEnumerable<AIAgentAwareObject> FindSupportedTargets()
            {
                return AIAgentAwareObject.FindAll(actionID);
            }
        }

        [Required] [SerializeField] private string id;
        [SerializeField] private AiCharacterIntelligencePreset intelligencePreset;
        
        [HideIf(nameof(UsingDeprecatedFields))]
        [SerializeField] private AIPrompt prompt = new();

        [ShowIf(nameof(UsingDeprecatedFields))]
        [Title("Context")]
        [LabelText("Persona")]
        [Tooltip("Information about the personality / role of the agent.")]
        [TextArea(minLines: 5, maxLines: 100)] [SerializeField] private string personality;
        [ShowIf(nameof(UsingDeprecatedFields))]
        [LabelText("Background / Priming")]
        [Tooltip("Background knowledge that the agent has access to.")]
        [TextArea(minLines: 5, maxLines: 100)] [SerializeField] private string backStory;
        [ShowIf(nameof(UsingDeprecatedFields))]
        [LabelText("Context / Additional Details")]
        [Tooltip("Information about the surrounding environment, and any additional details that the agent should use in their decision making.")]
        [TextArea(minLines: 5, maxLines: 100)] [SerializeField] private string contextualKnowledge;
        
        [Title("Actions & Events")]
        [SerializeField] private UnityEvent onThinkingStarted;
        [SerializeField] private UnityEvent onThinkingFinished;
        [SerializeField] private UnityEvent<string> onResponse;
        [SerializeField] private UnityEvent onVoiceComplete = new();
        [SerializeField] private UnityEvent onResponseFailed;
        [SerializeField] private List<AIAgentAction> actions;
        
        [Title("Text to Speech")]
        [SerializeField] private bool deferActionsUntilVoiceComplete;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private TextToSpeechVoicePreset voicePreset;
        
        public bool UsingDeprecatedFields => !string.IsNullOrEmpty(personality) || !string.IsNullOrEmpty(backStory) || !string.IsNullOrEmpty(contextualKnowledge);

        public string Id => id;
        
        public UnityEvent OnThinkingStarted => onThinkingStarted;
        public UnityEvent OnThinkingFinished => onThinkingFinished;
        public UnityEvent<string> OnResponse => onResponse;
        public UnityEvent OnResponseFailed => onResponseFailed;
        
        public List<AIAgentAction> Actions => actions;
        
        public AiCharacterIntelligencePreset IntelligencePreset 
        {
            get => intelligencePreset;
            set => intelligencePreset = value;
        }
        
        /// <summary>
        /// The prompt that the AI agent will use to generate responses.
        /// </summary>
        public string Prompt 
        {
            get => prompt.text;
            set => prompt.text = value;
        }

        [Obsolete("Use 'Prompt' instead.")]
        public string Personality
        {
            get => personality;
            set
            {
                personality = value;
                FlushMemory();
            }
        }

        [Obsolete("Use 'Prompt' instead.")]
        public string BackStory
        {
            get => backStory;
            set
            {
                backStory = value;
                FlushMemory();
            }
        }

        [Obsolete("Use 'Prompt' instead.")]
        public string ContextualKnowledge
        {
            get => contextualKnowledge;
            set
            {
                contextualKnowledge = value;
                FlushMemory();
            }
        }

        private void OnValidate()
        {
            if (!Guid.TryParse(id, out _) && !gameObject.IsPrefab()) 
                id = Guid.NewGuid().ToString();
        }

        private void Start() { /* for enabled / disabled toggle. */ }

        /// <summary>
        /// Starts the AI agent's thought process.
        /// </summary>
        public void FlushMemory()
        {
            id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Issues user input to the AI agent.
        /// </summary>
        /// <param name="input">The user input.</param>
        public void SubmitUserInput(string input)
        {
            SubmitDialogInternal(input);
        }
        
        partial void SubmitDialogInternal(string userInput);

        /// <summary>
        /// Cancels the current thought process.
        /// </summary>
        public void Cancel()
        {
            CancelInternal();
        }
        
        partial void CancelInternal();
    }
}