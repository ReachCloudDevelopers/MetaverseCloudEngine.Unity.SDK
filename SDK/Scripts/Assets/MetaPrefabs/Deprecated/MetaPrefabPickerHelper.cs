using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public partial class MetaPrefabPickerHelper : MetaPrefabPickerAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<MetaPrefabPickerAPI>();
    }
}