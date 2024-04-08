using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [HideMonoScript]
    public class NetworkObjectOwnerName : NetworkObjectBehaviour
    {
        [LabelText("On Input Authority Name")]
        public UnityEvent<string> onOwnerName;

        public override void OnNetworkReady(bool offline) => TryUpdatePlayerName();
        public override void OnLocalStateAuthority() => TryUpdatePlayerName();
        public override void OnRemoteStateAuthority() => TryUpdatePlayerName();
        public override void OnLocalInputAuthority() => TryUpdatePlayerName();
        public override void OnRemoteInputAuthority() => TryUpdatePlayerName();

        private void TryUpdatePlayerName()
        {
            if (MetaSpaceNetworkingService == null || NetworkObject == null) return;
            MetaSpaceNetworkingService.GetPlayerName(NetworkObject.InputAuthorityID, un => { if (this) onOwnerName?.Invoke(un); }, () => { });
        }
    }
}
