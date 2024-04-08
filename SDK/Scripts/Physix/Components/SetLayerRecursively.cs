using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Physix.Components
{
    /// <summary>
    /// Sets a game object's layer recursively.
    /// </summary>
    [AddComponentMenu(MetaverseConstants.ProductName + "/Physics/Set Layer Recursively")]
    [HideMonoScript]
    public class SetLayerRecursively : TriInspectorMonoBehaviour
    {
        [SerializeField, DisallowNull] private string layerName = "Default";
        [SerializeField] private bool setOnStart = true;

        public string LayerName => layerName;

        private void Start()
        {
            if (setOnStart)
                SetLayer();
        }

        public void SetLayer(string layer)
        {
            gameObject.SetLayerRecursively(LayerMask.NameToLayer(layer));
        }

        public void SetLayer()
        {
            if (!string.IsNullOrEmpty(layerName))
                SetLayer(layerName);
        }
    }
}
