using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class BoolCombine : MetaverseCloudEngine.Unity.Components.GameObjectBooleanStateAggregator
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.Components.GameObjectBooleanStateAggregator>();
    }
}