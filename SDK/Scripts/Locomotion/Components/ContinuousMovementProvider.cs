using System.Collections;
using MetaverseCloudEngine.Unity.Inputs.Components;
using MetaverseCloudEngine.Unity.Physix.Abstract;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.Locomotion.Components
{
    /// <summary>
    /// The continuous movement provider allows you to control a 
    /// game object using user input.
    /// </summary>
    [RequireComponent(typeof(IPhysicsProvider))]
    [HideMonoScript]
    [Experimental]
    public class ContinuousMovementProvider : TriInspectorMonoBehaviour
    {
        [Header("Simulation")]
        [SerializeField] private bool autoUpdate = true;
        [SerializeField] private bool useFixedUpdate;

        [Header("Motion")]
        [SerializeField] private bool allowVerticalMovement;
        [SerializeField] private bool inputFieldBlocksInput = true;
        [SerializeField] private bool cursorUnlockedBlocksInput;
        [SerializeField, Min(0)] private float movementSpeed = 5.0f;
        [SerializeField, Min(0)] private float movementSpeedMultiplier = 1.0f;
        [SerializeField, Min(0)] private float airControlModifier = 0.05f;
        [SerializeField, Min(0)] private float horizontalAcceleration = 25.0f;
        [SerializeField, Min(0)] private float groundedStickForce = 30.0f;
        [SerializeField] private Transform lookReference;

        [Header("Rotation")]
        [SerializeField] private bool updateRotation = true;
        [SerializeField] private bool allowPitchRotation;
        [SerializeField] private bool faceInput = true;
        [SerializeField] private bool instantRotation;
        [SerializeField] private bool useLerpRotation;
        [SerializeField, Min(0)] private float rotationSpeed = 720f;
        
        [Header("Input")]
        [SerializeField] private InputActionProperty movementInput;
        [Tooltip("If true, this component will read the current values of the player input flags and apply them to the movement provider.")]
        [SerializeField] private bool readPlayerInputAPI = true;

        private Transform _transform;
        private Vector2 _movementInputVector;
        private Vector3 _lastMovementInputVector;
        private IPhysicsProvider _physicsProvider;
        private IEnumerator _parentRotationMatchRoutine;

        public Transform LookReference {
            get => lookReference ? lookReference : _transform;
            set => lookReference = value;
        }

        public bool UpdateRotation {
            get => updateRotation;
            set => updateRotation = value;
        }

        public bool FaceInput {
            get => faceInput;
            set => faceInput = value;
        }

        public bool InstantRotation {
            get => instantRotation;
            set => instantRotation = value;
        }

        public bool MoveInputEnabled
        {
            get => (movementInput.action?.enabled ?? false) &&
                   (!readPlayerInputAPI || PlayerInputAPI.MovementInputEnabled);
            set {
                if (movementInput.action != null)
                {
                    if (!value)
                        movementInput.action.Disable();
                    else
                        movementInput.action.Enable();
                }
            }
        }

        public Vector3 Up => _transform.parent ? _transform.parent.up : _transform.up;

        public Vector3 ParentUp => _transform.parent ? _transform.parent.up : Vector3.up;

        public float MovementSpeedMultiplier
        {
            get
            {
                var speedMultiplier = readPlayerInputAPI && !PlayerInputAPI.SprintInputEnabled
                    ? Mathf.Min(1f, movementSpeedMultiplier)
                    : movementSpeedMultiplier;
                
                if (readPlayerInputAPI)
                    speedMultiplier *= PlayerInputAPI.MovementSpeedMultiplier;
                
                return speedMultiplier;
            }
            set => movementSpeedMultiplier = value;
        }

        public float MovementSpeed {
            get => movementSpeed;
            set => movementSpeed = value;
        }

        public Vector3 DesiredSpeed { get; private set; }

        public Vector2 MovementInputVector {
            get => _movementInputVector;
            set => _movementInputVector = value;
        }

        private void Awake()
        {
            _transform = transform;
            _physicsProvider = GetComponent<IPhysicsProvider>();

            if (!lookReference)
                lookReference = _transform;

            movementInput.action?.Enable();
        }

        private void OnDestroy()
        {
            if (movementInput.action != null)
            {
                movementInput.action.Disable();
                movementInput.action.Dispose();
            }
        }

        private void OnEnable()
        {
            if (movementInput.action != null)
            {
                movementInput.action.performed += OnMovePerformed;
                movementInput.action.canceled += OnMoveCancelled;
            }
        }

        private void OnDisable()
        {
            if (movementInput.action != null)
            {
                movementInput.action.performed -= OnMovePerformed;
                movementInput.action.canceled -= OnMoveCancelled;
            }

            _movementInputVector = Vector2.zero;
        }

        private void OnTransformParentChanged()
        {
            MatchParentRotation();
        }

        private void Update()
        {
            if (autoUpdate && !useFixedUpdate)
                Tick(false);
        }

        private void LateUpdate()
        {
            if (autoUpdate && !useFixedUpdate)
                FaceDirection(CalculateLookDirection());
        }

        private void FixedUpdate()
        {
            if (autoUpdate && useFixedUpdate)
                Tick(true);
        }

        public void Tick(bool updateRot = true)
        {
            UpdateMovement();
            if (updateRot)
                FaceDirection(CalculateLookDirection());
        }

        private Vector3 CalculateLookDirection()
        {
            return faceInput ? _lastMovementInputVector : LookReference.forward.FlattenDirection(Up);
        }

        private void MatchParentRotation()
        {
            if (_parentRotationMatchRoutine != null)
            {
                StopCoroutine(_parentRotationMatchRoutine);
                _parentRotationMatchRoutine = null;
            }

            _parentRotationMatchRoutine = ParentRotationMatchCoroutine();
            StartCoroutine(_parentRotationMatchRoutine);
        }

        private IEnumerator ParentRotationMatchCoroutine()
        {
            Vector3 up;
            Quaternion targetRot;
            do
            {
                up = ParentUp;
                var direction = (_physicsProvider.Rotation * Vector3.forward).FlattenDirection(up);
                targetRot = Quaternion.LookRotation(direction, up);

                if (!instantRotation)
                    _physicsProvider.Rotation = Quaternion.Lerp(_physicsProvider.Rotation, targetRot, Time.deltaTime * 10);
                else
                    break;

                yield return null;
            }
            while (Vector3.Angle(_physicsProvider.Rotation * Vector3.up, up) > Mathf.Epsilon);

            _physicsProvider.Rotation = targetRot;
            _parentRotationMatchRoutine = null;
        }

        private void UpdateMovement()
        {
            var movement = CalculateMovement();
            _lastMovementInputVector = movement;
            Move(movement);
        }

        private Vector3 CalculateMovement()
        {
            var movement = LookReference.rotation * new Vector3(_movementInputVector.x, 0, _movementInputVector.y);
            if (movement.magnitude > 1) movement.Normalize();
            if (!allowVerticalMovement)
                movement = movement.FlattenDirection(ParentUp);
            return movement;
        }

        private void Move(Vector3 movement)
        {
            DesiredSpeed = CalculateDesiredSpeed(movement);

            if (_physicsProvider == null)
                return;

            var useVerticalMove = UseVerticalMovement();
            var physicalHorizontalVelocity = useVerticalMove 
                ? _physicsProvider.Velocity 
                : _physicsProvider.Velocity.FlattenDirection(_transform.up);
            var verticalVelocity = useVerticalMove 
                ? Vector3.zero 
                : _physicsProvider.Velocity - physicalHorizontalVelocity;
            var accelerationMultiplier = _physicsProvider.IsGrounded ? 1f : airControlModifier;
            var appliedHorizontalVelocity = physicalHorizontalVelocity;

            if (_physicsProvider.IsGrounded)
            {
                if (DesiredSpeed.sqrMagnitude > 0)
                {
                    var downForceMultiplier = Mathf.Clamp01(Mathf.InverseLerp(0, 25, appliedHorizontalVelocity.sqrMagnitude));
                    verticalVelocity += -Up * (Time.deltaTime * groundedStickForce * downForceMultiplier);   
                }
                else
                {
                    var relativeVerticalVelocity = Quaternion.Inverse(_physicsProvider.Rotation) * verticalVelocity;
                    if (relativeVerticalVelocity.y > 0)
                    {
                        relativeVerticalVelocity.y = 0;
                        verticalVelocity = _physicsProvider.Rotation * relativeVerticalVelocity;
                    }
                }
            }

            appliedHorizontalVelocity = Vector3.MoveTowards(
                appliedHorizontalVelocity, 
                DesiredSpeed, 
                Time.deltaTime * horizontalAcceleration * accelerationMultiplier);
            
            _physicsProvider.Velocity = appliedHorizontalVelocity + verticalVelocity;
        }

        private Vector3 CalculateDesiredSpeed(Vector3 input)
        {
            if (input.magnitude > 1) 
                input.Normalize();
            if (!UseVerticalMovement())
                input = input.FlattenDirection(Up, _transform.up);
            
            var forward = _transform.forward;
            return movementSpeed * MovementSpeedMultiplier * (faceInput && !XRSettings.enabled 
                ? forward * (Mathf.Max(0.15f, Vector3.Dot(forward, input.normalized)) * input.magnitude) 
                : input);
        }

        private void FaceDirection(Vector3 direction)
        {
            if (!updateRotation)
                return;

            if (direction.sqrMagnitude <= 0 && !UnityEngine.Device.Application.isMobilePlatform)
                return;

            var targetRotation = Quaternion.LookRotation(UsePitchRotation() 
                ? direction 
                : direction.FlattenDirection(ParentUp), ParentUp);
            
            if (instantRotation)
            {
                _physicsProvider.Rotation = targetRotation;
                return;
            }

            const float snapSpeed = 999;

            var rotation = !useLerpRotation
                ? Quaternion.RotateTowards(_physicsProvider.Rotation, targetRotation,
                    Time.deltaTime * (instantRotation ? snapSpeed : rotationSpeed))
                : Quaternion.Lerp(_physicsProvider.Rotation, targetRotation,
                    Time.deltaTime * (instantRotation ? snapSpeed : rotationSpeed));
            
            _physicsProvider.Rotation = rotation;
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            if (!MoveInputEnabled)
            {
                _movementInputVector = Vector3.zero;
                return;
            }
            
            if (!inputFieldBlocksInput && (MVUtils.IsUnityInputFieldFocused() || InputButtonEvent.WebViewCheckInputFieldFocusedCallback?.Invoke() == true))
            {
                _movementInputVector = Vector3.zero;
                return;
            }

            if (cursorUnlockedBlocksInput && !XRSettings.enabled && !UnityEngine.Device.Application.isMobilePlatform && Cursor.lockState != CursorLockMode.Locked)
            {
                _movementInputVector = Vector3.zero;
                return;
            }

            _movementInputVector = ctx.ReadValue<Vector2>();
        }

        private void OnMoveCancelled(InputAction.CallbackContext ctx)
        {
            _movementInputVector = Vector3.zero;
        }

        private bool UseVerticalMovement()
        {
            return allowVerticalMovement && !_physicsProvider.IsGrounded;
        }

        private bool UsePitchRotation()
        {
            return allowPitchRotation && !_physicsProvider.IsGrounded;
        }
    }
}
