using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class TransformLookAt : LookAt
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<LookAt>();
    }
}