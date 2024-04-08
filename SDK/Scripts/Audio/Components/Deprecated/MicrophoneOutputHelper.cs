using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class MicrophoneOutputHelper : MicrophoneOutputAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MicrophoneOutputAPI>();
    }
}