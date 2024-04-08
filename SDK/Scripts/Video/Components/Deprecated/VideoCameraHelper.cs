using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Video.Components
{
    [AddComponentMenu(MetaverseConstants.DeprecatedComponent)]
    public class VideoCameraHelper : VideoCameraAPI
    {
        [Button("Upgrade")] public void UpgradeComponent() => this.ReplaceScript<VideoCameraAPI>();
    }
}