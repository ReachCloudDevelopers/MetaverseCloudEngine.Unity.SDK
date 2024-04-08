using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This component is used to detect mouse enter event on a game object.
    /// This only works on the PC platform, and is not for UI.
    /// </summary>
    [HideMonoScript]
    public class OnMouseEnterEvent : TriInspectorMonoBehaviour
    {
        [InfoBox("This only works on the PC platform, and is not for UI.")]
        public bool blockedByUI = true;
        public UnityEvent onMouseEnter;

        private void OnMouseEnter()
        {
            if (blockedByUI && MVUtils.IsPointerOverUI())
                return;

            onMouseEnter?.Invoke();
        }
    }
}