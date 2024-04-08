using System;
using System.Collections.Generic;
using System.Linq;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    /// <summary>
    /// This class provides a basic API to control players within a game. It adds various flags and multipliers
    /// so that modifying the player input can be done from without a ton of redundancy and architecture.
    /// </summary>
    [HideMonoScript]
    public class PlayerInputAPI : TriInspectorMonoBehaviour
    {
        private static readonly List<PlayerInputAPI> Instances = new();
        
        private static bool _movementInputEnabled = true;
        public static bool MovementInputEnabled
        {
            get => _movementInputEnabled;
            private set
            {
                if (_movementInputEnabled == value) return;
                _movementInputEnabled = value;
                MovementInputEnabledChanged?.Invoke();
            }
        }
        
        private static bool _lookInputEnabled = true;
        public static bool LookInputEnabled
        {
            get => _lookInputEnabled;
            private set
            {
                if (_lookInputEnabled == value) return;
                _lookInputEnabled = value;
                LookInputEnabledChanged?.Invoke();
            }
        }

        private static bool _jumpInputEnabled = true;
        public static bool JumpInputEnabled
        {
            get => _jumpInputEnabled;
            private set
            {
                if (_jumpInputEnabled == value) return;
                _jumpInputEnabled = value;
                JumpInputEnabledChanged?.Invoke();
            }
        }
        
        private static bool _crouchInputEnabled = true;
        public static bool CrouchInputEnabled
        {
            get => _crouchInputEnabled;
            private set
            {
                if (_crouchInputEnabled == value) return;
                _crouchInputEnabled = value;
                CrouchInputEnabledChanged?.Invoke();
            }
        }
        
        private static bool _sprintInputEnabled = true;
        public static bool SprintInputEnabled
        {
            get => _sprintInputEnabled;
            private set
            {
                if (_sprintInputEnabled == value) return;
                _sprintInputEnabled = value;
                SprintInputEnabledChanged?.Invoke();
            }
        }
        
        private static bool _interactInputEnabled = true;
        public static bool InteractInputEnabled
        {
            get => _interactInputEnabled;
            private set
            {
                if (_interactInputEnabled == value) return;
                _interactInputEnabled = value;
                InteractInputEnabledChanged?.Invoke();
            }
        }
        
        private static bool _crosshairEnabled = true;
        public static bool CrosshairEnabled
        {
            get => _crosshairEnabled;
            private set
            {
                if (_crosshairEnabled == value) return;
                _crosshairEnabled = value;
                CrosshairEnabledChanged?.Invoke();
            }
        }
        
        private static bool _avatarVisibility = true;
        public static bool AvatarVisibility
        {
            get => _avatarVisibility;
            private set
            {
                if (_avatarVisibility == value) return;
                _avatarVisibility = value;
                AvatarVisibilityChanged?.Invoke();
            }
        }
        
        private static float _jumpMultiplier = 1;
        public static float JumpMultiplier
        {
            get => _jumpMultiplier;
            private set
            {
                if (Mathf.Approximately(_jumpMultiplier, value)) return;
                _jumpMultiplier = value;
                JumpMultiplierChanged?.Invoke(value);
            }
        }
        
        private static float _movementSpeedMultiplier = 1;
        public static float MovementSpeedMultiplier
        {
            get => _movementSpeedMultiplier;
            private set
            {
                if (Mathf.Approximately(_movementSpeedMultiplier, value)) return;
                _movementSpeedMultiplier = value;
                MovementSpeedMultiplierChanged?.Invoke(value);
            }
        }

        public static event Action MovementInputEnabledChanged;
        public static event Action LookInputEnabledChanged;
        public static event Action JumpInputEnabledChanged;
        public static event Action CrouchInputEnabledChanged;
        public static event Action SprintInputEnabledChanged;
        public static event Action InteractInputEnabledChanged;
        public static event Action CrosshairEnabledChanged;
        public static event Action AvatarVisibilityChanged;
        public static event Action<float> JumpMultiplierChanged;
        public static event Action<float> MovementSpeedMultiplierChanged;

        [Title("Options")] 
        [SerializeField] private bool receiveEvents;
        [LabelText("Modify Player Input")]
        [SerializeField] private bool modifyPlayerInputFlags;
        
        [Title("Flags")] 
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableMovementInput = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableLookInput = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableJumpInput = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableCrouchInput = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableSprintInput = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableInteractInput = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableCrosshair = true;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [SerializeField] private bool enableAvatarVisibility = true;
        
        [Title("Modifiers")]
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [Min(0)] [SerializeField] private float jumpMultiplier = 1;
        [ShowIf(nameof(modifyPlayerInputFlags))]
        [Min(0)] [SerializeField] private float movementSpeedMultiplier = 1;

        [Title("Events")] 
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent movementInputEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent movementInputDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent lookInputEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent lookInputDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent jumpInputEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent jumpInputDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent crouchInputEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent crouchInputDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent sprintInputEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent sprintInputDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent interactInputEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent interactInputDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent crosshairEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent crosshairDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent avatarVisibilityEnabled;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent avatarVisibilityDisabled;
        [Space]
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent<float> jumpMultiplierChanged;
        [ShowIf(nameof(receiveEvents))]
        [SerializeField] private UnityEvent<float> movementSpeedMultiplierChanged;
        
        private void Start()
        {
            if (!receiveEvents) return;
            InvokeEvents();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (modifyPlayerInputFlags && !Application.isPlaying) return;
            CheckPlayerInput();
        }
#endif

        private void OnEnable()
        {
            if (modifyPlayerInputFlags && !Instances.Contains(this))
            {
                Instances.Add(this);
                CheckPlayerInput();
            }
            
            if (receiveEvents)
            {
                MovementInputEnabledChanged += InvokeEvents;
                LookInputEnabledChanged += InvokeEvents;
                JumpInputEnabledChanged += InvokeEvents;
                CrouchInputEnabledChanged += InvokeEvents;
                SprintInputEnabledChanged += InvokeEvents;
                InteractInputEnabledChanged += InvokeEvents;
                CrosshairEnabledChanged += InvokeEvents;
                AvatarVisibilityChanged += InvokeEvents;
                JumpMultiplierChanged += OnJumpMultiplierChanged;
                MovementSpeedMultiplierChanged += OnMovementMultiplierChanged;
            }
        }

        private void OnDisable()
        {
            if (MetaverseProgram.IsQuitting)
                return;
            
            MovementInputEnabledChanged -= InvokeEvents;
            LookInputEnabledChanged -= InvokeEvents;
            JumpInputEnabledChanged -= InvokeEvents;
            CrouchInputEnabledChanged -= InvokeEvents;
            SprintInputEnabledChanged -= InvokeEvents;
            InteractInputEnabledChanged -= InvokeEvents;
            CrosshairEnabledChanged -= InvokeEvents;
            AvatarVisibilityChanged -= InvokeEvents;
            JumpMultiplierChanged -= OnJumpMultiplierChanged;
            MovementSpeedMultiplierChanged -= OnMovementMultiplierChanged;

            if (modifyPlayerInputFlags && Instances.Remove(this))
            {
                CheckPlayerInput();
            }
        }

        private void InvokeEvents()
        {
            if (!isActiveAndEnabled) return;
            if (_movementInputEnabled) movementInputEnabled?.Invoke();
            else movementInputDisabled?.Invoke();
            if (_lookInputEnabled) lookInputEnabled?.Invoke();
            else lookInputDisabled?.Invoke();
            if (_jumpInputEnabled) jumpInputEnabled?.Invoke();
            else jumpInputDisabled?.Invoke();
            if (_crouchInputEnabled) crouchInputEnabled?.Invoke();
            else crouchInputDisabled?.Invoke();
            if (_sprintInputEnabled) sprintInputEnabled?.Invoke();
            else sprintInputDisabled?.Invoke();
            if (_interactInputEnabled) interactInputEnabled?.Invoke();
            else interactInputDisabled?.Invoke();
            if (_crosshairEnabled) crosshairEnabled?.Invoke();
            else crosshairDisabled?.Invoke();
            if (_avatarVisibility) avatarVisibilityEnabled?.Invoke();
            else avatarVisibilityDisabled?.Invoke();
            
            jumpMultiplierChanged?.Invoke(jumpMultiplier);
            movementSpeedMultiplierChanged?.Invoke(movementSpeedMultiplier);
        }

        private void OnJumpMultiplierChanged(float val) => InvokeEvents();
        
        private void OnMovementMultiplierChanged(float val) => InvokeEvents();

        private static void CheckPlayerInput()
        {
            if (Instances.Count == 0)
            {
                MovementInputEnabled = true;
                LookInputEnabled = true;
                JumpInputEnabled = true;
                CrouchInputEnabled = true;
                SprintInputEnabled = true;
                InteractInputEnabled = true;
                CrosshairEnabled = true;
                AvatarVisibility = true;
                JumpMultiplier = 1;
                MovementSpeedMultiplier = 1;
                return;
            }
            
            MovementInputEnabled = Instances.All(i => i.enableMovementInput);
            LookInputEnabled = Instances.All(i => i.enableLookInput);
            JumpInputEnabled = Instances.All(i => i.enableJumpInput);
            CrouchInputEnabled = Instances.All(i => i.enableCrouchInput);
            SprintInputEnabled = Instances.All(i => i.enableSprintInput);
            InteractInputEnabled = Instances.All(i => i.enableInteractInput);
            CrosshairEnabled = Instances.All(i => i.enableCrosshair);
            AvatarVisibility = Instances.All(i => i.enableAvatarVisibility);
            JumpMultiplier = Instances.Sum(i => i.jumpMultiplier);
            MovementSpeedMultiplier = Instances.Sum(i => i.movementSpeedMultiplier);
        }
        
        public void SetInteractInputEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableInteractInput = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetLookInputEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableLookInput = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetSprintInputEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableSprintInput = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetCrouchInputEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableCrouchInput = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetJumpInputEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableJumpInput = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }

        public void SetMovementInputEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableMovementInput = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetCrosshairEnabled(bool isEnabled)
        {
            if (!modifyPlayerInputFlags) return;
            enableCrosshair = isEnabled;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetJumpMultiplier(float multiplier)
        {
            if (!modifyPlayerInputFlags) return;
            jumpMultiplier = multiplier;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
        
        public void SetMovementSpeedMultiplier(float multiplier)
        {
            if (!modifyPlayerInputFlags) return;
            movementSpeedMultiplier = multiplier;
            if (isActiveAndEnabled)
                CheckPlayerInput();
        }
    }
}