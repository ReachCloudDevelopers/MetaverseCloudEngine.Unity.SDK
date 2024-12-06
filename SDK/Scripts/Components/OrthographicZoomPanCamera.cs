using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class OrthographicZoomPanCamera : TriInspectorMonoBehaviour
    {
        [Tooltip("The camera that will be used for zooming and panning.")]
        [Required, SerializeField] private Camera m_Camera;
        [Tooltip("The speed at which the camera will zoom in and out.")]
        [Min(0)]
        [SerializeField] private float m_ZoomSpeed = 1f;
        [Min(0)]
        [SerializeField] private float m_MinZoom = 0.1f;
        [Min(0)]
        [SerializeField] private float m_MaxZoom = 100f;
        [Tooltip("The speed at which the camera will pan.")]
        [Range(0, 5)]
        [SerializeField] private float m_PanSpeed = 1f;
        [Range(0, 2)]
        [SerializeField] private int m_PanMouseButton = 1;

        private Vector2 m_LastTouchPosition;
        private bool m_Initiated;

        public Camera Camera
        {
            get => m_Camera;
            set => m_Camera = value;
        }

        private void Start()
        {
            if (m_Camera) return;
            MetaverseProgram.Logger.LogError("Camera is not set.");
            enabled = false;
        }

        private void Update()
        {
            if (!m_Camera)
            {
                MetaverseProgram.Logger.LogError("Camera is not set.");
                enabled = false;
                return;
            }

            if (!m_Camera.orthographic)
            {
                m_Initiated = false;
                return;
            }

            if (!UnityEngine.Device.Application.isMobilePlatform)
            {
                HandleMouseInput();
            }
            else
            {
                HandleTouchInput();
            }
        }

        private void HandleMouseInput()
        {
            var isOverUI = MVUtils.IsPointerOverUI();
            if (Input.GetMouseButtonDown(m_PanMouseButton) && !isOverUI)
                m_Initiated = true;

            if (Input.GetMouseButton(m_PanMouseButton) && m_Initiated)
            {
                Vector3 pan = new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0) * (m_Camera.orthographicSize * m_PanSpeed);
                pan = m_Camera.transform.rotation * pan;
                m_Camera.transform.position -= pan;
            }

            if (Input.GetMouseButtonUp(m_PanMouseButton))
                m_Initiated = false;

            if (Input.mouseScrollDelta.y != 0 && (!isOverUI || m_Initiated))
                m_Camera.orthographicSize = Mathf.Clamp(m_Camera.orthographicSize - Input.mouseScrollDelta.y * m_ZoomSpeed, m_MinZoom, m_MaxZoom);
        }

        private void HandleTouchInput()
        {
            switch (Input.touchCount)
            {
                case 1:
                    HandleSingleTouch();
                    break;
                case 2:
                    HandlePinchToZoom();
                    break;
                default:
                    m_Initiated = false;
                    break;
            }
        }

        private void HandleSingleTouch()
        {
            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    m_LastTouchPosition = touch.position;
                    m_Initiated = !MVUtils.IsPointerOverUI();
                    break;

                case TouchPhase.Moved when m_Initiated:
                    Vector3 pan = new Vector3(
                        touch.position.x - m_LastTouchPosition.x,
                        touch.position.y - m_LastTouchPosition.y, 0) * (m_Camera.orthographicSize * m_PanSpeed);
                    pan = m_Camera.transform.rotation * pan;
                    m_Camera.transform.position -= pan;
                    m_LastTouchPosition = touch.position;
                    break;

                case TouchPhase.Ended:
                    m_Initiated = false;
                    break;
            }
        }

        private void HandlePinchToZoom()
        {
            var touch0 = Input.GetTouch(0);
            var touch1 = Input.GetTouch(1);
            var pinchDistance = Vector2.Distance(touch0.position, touch1.position);

            if (touch1.phase == TouchPhase.Began || touch0.phase == TouchPhase.Began)
            {
                m_LastTouchPosition = Vector2.zero;
                m_Initiated = !MVUtils.IsPointerOverUI();
                return;
            }

            if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                float delta = (Vector2.Distance(touch0.position, touch1.position) - pinchDistance) * m_ZoomSpeed;
                m_Camera.orthographicSize = Mathf.Clamp(m_Camera.orthographicSize - delta, m_MinZoom, m_MaxZoom);
            }
        }
    }
}
