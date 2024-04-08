using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace MetaverseCloudEngine.Unity.Inputs
{
    /// <summary>
    /// A stick control displayed on screen and moved around by touch or other pointer
    /// input.
    /// </summary>
    [AddComponentMenu("Input/On-Screen Drag")]
    public class OnScreenDrag : OnScreenControl, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        [Min(0)] public float multiplier = 1f;
        public bool preserveDragDelta;

        private Vector2 _lastDragPos;

        [InputControl(layout = "Vector2")]
        [SerializeField]
        private string m_ControlPath;

        protected override string controlPathInternal
        {
            get => m_ControlPath;
            set => m_ControlPath = value;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StopDrag();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopDrag();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));

            if (_lastDragPos == Vector2.zero)
            {
                _lastDragPos = eventData.position;
                return;
            }

            var delta = eventData.position - _lastDragPos;
            SendValueToControl(delta * multiplier);
            if (!preserveDragDelta)
                _lastDragPos = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            StopDrag();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StopDrag();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastDragPos = Vector2.zero;
        }

        private void StopDrag()
        {
            _lastDragPos = Vector2.zero;
            SendValueToControl(Vector2.zero);
        }
    }
}
