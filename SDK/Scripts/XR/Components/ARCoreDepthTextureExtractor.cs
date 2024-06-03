using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.ARFoundation;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    public class ARCoreDepthTextureExtractor : TriInspectorMonoBehaviour
    {
        [Required] [SerializeField] private AROcclusionManager occlusionManager;
        [SerializeField] private UnityEvent<Texture> onDepthTexture;
        [SerializeField] private UnityEvent<Texture> onDepthTexture2d;

        private Texture _depthTexture;
        
        private void Awake()
        {
            if (!occlusionManager)
                occlusionManager = GetComponentInParent<AROcclusionManager>(true);
        }
        
        private void Update()
        {
            if (occlusionManager.environmentDepthTexture is null)
                return;
            
            if (_depthTexture != occlusionManager.environmentDepthTexture)
            {
                _depthTexture = occlusionManager.environmentDepthTexture;
                onDepthTexture?.Invoke(_depthTexture);
                onDepthTexture2d?.Invoke(_depthTexture);
            }
        }
    }
}