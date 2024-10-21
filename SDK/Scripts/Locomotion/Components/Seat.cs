using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Avatar.Components;
using MetaverseCloudEngine.Unity.Networking.Components;
using MetaverseCloudEngine.Unity.XR;
using MetaverseCloudEngine.Unity.XR.Components;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using InputDevice = UnityEngine.XR.InputDevice;
#if MV_CINEMACHINE
using Cinemachine;
#endif
#if MV_XR_TOOLKIT_3
using XRSimpleInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable;
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
#else
using XRSimpleInteractable = UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable;
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable;
#endif

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    /// <summary>
    /// A <see cref="Seat"/> allows a <see cref="Sitter"/> to "sit down". This component is used to add sitting
    /// functionality to your player controllers.
    /// </summary>
    [DisallowMultipleComponent]
    [DeclareFoldoutGroup("Sitter")]
    [DeclareFoldoutGroup("IK")]
    [DeclareFoldoutGroup("Animation")]
    [DeclareFoldoutGroup("Events")]
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Locomotion/Seat")]
    public class Seat : NetworkObjectBehaviour
    {
        /// <summary>
        /// Events that are triggered by this <see cref="Seat"/>. These events can be listened to in order
        /// to perform actions upon entering/exiting the <see cref="Seat"/>.
        /// </summary>
        [Serializable]
        public class SeatEvents
        {
            [Tooltip("Invoked when the seat is entered by a Sitter.")]
            public UnityEvent<Sitter> onEntered;
            [Tooltip("Invoked when the seat is exited by a Sitter.")]
            public UnityEvent<Sitter> onExited;
            [Tooltip("Invoked when the seat is exited or entered by a Sitter, with a boolean value indicating whether the sitter has entered or exited the seat.")]
            public UnityEvent<bool> onEnteredValue;

            /// <summary>
            /// Invokes the <see cref="onEntered"/> or <see cref="onExited"/> events based on the <param name="entered"></param> value.
            /// </summary>
            /// <param name="sitter">The <see cref="Sitter"/> that is entering or exiting the seat.</param>
            /// <param name="entered">The value indicating whether the <see cref="Sitter"/> is entering or exiting.</param>
            public void Invoke(Sitter sitter, bool entered)
            {
                if (entered) onEntered?.Invoke(sitter);
                else onExited?.Invoke(sitter);
                onEnteredValue?.Invoke(entered);
            }
        }

        /// <summary>
        /// A class that stores the original state of the <see cref="Sitter"/> before
        /// sitting in the <see cref="Seat"/>. The <see cref="Sitter"/> will be restored
        /// to its original state upon exiting the <see cref="Seat"/> using the
        /// <see cref="SitterInitialState"/>.
        /// </summary>
        public class SitterInitialState
        {
            public Transform OriginalParent;
            public RuntimeAnimatorController OriginalAnimatorController;

            /// <summary>
            /// Captures the original state of the <see cref="Sitter"/>.
            /// </summary>
            /// <param name="sitter">The <see cref="Sitter"/> to capture the original state from.</param>
            public void Capture(Sitter sitter)
            {
                Clear();

                if (sitter.Root)
                    OriginalParent = sitter.Root.parent;
                if (sitter.Animator)
                    OriginalAnimatorController = sitter.Animator.runtimeAnimatorController;
            }

            /// <summary>
            /// Re-applies the original state to the specified <see cref="Sitter"/>.
            /// </summary>
            /// <param name="sitter">The <see cref="Sitter"/> to apply the original state to.</param>
            /// <param name="setParent">A flag indicating whether or not to apply the <see cref="OriginalParent"/>.</param>
            public void Apply(Sitter sitter, bool setParent = true)
            {
                if (!sitter.Destroying)
                {
                    if (sitter.Root && setParent)
                        sitter.Root.parent = OriginalParent;
                    if (sitter.Animator)
                        sitter.Animator.runtimeAnimatorController = OriginalAnimatorController;
                }

                Clear();
            }

            /// <summary>
            /// Clears the initial state data.
            /// </summary>
            public void Clear()
            {
                OriginalParent = null;
                OriginalAnimatorController = null;
            }
        }

        [Tooltip("The interactable object that will be used for interaction events.")]
        [SerializeField, HideInInspector] private XRSimpleInteractable interactable;

        [Group("Sitter")] [SerializeField] private Transform exitPoint;
        [Tooltip("A value indicating whether this seat, when destroyed, can also destroy the sitter who's attached to it. Otherwise the sitter will be un-parented before this seat is destroyed.")]
        [Group("Sitter")] [SerializeField] private bool canDestroySitter;
        [Group("Sitter")] [SerializeField] private bool allowExitInput = true;
        [ShowIf(nameof(allowExitInput))]
        [Group("Sitter")] [SerializeField] private InputActionProperty exitInput;

        [Group("IK")] [SerializeField] private UnityIkTarget[] ikTargets;

        [Tooltip("If you want to override the avatar's runtime animator controller, you can specify one here.")]
        [Group("Animation")] [SerializeField] private RuntimeAnimatorController customAnimatorController;
        [Tooltip("The bool animator parameter to use when sitting vs. not sitting on the avatar's animator.")]
        [Group("Animation")] [SerializeField] private string animatorBoolParameter = "Sit";

        [Group("Events")] [LabelText("Local & Remote")] public SeatEvents events;
        [Group("Events")] [LabelText("Local")] public SeatEvents localOnlyEvents;
        [Group("Events")] [LabelText("Remote")] public SeatEvents remoteOnlyEvents;

        private Transform _transform;
        private Transform _currentSitterTransform;
        private Vector3 _sitterOffset;
        private static readonly Vector3 VRSitterOffset = new (0, -0.2f, 0);

        private readonly SitterInitialState _initialState = new();
        private static float _exitInputCooldown;
        private int _animatorBoolParameterHash = -1;
        private bool _isCurrentSitterInputAuthority;

        private static readonly List<Seat> LocallyActiveSeats = new();

        /// <summary>
        /// The currently active <see cref="Sitter"/> that is using this <see cref="Seat"/>.
        /// </summary>
        public Sitter CurrentSitter { get; private set; }
        
        /// <summary>
        /// A static flag that indicates whether any local player owned <see cref="Sitter"/> is currently
        /// sitting in a <see cref="Seat"/>.
        /// </summary>
        public static bool IsLocalPlayerSitting => LocallyActiveSeats.Count > 0;

        /// <summary>
        /// Gets or sets a value indicating whether we are able to exit this <see cref="Seat"/> using
        /// the defined exit input.
        /// </summary>
        public bool AllowExitInput
        {
            get => allowExitInput;
            set
            {
                if (allowExitInput == value) return;
                allowExitInput = value;
                if (CurrentSitter != null)
                    CurrentSitter.AllowExitInputChanged();
            }
        }

        protected override void Awake()
        {
            _transform = transform;

            if (!string.IsNullOrEmpty(animatorBoolParameter))
                _animatorBoolParameterHash = Animator.StringToHash(animatorBoolParameter);

            EnsureComponents();

            exitInput.action.Enable();
            exitInput.action.performed += OnExitInputPerformed;

            XRInputTrackingAPI.HmdConnected += OnHmdConnected;
            XRInputTrackingAPI.HmdDisconnected += OnHmdDisconnected;

            base.Awake();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            transform.localScale = Vector3.one;
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;

            base.OnDestroy();

            LocallyActiveSeats.Remove(this);

            XRInputTrackingAPI.HmdConnected -= OnHmdConnected;
            XRInputTrackingAPI.HmdDisconnected -= OnHmdDisconnected;

            exitInput.action.performed -= OnExitInputPerformed;
            exitInput.action.Disable();
            exitInput.action.Dispose();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            if (!canDestroySitter)
                SaveSitterFromBeingDestroyed();

            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnHmdConnected(InputDevice hmd)
        {
            if (CurrentSitter != null && CurrentSitter.IsInputAuthority && MVUtils.IsVRCompatible())
                _sitterOffset = CurrentSitter.Root.localPosition = VRSitterOffset;
        }

        private void OnHmdDisconnected(InputDevice obj)
        {
            if (CurrentSitter != null && CurrentSitter.IsInputAuthority)
                _sitterOffset = CurrentSitter.Root.localPosition = Vector3.zero;
        }

        private void Reset()
        {
            EnsureComponents();
            exitInput = new InputActionProperty(new InputAction(binding: "<Keyboard>/f"));
        }

        private void FixedUpdate()
        {
            ForceSitterToZero();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem(MetaverseConstants.MenuItems.GameObjectMenuRootPath + "Seat")]
        private static void CreateSeat()
        {
            try
            {
                var resource = Resources.LoadAsync<GameObject>(MetaverseConstants.Resources.Seat);
                resource.completed += _ =>
                {
                    if (resource.asset == null || resource.asset is not GameObject go || !go.TryGetComponent(out Seat seat))
                    {
                        GenerateStandardSeat();
                        return;
                    }

                    seat = ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(seat.gameObject)).GetComponent<Seat>();
                    UnityEditor.Selection.activeGameObject = seat.gameObject;
                };
            }
            catch
            {
                GenerateStandardSeat();
            }
        }

        private static void GenerateStandardSeat()
        {
            var seat = new GameObject("Seat").AddComponent<Seat>();
            UnityEditor.Selection.activeGameObject = seat.gameObject;
        }
#endif

        public void Exit()
        {
            if (CurrentSitter != null && CurrentSitter.IsInputAuthority)
                SetCurrentSitter(null);
        }

        public void NotifyEntered(Sitter sitter)
        {
            SetCurrentSitter(sitter);
        }

        public void NotifyExited(Sitter sitter)
        {
            if (CurrentSitter == sitter)
                SetCurrentSitter(null);
        }

        private void ForceSitterToZero()
        {
            if (!_currentSitterTransform) return;
            var localPosition = _currentSitterTransform.localPosition;
            var updatePosition = localPosition != VRSitterOffset;
            if ((updatePosition && localPosition.sqrMagnitude > 0) ||
                _currentSitterTransform.localEulerAngles.sqrMagnitude > 0)
            {
                _currentSitterTransform.localEulerAngles = Vector3.zero;
                _currentSitterTransform.localPosition = _sitterOffset;
            }
        }

        private void EnsureComponents()
        {
            gameObject.GetOrAddComponent<NetworkTransform>();
            if (!gameObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.hideFlags = HideFlags.HideInInspector;
            }
            
            interactable = gameObject.GetOrAddComponent<XRSimpleInteractable>();
            if (interactable.colliders == null || interactable.colliders.Count == 0)
                interactable.colliders?.AddRange(GetComponentsInChildren<Collider>(true));
            interactable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.TransformPosition;
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectFilters.Add(new XRSelectFilterDelegate((_, _) => CanInteractWithSeat()));
            interactable.hoverFilters.Add(new XRHoverFilterDelegate((_, _) => CanInteractWithSeat()));
            interactable.enabled = false;
            interactable.enabled = true;
        }

        private bool CanInteractWithSeat()
        {
            if (CurrentSitter)
                return false;
            if (MVUtils.CachedTime < MetaverseInteractable.GlobalInteractionCooldownTime)
                return false;
            if (IsLocalPlayerSitting)
                return false;
            return true;
        }

        private void OnExitInputPerformed(InputAction.CallbackContext ctx)
        {
            if (!this) return;
            if (!enabled) return;
            if (!allowExitInput) return;
            if (!UnityEngine.Device.Application.isMobilePlatform && !XRSettings.isDeviceActive && Cursor.lockState != CursorLockMode.Locked) return;
            if (MVUtils.IsUnityInputFieldFocused()) return;

            if (CurrentSitter == null || !CurrentSitter.IsInputAuthority) return;
            SetCurrentSitter(null);
            _exitInputCooldown = MVUtils.CachedTime + 0.25f;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            args.manager.CancelInteractableSelection(args.interactableObject);

            if (!enabled)
                return;

            if (MVUtils.CachedTime < _exitInputCooldown)
                return;

            if (MVUtils.CachedTime < MetaverseInteractable.GlobalInteractionCooldownTime)
                return;

            MetaverseInteractable.InteractCooldown();

            var sitter = args.interactorObject.transform.GetComponentInParent<Sitter>();
            if (sitter == null || CurrentSitter != null || CurrentSitter == sitter || sitter.CurrentSeat != null)
                return;

            var interactables = args.interactorObject.interactablesSelected;
            foreach (var inter in interactables.ToArray())
            {
                if (inter.transform.TryGetComponent(out MetaverseInteractable mvInteractable))
                {
                    if (mvInteractable.MoveToSocketOrDeselect())
                        continue;
                }
                args.manager.CancelInteractableSelection(inter);
            }
            
            SetCurrentSitter(sitter);
        }

        private void SetCurrentSitter(Sitter sitter)
        {
            if (sitter == CurrentSitter)
                return;
            
            if (sitter && sitter.IsInputAuthority)
                LocallyActiveSeats.Add(this);
            else if (!sitter)
                LocallyActiveSeats.Remove(this);

            DeConfigureCurrentSitter();

            if (sitter)
                ConfigureNewSitter(sitter);
        }

        private void DeConfigureCurrentSitter()
        {
            var oldSitter = CurrentSitter;
            if (CurrentSitter == null || CurrentSitter.Destroying)
            {
                if (!CurrentSitter) return;
                Cleanup();
                return;
            }
            
            var exit = exitPoint ? exitPoint : _transform;
            if (_isCurrentSitterInputAuthority)
                ResetCamera(exit);

            Cleanup();

            var oldSitterRoot = oldSitter.Root;
            var up = oldSitterRoot.up;
            var exitPosition = exit.position;
            var exitDir = Vector3.ProjectOnPlane(exit.forward, up).normalized;
            var exitRot = Quaternion.LookRotation(exitDir, up);
            ApplyPose(position: true, rotate: false);
            MetaverseDispatcher.WaitForSeconds(0.1f,() => ApplyPose(position: false, rotate: true));

            if (_animatorBoolParameterHash != -1 && oldSitter.Animator)
                oldSitter.Animator.SetBool(_animatorBoolParameterHash, false);

            var avatarContainer = oldSitter.GetComponentInChildren<PlayerAvatarContainer>();
            if (avatarContainer)
                avatarContainer.Events.onAvatarSpawned.RemoveListener(OnSitterAvatarSpawned);

            if (oldSitter.IsInputAuthority)
                MetaverseInteractable.InteractCooldown();
            return;

            void Cleanup()
            {
                CurrentSitter = null;
                InvokeEvents(oldSitter, _isCurrentSitterInputAuthority, false);
                _isCurrentSitterInputAuthority = false;
                if (ikTargets != null) foreach (var ikTarget in ikTargets) if (ikTarget) ikTarget.Animator = null;
                _currentSitterTransform = null;
                _sitterOffset = Vector3.zero;
                interactable.enabled = true;
                if (oldSitter) _initialState.Apply(oldSitter, oldSitter.IsInputAuthority);
                else _initialState.Clear();
            }

            void ApplyPose(bool position = true, bool rotate = true)
            {
                if (!this) return;
                if (!oldSitterRoot) return;
                if (position) oldSitterRoot.position = exitPosition;
                if (rotate) oldSitterRoot.rotation = exitRot;
                var rb = oldSitterRoot.GetComponentInParent<Rigidbody>();
                if (!rb) return;
                if (position) rb.MovePosition(exitPosition);
                if (rotate) rb.MoveRotation(exitRot);
            }
        }

        private static void ResetCamera(Transform target)
        {
#if MV_CINEMACHINE
            MetaverseDispatcher.WaitForSeconds(0.15f, () =>
            {
                var currentCamera = CinemachineCore.Instance.GetActiveBrain(0)?.ActiveVirtualCamera;
                var xOffset = 0f;
                if (currentCamera is not CinemachineFreeLook freeLook)
                {
                    if (currentCamera is not CinemachineStateDrivenCamera stateDrivenCamera)
                        return;
                    freeLook = stateDrivenCamera.LiveChild as CinemachineFreeLook;
                    if (freeLook == null)
                        return;

                    target = target ? target : freeLook.m_Follow;
                    xOffset = (Quaternion.Inverse(stateDrivenCamera.transform.rotation) * target.rotation).eulerAngles.y;
                }
                else if (!freeLook.m_RecenterToTargetHeading.m_enabled)
                    xOffset = freeLook.m_Follow.localEulerAngles.y;
                freeLook.m_XAxis.Value = xOffset;
                freeLook.m_YAxis.Value = 0.5f;
                freeLook.PreviousStateIsValid = false;
                
                XRInputTrackingAPI.CenterOrigin();
            });
#endif
        }

        private void ConfigureNewSitter(Sitter sitter)
        {
            _initialState.Capture(sitter);
            CurrentSitter = sitter;
            _isCurrentSitterInputAuthority = sitter.IsInputAuthority;
            CurrentSitter.Root.SetParent(_transform);
            CurrentSitter.Root.ResetLocalTransform(scale: false);
            if (sitter.IsInputAuthority && MVUtils.IsVRCompatible())
                _sitterOffset = CurrentSitter.Root.localPosition = VRSitterOffset;
            _currentSitterTransform = sitter.transform;
            var avatarContainer = CurrentSitter.GetComponentsInChildren<PlayerAvatarContainer>(true).FirstOrDefault();
            if (avatarContainer)
                avatarContainer.Events.onAvatarSpawned.AddListener(OnSitterAvatarSpawned);
            if (sitter.IsInputAuthority)
                XRInputTrackingAPI.CenterOrigin();
            UpdateCurrentSitterAvatar();
            interactable.enabled = false;
            if (ikTargets != null)
                foreach (var ikTarget in ikTargets)
                    if (ikTarget) ikTarget.Animator = sitter.Animator;
            InvokeEvents(CurrentSitter, _isCurrentSitterInputAuthority, true);
            if (_isCurrentSitterInputAuthority)
                ResetCamera(_transform);
        }

        private void UpdateCurrentSitterAvatar()
        {
            if (!CurrentSitter.Animator)
                return;

            if (customAnimatorController != null)
                CurrentSitter.Animator.runtimeAnimatorController = customAnimatorController;

            if (!string.IsNullOrEmpty(animatorBoolParameter))
                CurrentSitter.Animator.SetBool(animatorBoolParameter, true);
        }

        private void InvokeEvents(Sitter sitter, bool isLocal, bool sitting)
        {
            if (isLocal) 
                localOnlyEvents.Invoke(sitter, sitting);
            else remoteOnlyEvents.Invoke(sitter, sitting);
            events.Invoke(sitter, sitting);
        }

        private void SaveSitterFromBeingDestroyed()
        {
            if (!MetaSpace)
                return;

            if (MetaSpace.NetworkingService is null || 
                MetaSpace.PlayerSpawnService is null || 
                !(MetaSpace.PlayerSpawnOptions?.AutoSpawnPlayer ?? false))
                return;
            
            var children = _transform.gameObject
                .GetComponentsInChildren<NetworkObject>(true)
                .Where(x => x.transform != _transform);

            if (children.All(child => child.gameObject != MetaSpace.PlayerSpawnService.SpawnedPlayerObject)) 
                return;
            
            var metaSpace = MetaSpace;
            MetaverseDispatcher.WaitForSeconds(1f, () =>
            {
                if (!metaSpace) return;
                if (metaSpace.PlayerSpawnService.SpawnedPlayerObject) return;
                metaSpace.PlayerSpawnService.TrySpawnPlayer(metaSpace.NetworkingService.LocalPlayerID);
            });
        }

        private void OnSitterAvatarSpawned(Animator avatar)
        {
            UpdateCurrentSitterAvatar();
        }
    }
}