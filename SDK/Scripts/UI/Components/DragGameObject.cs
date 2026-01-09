using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [HideMonoScript]
    public class DragGameObject : TriInspectorMonoBehaviour
    {
        [InfoBox("This does not work on UI elements. This is specifically for 3D/2D objects in the scene.")]
        [Header("References")]
        [Required] [SerializeField] private Camera raycastCamera;
        [SerializeField] private RectTransform rectArea;

        [Header("Input")]
        [SerializeField, Min(0)] private int targetMouseButton;
        [SerializeField, Min(0f)] private float screenRayBlockDuration = 0.05f;

        [Header("Raycast Filter")]
        [SerializeField] private LayerMask layerMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float maxDistance = Mathf.Infinity;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal;

        [Header("Events")]
        [SerializeField] private UnityEvent<GameObject> onSelected;
        [SerializeField] private UnityEvent onDeslected;

        private float _initialDistance;
        private bool _isSelecting;
        private bool _hadSelection;
        private bool _simulateSelecting;
        private DraggableGameObject _lastSelectedGameObject;
        private Vector3 _selectionOffset;
        private Vector3 _initialPlanePoint;
        private Vector3 _initialPlaneNormal;

        public Camera RaycastCamera { get => raycastCamera; set => raycastCamera = value; }

        public static DraggableGameObject Current { get; private set; }
        public static float ScreenRayBlockedUntil { get; private set; }

        private void Reset()
        {
            raycastCamera = GetComponentInParent<Camera>(true);
            SyncLayerMaskWithCamera();
        }

        private void OnValidate()
        {
            SyncLayerMaskWithCamera();
        }

        private void SyncLayerMaskWithCamera()
        {
            if (raycastCamera != null)
            {
                layerMask = raycastCamera.cullingMask;
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(targetMouseButton))
            {
                if (Current == null)
                {
                    _isSelecting = true;
                }
            }
            else if (Input.GetMouseButtonUp(targetMouseButton))
            {
                ReleaseSelection();
            }

            if (_isSelecting || _simulateSelecting)
            {
                OnSelecting();
            }
        }

        private void OnDisable()
        {
            if (MetaverseProgram.IsQuitting) return;
            ReleaseSelection();
        }

        private void OnSelecting()
        {
            if (!raycastCamera)
            {
                if (_hadSelection)
                    ReleaseSelection();
                return;
            }

            if (!_lastSelectedGameObject)
            {
                if (_hadSelection)
                {
                    ReleaseSelection();
                    return;
                }

                FindGameObjectToSelect();

                if (!_lastSelectedGameObject)
                {
                    ReleaseSelection();
                    return;
                }
            }

            if (GetRay(out var ray))
            {
                var targetPos = CalculateDragPosition(ray, _lastSelectedGameObject);
                _lastSelectedGameObject.transform.position = targetPos;
            }
        }

        private Vector3 CalculateDragPosition(Ray ray, DraggableGameObject draggable)
        {
            var dragPlane = draggable.DragPlane;

            switch (dragPlane)
            {
                case DragPlane.Camera:
                    // Use camera facing plane
                    var point = ray.GetPoint(_initialDistance);
                    return point - _selectionOffset;

                case DragPlane.WorldUp:
                case DragPlane.WorldForward:
                case DragPlane.WorldRight:
                case DragPlane.LocalUp:
                case DragPlane.LocalForward:
                case DragPlane.LocalRight:
                    // Use the stored initial plane point and normal
                    var plane = new Plane(_initialPlaneNormal, _initialPlanePoint);
                    if (plane.Raycast(ray, out var enter))
                    {
                        var intersectionPoint = ray.GetPoint(enter);
                        return intersectionPoint - _selectionOffset;
                    }
                    break;
            }

            // Fallback to camera plane if raycast fails
            var fallbackPoint = ray.GetPoint(_initialDistance);
            return fallbackPoint - _selectionOffset;
        }

        private void FindGameObjectToSelect()
        {
            if (!GetRay(out var ray))
                return;

            if (!Physics.Raycast(ray, out var hitInfo, maxDistance, layerMask, triggerInteraction))
                return;

            var draggable = hitInfo.transform.GetComponentInParent<DraggableGameObject>();
            if (draggable && draggable.isActiveAndEnabled)
            {
                _initialDistance = hitInfo.distance;
                _lastSelectedGameObject = draggable;
                Current = draggable;
                
                // Store initial plane point and normal based on drag plane type
                _initialPlanePoint = draggable.transform.position;
                _initialPlaneNormal = GetPlaneNormal(draggable);
                
                // Calculate selection offset and project it onto the plane
                // This ensures the object maintains its distance from the plane
                var rawOffset = hitInfo.point - draggable.transform.position;
                _selectionOffset = Vector3.ProjectOnPlane(rawOffset, _initialPlaneNormal);
                
                _lastSelectedGameObject.SelectedBy(this);
                _hadSelection = true;
                onSelected?.Invoke(draggable.gameObject);
            }
        }

        private Vector3 GetPlaneNormal(DraggableGameObject draggable)
        {
            switch (draggable.DragPlane)
            {
                case DragPlane.Camera:
                    return raycastCamera.transform.forward;
                
                case DragPlane.WorldUp:
                    return Vector3.up;
                
                case DragPlane.WorldForward:
                    return Vector3.forward;
                
                case DragPlane.WorldRight:
                    return Vector3.right;
                
                case DragPlane.LocalUp:
                    return draggable.transform.parent 
                        ? draggable.transform.parent.up 
                        : Vector3.up;
                
                case DragPlane.LocalForward:
                    return draggable.transform.parent 
                        ? draggable.transform.parent.forward 
                        : Vector3.forward;
                
                case DragPlane.LocalRight:
                    return draggable.transform.parent 
                        ? draggable.transform.parent.right 
                        : Vector3.right;
                
                default:
                    return raycastCamera.transform.forward;
            }
        }

        public void SimulateSelection()
        {
            _simulateSelecting = true;
        }

        public void ReleaseSelection()
        {
            if (!_isSelecting && !_simulateSelecting && !_lastSelectedGameObject)
                return;
            if (_lastSelectedGameObject)
            {
                _lastSelectedGameObject.DeselectedBy(this);
                if (Current == _lastSelectedGameObject)
                {
                    Current = null;
                }
            }
            _lastSelectedGameObject = null;
            _isSelecting = false;
            _simulateSelecting = false;
            _hadSelection = false;

            if (screenRayBlockDuration > 0f)
            {
                var blockedUntil = Time.unscaledTime + screenRayBlockDuration;
                if (blockedUntil > ScreenRayBlockedUntil)
                    ScreenRayBlockedUntil = blockedUntil;
            }

            onDeslected?.Invoke();
        }

        private bool GetRay(out Ray ray)
        {
            var position = Input.mousePosition;
            if (!rectArea)
            {
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
