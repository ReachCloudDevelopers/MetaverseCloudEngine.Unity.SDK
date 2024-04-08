using System.Collections.Generic;

using TriInspectorMVCE;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [HideMonoScript]
    public class DraggableGameObject : TriInspectorMonoBehaviour
    {
        [SerializeField] private UnityEvent onSelected;
        [SerializeField] private UnityEvent onDeselected;

        private List<DragGameObject> _draggers;

        private void Start() { /* for enabled/disabled toggle */ }

        public void SelectedBy(DragGameObject dragger)
        {
            if (_draggers != null && _draggers.Contains(dragger))
                return;
            _draggers ??= new List<DragGameObject>();
            _draggers.Add(dragger);
            if (_draggers.Count == 1)
                onSelected?.Invoke();
        }

        public void DeselectedBy(DragGameObject dragger)
        {
            if (_draggers == null)
                return;
            if (_draggers.Remove(dragger) && _draggers.Count == 0)
                onDeselected?.Invoke();
        }
    }
}
