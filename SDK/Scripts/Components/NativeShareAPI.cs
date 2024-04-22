using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    public partial class NativeShareAPI : TriInspectorMonoBehaviour
    {
        [InfoBox("This component is only supported on mobile devices.")]
        [SerializeField] private string text;
        [SerializeField] private UnityEvent onShareStarted;
        [SerializeField] private UnityEvent onShareFinished;
        [SerializeField] private UnityEvent onShare;
        [SerializeField] private UnityEvent onShareFailed;

        /// <summary>
        /// The text that you want to share on this platform.
        /// </summary>
        public string Text
        {
            get => text;
            set => text = value;
        }

        /// <summary>
        /// Perform the share operation.
        /// </summary>
        public void Share()
        {
            ShareInternal();
        }

        partial void ShareInternal();
    }
}
