#pragma warning disable CS0414
#pragma warning disable IDE0079
#pragma warning disable IDE0052

using UnityEngine;
using UnityEngine.Events;

using MetaverseCloudEngine.Unity.Networking.Components;

using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Web.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public partial class MetaverseWebView : NetworkObjectBehaviour
    {
        public enum SynchronizationOption
        {
            [Tooltip("The web view will operate locally only.")]
            Local,
            [Tooltip("The web view will be synchronized via URL.")]
            URL,
            [Tooltip("The web view will be synchronized via Co Browse.")]
            CoBrowse,
        }

        [Header("Behaviour")]
        [DisableInPlayMode]
        [Tooltip("The initial URL of the web view.")]
        [SerializeField] private string initialUrl = "https://www.google.com/";
        [DisableInPlayMode]
        [Tooltip("If true, the web view will be in kiosk mode.")]
        [SerializeField] private bool kiosk;
        [DisableInPlayMode]
        [Tooltip("If true, the web view will allow interaction.")]
        [SerializeField] private bool allowInteraction = true;
        [Tooltip("The volume of the web view.")]
        [DisableInPlayMode]
        [Range(0, 1)] [SerializeField] private float volume = 0.5f;
        [InfoBox("The 'Co Browse' and 'URL' sync modes require that a network object is assigned to the 'Network Object' field.")]
        [Tooltip("The synchronization mode of the web view.")]
        [DisableInPlayMode]
        [SerializeField] private SynchronizationOption sync = SynchronizationOption.Local;
        [Tooltip("If true, the web view will use a desktop user agent.")]
        [DisableInPlayMode]
        [SerializeField] private bool forceDesktopUserAgent = true;

        [Header("Events")]
        [Tooltip("Invoked when the URL changes.")]
        [SerializeField] private UnityEvent<string> onUrlChanged = new();
        [Tooltip("Invoked when the web view is put into or out of fullscreen mode.")]
        [SerializeField] private UnityEvent<bool> onFullScreenChanged = new();

        /// <summary>
        /// Invoked when the URL of the web view changes.
        /// </summary>
        public UnityEvent<string> OnUrlChanged => onUrlChanged;

        /// <summary>
        /// Invoked when the web view enters or exits full-screen mode.
        /// </summary>
        public UnityEvent<bool> OnFullScreenChanged => onFullScreenChanged;
        
        /// <summary>
        /// Gets or sets the initial URL of the web view.
        /// </summary>
        public string InitialUrl {
            get => initialUrl;
            set => initialUrl = value;
        }

        /// <summary>
        /// Modifies the volume of the web view.
        /// </summary>
        public float Volume {
            get => volume;
            set {
                volume = value;
                SetVolume_Impl(volume);
            }
        }
        
        /// <summary>
        /// The most recently loaded URL of the web view.
        /// </summary>
        public string CurrentlyLoadedUrl { get; private set; }

        /// <summary>
        /// Sets the URL of the web view.
        /// </summary>
        /// <param name="url">The url to apply to the webview.</param>
        public void SetUrl(string url) => SetUrl_Impl(url);
        
        /// <summary>
        /// Set the full-screen state of the web view.
        /// </summary>
        /// <param name="value">Whether to set the web view to full screen.</param>
        public void FullScreen(bool value) => FullScreen_Impl(value);

        partial void SetUrl_Impl(string url);

        partial void SetVolume_Impl(float v);
        
        partial void FullScreen_Impl(bool value);
    }
}
