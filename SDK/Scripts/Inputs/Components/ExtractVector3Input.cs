using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class ExtractVector3Input : TriInspectorMonoBehaviour
    {
        [InputControl(layout = nameof(Vector3))] [SerializeField]
        private InputActionReference inputAction;
        [SerializeField] private UnityEvent<Vector3> onGetValue;

        public UnityEvent<Vector3> OnGetValue => onGetValue;
        
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
            onGetValue?.Invoke(ctx.ReadValue<Vector3>());
        }
    }
}