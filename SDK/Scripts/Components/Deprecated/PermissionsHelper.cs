using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class PermissionsHelper : PermissionsAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<PermissionsAPI>();
    }
}
