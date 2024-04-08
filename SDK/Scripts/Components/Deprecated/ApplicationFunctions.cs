using System;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class ApplicationFunctions : UnityApplicationAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<UnityApplicationAPI>();
    }
}
