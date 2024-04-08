using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.RemoteConnect.Components
{
    /// <summary>
    /// API for remote connection events.
    /// </summary>
    [HideMonoScript]
    public partial class RemoteConnectionAPI : TriInspectorMonoBehaviour
    {
        [Title("Remote Control")]
        [InfoBox("These events are invoked when this local device is (or is not) being remote controlled.")]
        [Tooltip("Invoked when this local device is being remote controlled.")]
        [InspectorName("onBeingRemoteControlled")]
        [SerializeField] private UnityEvent onRemoteControlActive;
        [Tooltip("Invoked when this local device is no longer being remote controlled.")]
        [InspectorName("onNotBeingRemoteControlled")]
        [SerializeField] private UnityEvent onRemoteControlStopped;
        
        [Title("Remote Connection")]
        [InfoBox("These events are invoked when this local device is (or is not) remote controlling another device.")]
        [Tooltip("Invoked when currently remote controlling a remote device.")]
        [InspectorName("onRemoteControlling")]
        [SerializeField] private UnityEvent onRemoteConnectionActive;
        [Tooltip("Invoked when no longer remote controlling a remote device.")]
        [InspectorName("onNotRemoteControlling")]
        [SerializeField] private UnityEvent onRemoteConnectionStopped;

        public void SetIncomingConnectionsEnabled(bool active) => SetIncomingConnectionsEnabledInternal(active);
        public void SetOutgoingConnectionsEnabled(bool active) => SetOutgoingConnectionsEnabledInternal(active);

        partial void SetIncomingConnectionsEnabledInternal(bool active);
        partial void SetOutgoingConnectionsEnabledInternal(bool active);
    }
}