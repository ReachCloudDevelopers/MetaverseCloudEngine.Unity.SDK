using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SceneManagement.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class MetaSpaceHelper : MetaSpaceAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<MetaSpaceAPI>();
    }
}