using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Builder.Components
{
    [RequireComponent(typeof(BoxCollider))]
    [DefaultExecutionOrder(int.MaxValue)]
    [HideMonoScript]
    [Experimental]
    public class TerrainMask : TriInspectorMonoBehaviour
    {
        public bool removeTrees = true;
        public bool removeDetails = true;

        private bool _didEnable;

        private void OnEnable()
        {
            if (TerrainMaskManager.Instance && (removeTrees || removeDetails))
            {
                TerrainMaskManager.Instance.ApplyMasks();
                _didEnable = true;
            }
        }

        private void OnDisable()
        {
            if (TerrainMaskManager.Instance && (removeTrees || removeDetails) && _didEnable)
                TerrainMaskManager.Instance.ApplyMasks();
        }
    }
}
