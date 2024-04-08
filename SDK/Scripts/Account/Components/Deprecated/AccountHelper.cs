/* Copyright (C) 2024 Reach Cloud - All Rights Reserved */

using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Account.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class AccountHelper : AccountAPI
    {
        [Button("Upgrade Component")] public void Upgrade() => this.ReplaceScript<AccountAPI>();
    }
}