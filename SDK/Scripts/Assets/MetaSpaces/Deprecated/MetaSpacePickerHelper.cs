using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.Metaspaces
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class MetaSpacePickerHelper : MetaSpacePickerAPI
    {
        [Button("Upgrade Component")]
        public void Upgrade() => this.ReplaceScript<MetaSpacePickerAPI>();
    }
}