using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class Texture2DPickerHelper : MetaverseCloudEngine.Unity.Components.Texture2DPickerAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.Components.Texture2DPickerAPI>();
    }
}