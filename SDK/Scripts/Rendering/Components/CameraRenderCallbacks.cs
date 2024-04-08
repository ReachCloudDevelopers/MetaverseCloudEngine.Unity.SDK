using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class CameraRenderCallbacks : MonoBehaviour
    {
        private Camera _camera;
        
        public event Action PreRender;
        public event Action PostRender;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnPreCullUrp;
            RenderPipelineManager.endCameraRendering += OnPostRenderUrp;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnPreCullUrp;
            RenderPipelineManager.endCameraRendering -= OnPostRenderUrp;
        }

        private void OnPreCullUrp(ScriptableRenderContext ctx, Camera c)
        {
            if (_camera == c) OnPreCull();
        }
        
        private void OnPostRenderUrp(ScriptableRenderContext ctx, Camera c)
        {
            if (_camera == c) OnPostRender();
        }

        private void OnPreCull() => PreRender?.Invoke();
        private void OnPostRender() => PostRender?.Invoke();
    }
}
