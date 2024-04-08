using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class CursorLockHelper : CursorLockAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<CursorLockAPI>();
    }
}
