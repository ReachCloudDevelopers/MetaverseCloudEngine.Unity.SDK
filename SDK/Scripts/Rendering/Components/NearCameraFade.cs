using System;
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    /// <summary>
    /// Fades the object out when the camera is near it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Rendering/Near Camera Fade")]
    [HideMonoScript]
    [DefaultExecutionOrder(int.MaxValue)]
    public class NearCameraFade : TriInspectorMonoBehaviour
    {
        private static readonly List<NearCameraFade> NearCameraTesters = new();
        private static Transform _mainCamera;

        [SerializeField] private Renderer[] renderers;
        [SerializeField] private float distance = 0.1f;

        [Tooltip("A multiplier to add to the 'Distance' value after hidden to prevent flickering.")] [SerializeField]
        private float hiddenDistanceMultiplier = 2;

        [SerializeField] private Collider bounds;

        private bool _isHidden;
        private static CameraRenderCallbacks _renderCallbacks;

        private void Start()
        {
            if (bounds is MeshCollider { convex: false }) 
                enabled = false;
        }

        private void OnEnable()
        {
            NearCameraTesters.Add(this);

            if (_renderCallbacks)
            {
                _renderCallbacks.PreRender += OnMainCameraPreRender;
                _renderCallbacks.PostRender += OnMainCameraPostRender;
            }
        }

        private void OnDisable()
        {
            UnHide();

            NearCameraTesters.Remove(this);

            if (NearCameraTesters.Count == 0)
                _mainCamera = null;

            if (_renderCallbacks)
            {
                _renderCallbacks.PreRender -= OnMainCameraPreRender;
                _renderCallbacks.PostRender -= OnMainCameraPostRender;
            }
        }

        private void Update()
        {
            TestPositionNow();
        }

        public void FindRenderers()
        {
            UnHide();
            renderers = null;
            GetRenderers();
            TestPositionNow();
        }

        public void UnHide()
        {
            if (!_isHidden)
                return;

            if (renderers.Length == 0)
                return;

            UnHideAllRenderers();

            _isHidden = false;
        }

        public void Hide()
        {
            if (!enabled)
                return;

            if (_isHidden)
                return;

            GetRenderers();

            if (renderers.Length == 0)
                return;

            _isHidden = true;
        }

        private void TestPositionNow()
        {
            FindMainCamera();

            if (!_mainCamera)
                return;

            var mainCameraPosition = _mainCamera.position;
            var nearestPoint = bounds.ClosestPoint(mainCameraPosition);
            var currentDistance = Vector3.Distance(nearestPoint, mainCameraPosition);
            if (currentDistance < distance)
                Hide();
            else if (_isHidden && currentDistance > distance * hiddenDistanceMultiplier)
                UnHide();
        }

        private void GetRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void FindMainCamera()
        {
            if (NearCameraTesters.Count == 0)
                return;

            if (NearCameraTesters[0] != this || _mainCamera)
                return;

            var mainCam = Camera.main;
            if (!mainCam)
                return;

            _mainCamera = mainCam.transform;

            if (!_renderCallbacks)
                _renderCallbacks = _mainCamera.gameObject.AddComponent<CameraRenderCallbacks>();

            _renderCallbacks.PreRender += OnMainCameraPreRender;
            _renderCallbacks.PostRender += OnMainCameraPostRender;
        }

        private void OnMainCameraPreRender()
        {
            HideAllRenderers();
        }

        private void OnMainCameraPostRender()
        {
            UnHideAllRenderers();
        }

        private void UnHideAllRenderers()
        {
            if (renderers is not { Length: > 0 }) return;
            for (var i = renderers.Length - 1; i >= 0; i--)
            {
                if (renderers[i])
                    renderers[i].shadowCastingMode = ShadowCastingMode.On;
            }
        }

        private void HideAllRenderers()
        {
            if (!_isHidden)
                return;

            if (renderers is not { Length: > 0 }) return;
            for (var i = renderers.Length - 1; i >= 0; i--)
            {
                if (renderers[i])
                    renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }
    }
}