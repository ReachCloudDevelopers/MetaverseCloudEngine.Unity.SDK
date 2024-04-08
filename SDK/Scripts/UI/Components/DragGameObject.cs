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

        public Camera RaycastCamera { get => raycastCamera; set => raycastCamera = value; }

        private void Reset()
        {
            raycastCamera = GetComponentInParent<Camera>(true);
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(targetMouseButton))
                _isSelecting = true;
            else if (Input.GetMouseButtonUp(targetMouseButton))
                ReleaseSelection();
            if (_isSelecting || _simulateSelecting)
                OnSelecting();
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
                var point = ray.GetPoint(_initialDistance);
                var targetPos = point - _selectionOffset;
                _lastSelectedGameObject.transform.position = targetPos;
            }
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
                _selectionOffset = hitInfo.point - draggable.transform.position;
                _lastSelectedGameObject = draggable;
                _lastSelectedGameObject.SelectedBy(this);
                _hadSelection = true;
                onSelected?.Invoke(draggable.gameObject);
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
                _lastSelectedGameObject.DeselectedBy(this);
            _lastSelectedGameObject = null;
            _isSelecting = false;
            _simulateSelecting = false;
            _hadSelection = false;
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
