using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class XRFakeRecenter : MetaverseCloudEngine.Unity.XR.Components.XRSimulatedRecenter
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.XR.Components.XRSimulatedRecenter>();
    }
}