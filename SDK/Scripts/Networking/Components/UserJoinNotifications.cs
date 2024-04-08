using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [Experimental]
    [HideMonoScript]
    public partial class UserJoinNotifications : TriInspectorMonoBehaviour
    {
        public UserJoinNotification notificationPrefab;
        public RectTransform notificationContainer;
    }
}