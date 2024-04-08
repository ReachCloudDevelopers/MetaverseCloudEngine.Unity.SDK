using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class DestroySelf : DestroyObject
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<DestroyObject>();
    }
}