using System;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.SceneManagement.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class SceneManagerHelper : MetaSpaceHelper
    {
        [Button("Upgrade Component")] public new void Upgrade() => this.ReplaceScript<MetaSpaceHelper>();
    }
}