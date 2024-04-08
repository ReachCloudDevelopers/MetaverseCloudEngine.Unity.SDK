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
            Local,
            URL,
            CoBrowse,
        }

        [Header("Behaviour")]
        [SerializeField] private string initialUrl = "https://www.google.com/";
        [SerializeField] private bool kiosk;
        [SerializeField] private bool allowInteraction = true;
        [Range(0, 1)] [SerializeField] private float volume = 0.5f;
        [InfoBox("The 'Co Browse' and 'URL' sync modes require that a network object is assigned to the 'Network Object' field.")]
        [SerializeField] private SynchronizationOption sync = SynchronizationOption.Local;
        [SerializeField] private bool forceDesktopUserAgent = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> onUrlChanged = new();

        public float Volume {
            get => volume;
            set {
                volume = value;
                SetVolume_Impl(volume);
            }
        }

        public void SetUrl(string url) => SetUrl_Impl(url);

        partial void SetUrl_Impl(string url);

        partial void SetVolume_Impl(float v);
    }
}
