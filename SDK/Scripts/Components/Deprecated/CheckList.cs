using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class CheckList : MetaverseCloudEngine.Unity.Components.MultiTickBasedStateAggregator
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.Components.MultiTickBasedStateAggregator>();
    }
}