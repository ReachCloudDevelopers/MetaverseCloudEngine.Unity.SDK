using UnityEngine;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [RequireComponent(typeof(Camera))]
    public class CameraCopy : MonoBehaviour
    {
        [SerializeField] private Camera target;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (!_camera || !target)
                enabled = false;
        }

        private void OnEnable() => RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;

        private void OnDisable() => RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;

        private void OnBeginFrameRendering(ScriptableRenderContext context,Camera[] cameras) => OnPreRender();

        private void OnPreRender()
        {
            _camera.fieldOfView = target.fieldOfView;
            _camera.orthographicSize = target.orthographicSize;
        }
    }
}