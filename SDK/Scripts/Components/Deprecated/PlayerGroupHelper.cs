﻿using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class PlayerGroupHelper : PlayerGroupAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<PlayerGroupAPI>();
    }
}