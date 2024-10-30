using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    [Experimental]
    public partial class VoiceTranscriber : TriInspectorMonoBehaviour
    {
        [SerializeField] private UnityEvent onListeningStarted = new();
        [SerializeField] private UnityEvent onListeningFinished = new();
        [SerializeField] private UnityEvent<string> onListeningSucceeded = new();
        [SerializeField] private UnityEvent<string> onRealtimeResult = new();
        [SerializeField] private UnityEvent onListeningFailed = new();
        [SerializeField] private UnityEvent onRecordingStarted = new();
        [SerializeField] private UnityEvent onRecordingStopped = new();
        
        public UnityEvent OnListeningStarted => onListeningStarted;
        public UnityEvent OnListeningFinished => onListeningFinished;
        public UnityEvent<string> OnListeningSucceeded => onListeningSucceeded;
        public UnityEvent<string> OnRealtimeResult => onRealtimeResult;
        public UnityEvent OnListeningFailed => onListeningFailed;
        public UnityEvent OnRecordingStarted => onRecordingStarted;

        /// <summary>
        /// Begins listening for user from the microphone.
        /// </summary>
        public void BeginListening()
        {
            BeginListeningInternal();
        }
        
        /// <summary>
        /// Cancels the current listening session.
        /// </summary>
        public void CancelListening()
        {
            CancelInternal();
        }
        
        /// <summary>
        /// Completes the listening session and submits the user input to the
        /// <see cref="AIAgent"/>.
        /// </summary>
        public void FinishListening()
        {
            FinishListeningInternal();
        }

        partial void BeginListeningInternal();
        partial void CancelInternal();
        partial void FinishListeningInternal();
    }
}