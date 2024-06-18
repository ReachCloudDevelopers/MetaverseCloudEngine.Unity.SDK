using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class ExtractFloatInput : TriInspectorMonoBehaviour
    {
        [InputControl(layout = "Axis")] [SerializeField]
        private InputActionReference inputAction;
        [SerializeField] private UnityEvent<float> onGetValue;

        public UnityEvent<float> OnGetValue => onGetValue;
        
        private void OnEnable()
        {
            if (!inputAction) return;
            inputAction.action.Enable();
            inputAction.action.performed += OnPerformed;
        }

        private void OnDisable()
        {
            if (!inputAction) return;
            inputAction.action.Disable();
            inputAction.action.performed -= OnPerformed;
        }

        private void OnPerformed(InputAction.CallbackContext ctx)
        {
            onGetValue?.Invoke(ctx.ReadValue<float>());
        }
    }
}