using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class UIPointerEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public UnityEvent onPointerDown;
        public UnityEvent onPointerUp;
        public UnityEvent onPointerClick;
        public UnityEvent onPointerEnter;
        public UnityEvent onPointerExit;

        private void OnDisable()
        {
            onPointerUp?.Invoke();
            onPointerExit?.Invoke();
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            onPointerClick?.Invoke();
        }

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
        {
            onPointerDown?.Invoke();
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            onPointerEnter?.Invoke();
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            onPointerExit?.Invoke();
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData)
        {
            onPointerUp?.Invoke();
        }
    }
}
