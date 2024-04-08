using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class PlatformHelper : PlatformAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<PlatformAPI>();
    }
}
