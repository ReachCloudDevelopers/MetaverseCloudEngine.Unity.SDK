using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// This component is used to detect mouse down event on a game object.
    /// This only works on the PC platform, and is not for UI.
    /// </summary>
    [HideMonoScript]
    public class OnMouseDownEvent : TriInspectorMonoBehaviour
    {
        [InfoBox("This only works on the PC platform, and is not for UI.")]
        public bool blockedByUI = true;
        public UnityEvent onMouseDown;

        private void OnMouseDown()
        {
            if (blockedByUI && MVUtils.IsPointerOverUI())
                return;
            onMouseDown?.Invoke();
        }
    }
}