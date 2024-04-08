using System.Linq;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.Networking.Components
{
    [Experimental]
    [HideMonoScript]
    public class NetworkCullingSource : NetworkObjectBehaviour
    {
        public static NetworkCullingSource Current { get; private set; }

        public static bool IsCullingIdentifier(NetworkObject obj)
        {
            if (!Current) return false;
            return Current.NetworkObject == obj;
        }
        
        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            base.OnDestroy();
            if (Current == this)
            {
                Current = FindObjectsOfType<NetworkCullingSource>()
                    .FirstOrDefault(x => x != this && x.NetworkObject && x.NetworkObject.IsStateAuthority);
            }
        }

        public override void OnLocalStateAuthority()
        {
            base.OnLocalStateAuthority();

            if (Current == null)
                Current = this;
        }
    }
}