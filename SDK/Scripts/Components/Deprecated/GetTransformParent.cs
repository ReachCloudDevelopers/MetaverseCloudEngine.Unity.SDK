using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class GetTransformParent : GetParent
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<GetParent>();
    }
}