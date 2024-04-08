using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Networking.Components;

using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.Video.Components
{
    public partial class VideoCameraOutputSource : NetworkObjectBehaviour
    {
        [HideIf("HasRawImageSource")] [DisallowNull] [SerializeField] private Renderer rendererSource;
        [HideIf("HasRendererSource")] [DisallowNull] [SerializeField] private RawImage rawImageSource;
        [SerializeField] private VideoCameraEvents events = new();
        private bool HasRawImageSource => rawImageSource;
        private bool HasRendererSource => rendererSource;
    }
}
