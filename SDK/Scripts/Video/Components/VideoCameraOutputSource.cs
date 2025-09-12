using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.Video.Abstract;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.Video.Components
{
    public partial class VideoCameraOutputSource : NetworkObjectBehaviour
    {
        [HideIf("HasRawImageSource")][DisallowNull][SerializeField] private Renderer rendererSource;
        [HideIf("HasRendererSource")][DisallowNull][SerializeField] private RawImage rawImageSource;
        [SerializeField] private VideoCameraEvents events = new();
        private bool HasRawImageSource => rawImageSource;
        private bool HasRendererSource => rendererSource;

#if !METAVERSE_CLOUD_ENGINE_INTERNAL
        protected override void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();

            var videoService = MetaSpace != null ? MetaSpace.GetService<IVideoCameraService>() : null;
            if (videoService != null)
            {
                if (rendererSource) videoService.AddSource(rendererSource, NetworkObject);
                if (rawImageSource) videoService.AddSource(rawImageSource, NetworkObject);
            }
        }
#endif
    }
}
