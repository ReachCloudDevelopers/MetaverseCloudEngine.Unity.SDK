using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class PlayerInputFlags : MetaverseCloudEngine.Unity.Inputs.Components.PlayerInputAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.Inputs.Components.PlayerInputAPI>();
    }
}