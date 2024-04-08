using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class XRRecenterHelper : MetaverseCloudEngine.Unity.XR.Components.XRRecenterAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.XR.Components.XRRecenterAPI>();
    }
}