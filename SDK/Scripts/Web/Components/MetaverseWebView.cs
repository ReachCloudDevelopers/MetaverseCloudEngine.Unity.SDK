#pragma warning disable CS0414
#pragma warning disable IDE0079
#pragma warning disable IDE0052

using System;

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
        
        /// <summary>
        /// Invoked when the URL changes.
        /// </summary>
        public UnityEvent<string> OnUrlChanged => onUrlChanged;
        
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
        /// Sets the URL of the web view.
        /// </summary>
        /// <param name="url">The url to apply to the webview.</param>
        public void SetUrl(string url) => SetUrl_Impl(url);

        partial void SetUrl_Impl(string url);

        partial void SetVolume_Impl(float v);
    }
}
