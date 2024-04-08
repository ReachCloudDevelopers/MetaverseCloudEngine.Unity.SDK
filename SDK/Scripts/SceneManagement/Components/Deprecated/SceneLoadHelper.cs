using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SceneManagement.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class SceneLoadHelper : LoadScene
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<LoadScene>();
    }
}