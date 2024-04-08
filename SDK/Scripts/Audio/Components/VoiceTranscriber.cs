using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    [Experimental]
    public partial class VoiceTranscriber : TriInspectorMonoBehaviour
    {
        [SerializeField] private UnityEvent onListeningStarted;
        [SerializeField] private UnityEvent onListeningFinished;
        [SerializeField] private UnityEvent<string> onListeningSucceeded;
        [SerializeField] private UnityEvent onListeningFailed;
        [SerializeField] private UnityEvent onRecordingStarted;
        [SerializeField] private UnityEvent onRecordingStopped;
        
        public UnityEvent OnListeningStarted => onListeningStarted;
        public UnityEvent OnListeningFinished => onListeningFinished;
        public UnityEvent<string> OnListeningSucceeded => onListeningSucceeded;
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