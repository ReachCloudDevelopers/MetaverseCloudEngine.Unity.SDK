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
        [SerializeField] private float m_ZoomSpeed = 1f;
        [SerializeField] private float m_MinZoom = 0.1f;
        [SerializeField] private float m_MaxZoom = 100f;
        
        private float m_LastPinchDistance;
        private Vector2 m_LastTouchPosition;
        private bool m_MoveInitiated;
        
        /// <summary>
        /// The camera that will be used for zooming and panning.
        /// </summary>
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

            if (!Application.isMobilePlatform)
            {
                var isOverUI = MVUtils.IsPointerOverUI();
                if (Input.GetMouseButtonDown(2) && !isOverUI)
                    m_MoveInitiated = true;
                if (Input.GetMouseButton(2) && m_MoveInitiated)
                    m_Camera.transform.position -= new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0) * (m_Camera.orthographicSize * 0.01f);
                if (Input.mouseScrollDelta.y != 0 && !isOverUI)
                    m_Camera.orthographicSize = Mathf.Clamp(m_Camera.orthographicSize - Input.mouseScrollDelta.y * m_ZoomSpeed, m_MinZoom, m_MaxZoom);
                return;
            }
            
            switch (Input.touchCount)
            {
                case 1:
                {
                    var touch = Input.GetTouch(0);
                    switch (touch.phase)
                    {
                        case TouchPhase.Began:
                            m_LastTouchPosition = touch.position;
                            m_MoveInitiated = !MVUtils.IsPointerOverUI();
                            break;
                        case TouchPhase.Moved when m_MoveInitiated:
                            m_Camera.transform.position -=
                                new Vector3(
                                    touch.position.x - m_LastTouchPosition.x,
                                    touch.position.y - m_LastTouchPosition.y, 0) * (m_Camera.orthographicSize * 0.01f);
                            break;
                        case TouchPhase.Ended:
                            m_MoveInitiated = false;
                            break;
                    }

                    m_LastTouchPosition = touch.position;
                    break;
                }
                case 2:
                {
                    var touch0 = Input.GetTouch(0);
                    var touch1 = Input.GetTouch(1);
                    var pinchDistance = Vector2.Distance(touch0.position, touch1.position);
                
                    if ((touch1.phase == TouchPhase.Began ||
                         touch0.phase == TouchPhase.Began) && !m_MoveInitiated)
                    {
                        m_LastPinchDistance = pinchDistance;
                        m_MoveInitiated = !MVUtils.IsPointerOverUI();
                    }
                
                    if (touch0.phase == TouchPhase.Ended ||
                        touch1.phase == TouchPhase.Ended)
                    {
                        m_MoveInitiated = false;
                        return;
                    }
                
                    if (m_LastPinchDistance != 0 && (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved) && m_MoveInitiated)
                        m_Camera.orthographicSize = Mathf.Clamp(m_Camera.orthographicSize - (pinchDistance - m_LastPinchDistance) * m_ZoomSpeed, m_MinZoom, m_MaxZoom);
                
                    m_LastPinchDistance = pinchDistance;
                    break;
                }
                default:
                    m_MoveInitiated = false;
                    break;
            }
        }
    }
}