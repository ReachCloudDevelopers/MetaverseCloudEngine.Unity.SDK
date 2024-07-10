using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public abstract class ExtractInput<T> : TriInspectorMonoBehaviour where T : struct
    {
        [SerializeField] private InputActionProperty inputAction;
        [SerializeField] private UnityEvent<T> onGetValue = new();

        private InputAction _inputAction;

        protected ExtractInput()
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            inputAction = new InputActionProperty(new InputAction(expectedControlType: ExpectedControlType()));
        }
        
        /// <summary>
        /// Gets or sets the input action to extract the value from.
        /// </summary>
        public UnityEvent<T> OnGetValue => onGetValue;

        protected abstract string ExpectedControlType();
        
        protected virtual void OnEnable()
        {
            if (inputAction.action is null) return;
            inputAction.action.Enable();
            inputAction.action.performed += OnPerformed;
        }

        protected virtual void OnDisable()
        {
            if (inputAction.action is null) return;
            inputAction.action.Disable();
            inputAction.action.performed -= OnPerformed;
        }

        protected virtual void OnPerformed(InputAction.CallbackContext ctx)
        {
            onGetValue?.Invoke(ctx.ReadValue<T>());
        }
    }
}