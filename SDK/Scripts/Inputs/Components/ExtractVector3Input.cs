using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class ExtractVector3Input : TriInspectorMonoBehaviour
    {
        [InputControl(layout = nameof(Vector3))]
        [SerializeField] private string inputAction;
        [SerializeField] private UnityEvent<Vector3> onGetValue;

        private InputAction _inputAction;
        public InputAction Action => _inputAction ??= new InputAction(inputAction);
        public UnityEvent<Vector3> OnGetValue => onGetValue;
        
        private void OnEnable()
        {
            if (Action is null) return;
            Action.Enable();
            Action.performed += OnPerformed;
        }

        private void OnDisable()
        {
            if (Action is null) return;
            Action.Disable();
            Action.performed -= OnPerformed;
        }

        private void OnPerformed(InputAction.CallbackContext ctx)
        {
            onGetValue?.Invoke(ctx.ReadValue<Vector3>());
        }
    }
}