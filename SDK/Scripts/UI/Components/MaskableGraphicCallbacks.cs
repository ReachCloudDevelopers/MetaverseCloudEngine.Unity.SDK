using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(MaskableGraphic))]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/UI/Components/Maskable Graphic Callbacks")]
    public class MaskableGraphicCallbacks : TriInspectorMonoBehaviour
    {
        [InfoBox("This component will invoke the OnVisible event when the MaskableGraphic is visible and the OnInvisible event when it is not.")]
        public UnityEvent onVisible = new();
        public UnityEvent onInvisible = new();
        
        private MaskableGraphic _maskableGraphic;
        
        private void Awake()
        {
            _maskableGraphic = GetComponent<MaskableGraphic>();
            
            if (_maskableGraphic == null)
            {
                Debug.LogError("MaskableGraphicCallbacks requires a MaskableGraphic component to be attached to the same GameObject.");
            }
            
            _maskableGraphic.onCullStateChanged.AddListener(OnCullStateChanged);
        }

        private void OnDestroy()
        {
            _maskableGraphic.onCullStateChanged.RemoveListener(OnCullStateChanged);
        }

        private void OnCullStateChanged(bool visible)
        {
            if (visible)
            {
                onInvisible.Invoke();
            }
            else
            {
                onVisible.Invoke();
            }
        }
    }
}