using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class SetTransformParent : SetParent
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<SetParent>();
    }
}