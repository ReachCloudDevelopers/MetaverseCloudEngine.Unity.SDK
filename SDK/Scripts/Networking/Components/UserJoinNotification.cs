using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [Experimental]
    [HideMonoScript]
    public class UserJoinNotification : TriInspectorMonoBehaviour
    {
        public string joinedFormat = "{0} just joined.";
        public string leaveFormat = "{0} left the scene.";
        public UnityEvent<string> onText;
        public float destroyDelay = 5f;

        public void Repaint(string userName, bool isJoined)
        {
            onText?.Invoke(isJoined 
                ? string.Format(joinedFormat, userName) 
                : string.Format(leaveFormat, userName));
            
            if (destroyDelay > 0)
                Destroy(gameObject, destroyDelay);
        }
    }
}