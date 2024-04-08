using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class MicrophoneHelper : MetaverseCloudEngine.Unity.Audio.Components.MicrophoneAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<MetaverseCloudEngine.Unity.Audio.Components.MicrophoneAPI>();
    }
}