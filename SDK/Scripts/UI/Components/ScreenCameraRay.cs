using System;
using MetaverseCloudEngine.Unity.Physix.Components;

using TriInspectorMVCE;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [HideMonoScript]
    public class ScreenCameraRay : Raycast
    {
        [Header("References")]
        [PropertyOrder(-1000)]
        [Required][SerializeField] private Camera raycastCamera;
        [PropertyOrder(-999)]
        [SerializeField] private RectTransform rectArea;
        
        [Header("Input")]
        [PropertyOrder(-998)]
        [HideIf(nameof(rectArea))]
        [SerializeField] private bool ignoreIfOverUI = true;
        [PropertyOrder(-997)]
        [SerializeField] private bool performOnClickOrTap = true;
        [PropertyOrder(-996)]
        [PropertySpace(SpaceAfter = 15)]
        [SerializeField, Min(0)] private int targetMouseButton;

        private bool _isClicking;
        private int _rayFrameCount;

        public Camera RaycastCamera { get => raycastCamera; set => raycastCamera = value; }

        private void Reset()
        {
            raycastCamera = GetComponentInParent<Camera>(true);
        }

        private void Update()
        {
            if (performOnClickOrTap && IsClicking())
            {
                _isClicking = true;
            }
            
            if (_isClicking && Time.timeScale < 0.1f)
            {
                _isClicking = false;
                PerformRaycast(false);
            }
        }

        private void FixedUpdate()
        {
            if (_isClicking && Time.timeScale > 0.1f)
            {
                _isClicking = false;
                PerformRaycast(false);
            }
        }

        public virtual bool IsClicking()
        {
            return Input.GetMouseButtonDown(targetMouseButton);
        }

        public override bool CanPerform()
        {
            return raycastCamera != null;
        }

        public override bool GetRay(out Ray ray)
        {
            Vector3 position = Input.mousePosition;
            if (!rectArea)
            {
                if (ignoreIfOverUI)
                {
                    // Check if the mouse is over a UI element
                    var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                    if (eventSystem != null && eventSystem.IsPointerOverGameObject())
                    {
                        ray = default;
                        return false;
                    }
                }
                
                ray = raycastCamera.ScreenPointToRay(position);
                return true;
            }

            Vector2 localPoint = rectArea.transform.InverseTransformPoint(position);
            localPoint /= rectArea.rect.size;
            localPoint += rectArea.pivot;

            position = raycastCamera.ViewportToScreenPoint(localPoint);
            ray = raycastCamera.ScreenPointToRay(position);
            return true;
        }
    }
}
