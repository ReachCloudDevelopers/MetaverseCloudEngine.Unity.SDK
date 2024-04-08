/* Copyright (C) 2024 Reach Cloud - All Rights Reserved */

using TriInspectorMVCE;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class LoginHelper : LogInAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<LogInAPI>();
    }
}
