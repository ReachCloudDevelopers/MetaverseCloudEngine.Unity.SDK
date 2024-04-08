using UnityEngine;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(Canvas))]
    public class CanvasReferenceMainCamera : MonoBehaviour
    {
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvas.worldCamera = Camera.main;
        }

        private void Update()
        {
            if (!_canvas.worldCamera)
                _canvas.worldCamera = Camera.main;
        }
    }
}