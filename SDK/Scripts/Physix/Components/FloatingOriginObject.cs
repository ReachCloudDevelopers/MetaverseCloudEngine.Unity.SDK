using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// An object that will be shifted by the floating origin.
    /// </summary>
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Floating Origin Object")]
    [DisallowMultipleComponent]
    [HideMonoScript]
    public class FloatingOriginObject : TriInspectorMonoBehaviour
    {
        public UnityEvent onShifted;

        public static List<FloatingOriginObject> All = new();

        private void Awake()
        {
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        public void OnShifted()
        {
            onShifted?.Invoke();
        }
    }
}
