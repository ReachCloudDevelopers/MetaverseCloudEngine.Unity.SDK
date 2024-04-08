using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    public class UIDragEvents : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public UnityEvent onBeginDrag;
        public UnityEvent onEndDrag;
        public UnityEvent onDrag;

        private void OnDisable()
        {
            onEndDrag?.Invoke();
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            onDrag?.Invoke();
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            onBeginDrag?.Invoke();
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            onEndDrag?.Invoke();
        }
    }
}
