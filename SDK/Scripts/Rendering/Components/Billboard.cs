using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [DefaultExecutionOrder(int.MaxValue)]
    [HideMonoScript]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Rendering/Billboard")]
    public class Billboard : TriInspectorMonoBehaviour
    {
        public bool facePositionInVr = true;
        public bool invert;

        private bool _useLateUpdate;
        private Transform _transform;
        private static Transform _mainCamera;

        private void Awake()
        {
            _transform = transform;

            if (GetComponent<RectTransform>())
                _useLateUpdate = true;
        }

        private void LateUpdate()
        {
            if (_useLateUpdate)
                OnWillRenderObject();
        }

        private void OnWillRenderObject()
        {
            if (!_mainCamera)
            {
                var mainCam = Camera.main;
                _mainCamera = mainCam ? mainCam.transform : null;
            }
            else FaceCamera();
        }

        private void FaceCamera()
        {
            var up = _transform.root != _transform ? _transform.root.up : Vector3.up;
            if (facePositionInVr && XRSettings.isDeviceActive)
            {
                var dir = _mainCamera.position - _transform.position;
                _transform.rotation = Quaternion.LookRotation(invert ? -dir.normalized : dir.normalized, up);
                return;
            }

            _transform.rotation = Quaternion.LookRotation(invert ? _mainCamera.forward : -_mainCamera.forward, up);
        }
    }
}