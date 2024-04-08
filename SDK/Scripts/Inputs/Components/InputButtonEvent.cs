using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    [HideMonoScript]
    [DeclareFoldoutGroup("Options")]
    [DeclareFoldoutGroup("Events")]
    public class InputButtonEvent : TriInspectorMonoBehaviour
    {
        public enum LockedCursorBehavior
        {
            RequireLocked,
            RequireUnlocked,
            [InspectorName("Doesn't Matter")]
            DoesntMatter
        }

        public enum ToggleState
        {
            OnPerformed,
            OnCancelled
        }

        [LabelText("")]
        [PropertySpace(-10, 10)]
        [Indent(-1)]
        public InputActionProperty action;
        [Space]
        [Group("Options")] public bool autoEnable = true;
        [Group("Options")] public bool autoDisable = true;
        [Group("Options")] public bool ignoreIfInputFocused = true;
        [Group("Options")] public bool ignoreIfOverUI;
        [Group("Options")] public LockedCursorBehavior cursorBehaviour = LockedCursorBehavior.DoesntMatter;
        [Group("Options")] public bool toggleValue;
        [Group("Options")] public ToggleState toggleState;
        [Group("Options")] public bool checkStateOnEnable;
        [Space]
        [Group("Events")] public UnityEvent onPerformed = new();
        [Group("Events")] public UnityEvent onCancelled = new();
        [Group("Events")] public UnityEvent<bool> onToggled = new();
        [Group("Events")] public UnityEvent<bool> onPerforming = new();

        public static Func<bool> WebViewCheckInputFieldFocusedCallback;

        private bool _hasCalculatedCallbacks;
        private bool _hasAnyPerformedCallbacks;
        private bool _hasAnyCancelledCallbacks;
        private bool _hasAnyToggledCallbacks;
        private bool _hasAnyPerformingCallbacks;

        private void Awake()
        {
            if (action.action == null) return;
            action.action.performed += OnActionPerformed;
            action.action.canceled += OnActionCancelled;
            if (action.action.IsPressed() && checkStateOnEnable)
                OnPerformed();
            else OnCancelled();
        }

        private void OnDestroy()
        {
            if (action.action == null) return;
            action.action.performed -= OnActionPerformed;
            action.action.canceled -= OnActionCancelled;
            action.action.Disable();
            action.action.Dispose();
        }

        private void OnEnable()
        {
            if (action.action == null) return;
            if (autoEnable) action.action.Enable();
            if (action.action.IsPressed() && checkStateOnEnable) Performed();
        }

        private void OnDisable()
        {
            if (action.action == null) return;
            if (autoDisable) action.action.Disable();
            if (action.action.IsPressed()) Cancelled();
        }

        private bool HasAnyEventCallbacksIfNotDestroySelf()
        {
            if (!_hasCalculatedCallbacks)
            {
                _hasAnyPerformedCallbacks = onPerformed.GetPersistentEventCount() > 0;
                _hasAnyCancelledCallbacks = onCancelled.GetPersistentEventCount() > 0;
                _hasAnyToggledCallbacks = onToggled.GetPersistentEventCount() > 0;
                _hasAnyPerformingCallbacks = onPerforming.GetPersistentEventCount() > 0;
                _hasCalculatedCallbacks = true;
            }

            if (_hasAnyCancelledCallbacks || _hasAnyPerformedCallbacks || _hasAnyPerformingCallbacks || _hasAnyToggledCallbacks)
                return true;

            Destroy(this);
            return false;
        }

        private void OnPerformed()
        {
            if (toggleState == ToggleState.OnPerformed)
                Toggle();
        }

        private void OnCancelled()
        {
            if (toggleState == ToggleState.OnCancelled)
                Toggle();
        }

        private void Toggle()
        {
            toggleValue = !toggleValue;
            onToggled?.Invoke(toggleValue);
        }

        private void OnActionPerformed(InputAction.CallbackContext ctx)
        {
            if (isActiveAndEnabled)
                Performed();
        }

        private void OnActionCancelled(InputAction.CallbackContext ctx)
        {
            if (isActiveAndEnabled)
                Cancelled();
        }
        
        private void Performed()
        {
            if (!ShouldPerform())
                return;
            
            if (!HasAnyEventCallbacksIfNotDestroySelf())
                return;

            if (ignoreIfInputFocused)
            {
                if (MVUtils.IsUnityInputFieldFocused())
                    return;
                if (!IsCursorLockStateValid())
                    return;
                if (WebViewCheckInputFieldFocusedCallback?.Invoke() == true)
                    return;
            }

            if (ignoreIfOverUI)
            {
                if (MVUtils.IsPointerOverUI())
                    return;
            }

            onPerformed?.Invoke();
            onPerforming?.Invoke(true);
            OnPerformed();
        }

        private void Cancelled()
        {
            if (!HasAnyEventCallbacksIfNotDestroySelf())
                return;

            onCancelled?.Invoke();
            onPerforming?.Invoke(false);
            OnCancelled();
        }

        private bool IsCursorLockStateValid()
        {
            if (XRSettings.isDeviceActive || Application.isMobilePlatform)
                return true;

            return cursorBehaviour switch
            {
                LockedCursorBehavior.RequireUnlocked => Cursor.lockState != CursorLockMode.Locked,
                LockedCursorBehavior.RequireLocked => Cursor.lockState == CursorLockMode.Locked,
                _ => true,
            };
        }
        
        protected virtual bool ShouldPerform()
        {
            return true;
        }
    }
}
