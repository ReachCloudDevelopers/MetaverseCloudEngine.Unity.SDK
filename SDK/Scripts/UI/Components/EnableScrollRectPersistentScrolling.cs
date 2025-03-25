using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    /// <summary>
    /// This component is used to fix the issue where the scroll rect does not scroll
    /// when the mouse is over a child element.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    [HideMonoScript]
    public class EnableScrollRectPersistentScrolling : TriInspectorMonoBehaviour
    {
        private ScrollRect _scrollRect;

        private void Awake()
        {
            if (!_scrollRect)
                _scrollRect = GetComponent<ScrollRect>();
            var controls = GetComponentsInChildren<IPointerEnterHandler>(true);
            foreach (var control in controls)
            {
                var component = (control as Component)?.gameObject.AddComponent<PropogateScrollRectEvents>();
                if (component)
                    component.scrollRect = _scrollRect;
            }
        }
        
        /// <summary>
        /// This component is used to propogate the scroll event to the parent scroll rect.
        /// </summary>
        [AddComponentMenu("")]
        public class PropogateScrollRectEvents : MonoBehaviour, IScrollHandler
        {
            public ScrollRect scrollRect;

            public void OnScroll(PointerEventData eventData)
            {
                scrollRect.OnScroll(eventData);
            }
        }
    }
}