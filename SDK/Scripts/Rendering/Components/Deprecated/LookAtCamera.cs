using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class LookAtCamera : Billboard
    {
        [Button("Upgrade Component")]
        public void Upgrade() => this.ReplaceScript<Billboard>();
    }
}
