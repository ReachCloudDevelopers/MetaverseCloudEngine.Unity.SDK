
#if MV_XR_TOOLKIT
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using MetaverseCloudEngine.Unity.Avatar.Components;
using MetaverseCloudEngine.Unity.Networking.Components;
using TriInspectorMVCE;
using UnityEngine.Events;
using UnityEngine.Animations;
using UnityEngine.Playables;
using MetaverseCloudEngine.Unity.Async;
#if MV_XR_TOOLKIT_3
using XRSocketInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
using XRBaseInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
using IXRSelectInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor;
using IXRInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor;
using IXRSelectInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
using InteractableSelectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode;
using IXRHoverInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRHoverInteractor;
#else
using XRSocketInteractor = UnityEngine.XR.Interaction.Toolkit.XRSocketInteractor;
using XRBaseInteractor = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor;
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable;
using IXRSelectInteractor = UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractor;
using IXRInteractor = UnityEngine.XR.Interaction.Toolkit.IXRInteractor;
using IXRSelectInteractable = UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractable;
using InteractableSelectMode = UnityEngine.XR.Interaction.Toolkit.InteractableSelectMode;
using IXRHoverInteractor = UnityEngine.XR.Interaction.Toolkit.IXRHoverInteractor;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// Allows players to interact with objects on VR and non-VR platforms. This class utilizes the 
    /// Unity XR Interaction Toolkit and derives from <see cref="XRBaseInteractable"/>.
    /// </summary>
    [DefaultExecutionOrder(int.MaxValue - 2)]
    [DeclareFoldoutGroup("Metaverse Interactions")]
    [HideMonoScript]
    [HelpURL("https://reach-cloud.gitbook.io/reach-explorer-documentation/metaverse-cloud-engine-sdk/unity-engine-sdk/components/interactions/metaverse-interactable")]
    public class MetaverseInteractable : XRBaseInteractable
    {
        #region Classes & Structs

        /// <summary>
        /// The selection mode to use for Non-VR use animation picking.
        /// </summary>
        public enum UseAnimationSelectMode
        {
            /// <summary>
            /// The animation will be selected sequentially.
            /// </summary>
            Sequential,
            /// <summary>
            /// The animation will be selected randomly within the list.
            /// </summary>
            Random,
        }

        /// <summary>
        /// Represents an instance of a non VR animation.
        /// </summary>
        [Serializable]
        public class NonVRAnimation
        {
            [Required]
            [Tooltip("The name of the non VR animation.")]
            public string name;
            [Tooltip("A cooldown time used to prevent another animation from begin played until this time passes.")]
            [Min(0)] public float cooldown;
            [Tooltip("Whether this animation should be played over the network. NOTE: If this animation is played extremely frequently, this may increase bandwidth.")]
            public bool networked = true;
            [Required]
            [Tooltip("The animation preset to use.")]
            public AvatarPlayableAnimationPreset preset;
            [Tooltip("Can this animation be selected using the " + nameof(PlayNextNonVRAnimation) + " method?")]
            public bool isSequencable = true;
            public UnityEvent onStarted;
            public UnityEvent onStopped;

            /// <summary>
            /// The currently active playable data on the animation.
            /// </summary>
            public List<AnimationClipPlayable> PlayableData { get; internal set; }
        }

        #endregion

        #region Const

        private const string UpperBodyStyleParam = "UpperBodyStyleID";
        private const float VelocityDamping = 1;
        private const float VelocityScale = 1;
        private const float GlobalInteractionCooldown = 0.25f;
        private const int MaxVelocityFrames = 5;
        private const float InterpToTargetPositionDuration = 0.25f;

        #endregion

        #region Inspector
        
        [Title("General")]
        [Group("Metaverse Interactions")]
        [Tooltip("If this is true, and another player is holding this object, another player will be able to take it from them.")]
        [SerializeField] private bool canBeStolen;

        [Title("Attachment")]
        [InfoBox("Define settings for attaching to the interactor.")]
        [Tooltip("Enables the player to rotate the object while holding it.")]
        [Group("Metaverse Interactions")]
        [SerializeField] private bool enableRotation = true;
        [Tooltip("Defines specific attach points for the left and right hands.")]
        [Group("Metaverse Interactions")]
        [SerializeField] private MetaverseInteractableAttachPoint[] attachPoints;
        [Tooltip("If greater than -1, will attach this interactable to the first empty socket with the given type whenever this interactable is de-selected completely.")]
        [Group("Metaverse Interactions")]
        [SerializeField, Min(-1)] private int nonVRDetachSocketType = -1;

        [Title("Physics")]
        [InfoBox("Define settings to use for physics interaction.")]
        [Tooltip("If true, will use physics based tracking for VR and Non-VR interactors.")]
        [Group("Metaverse Interactions")]
        [SerializeField] private bool usePhysicsTracking;
        [Tooltip("Whenever the player is using VR physics, this will allow the player to collide with the object they're holding.")]
        [Group("Metaverse Interactions")]
        [SerializeField, LabelText("Collide w/ Player")] private bool enablePlayerCollision;
        [Tooltip("The maximum distance from the original grab point that the interactor can be before the grab breaks.")]
        [Group("Metaverse Interactions")]
        [SerializeField, Range(0.1f, 1f)] private float breakDistance = 0.1f;

        [Title("Non VR Animations")]
        [InfoBox("Define settings to use for Non-VR player animation.")]
        [Group("Metaverse Interactions")][SerializeField, LabelText("Non VR Secondary Hand IK Target")] private UnityIkTarget leftHandIKTarget;
        [Group("Metaverse Interactions")][SerializeField, LabelText("Non VR Primary Hand Aim")] private bool nonVRRightHandAim = true;
        [Tooltip("The style to use for the character's upper body. ('-1' = None), ('0' = 1 Handed), ('1' = 2 Handed)")]
        [Group("Metaverse Interactions")][SerializeField, Range(-1, 1), LabelText("Non VR Upper Body Style ID")] private int upperBodyStyleID;
        [Group("Metaverse Interactions")][SerializeField] private UseAnimationSelectMode nonVRAnimationSelectMode = UseAnimationSelectMode.Sequential;
        [Group("Metaverse Interactions")][SerializeField] private float nonVRSequenceResetCooldown = 1f;
        [Group("Metaverse Interactions")][SerializeField] private NonVRAnimation[] nonVRAnimations;

        #endregion

        #region Private Fields

        private readonly List<IXRSelectInteractor> _interactors = new();

        private Transform _transform;
        private Vector3 _initialDelta;
        private Vector3 _interactor0RelativeAttachPosition;
        private Quaternion _initialRotation;
        private Quaternion _initialRotationOffset;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _interpPosition;
        private Quaternion _interpRotation;
        private float _currentInterpToTargetTime;
        private Rigidbody _rootRigidbody;
        private Transform _player;
        private Vector3 _lastPlayerPos;
        private Quaternion _lastPlayerRot;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3[] _velocityFrames;
        private Vector3[] _angularVelocityFrames;
        private int _velocityFrame;
        private LayerMask _originalRigidbodyExcludeLayers;

        private PlayerAvatarContainer _playerAvatar;
        private float _currentNonVRAnimationCooldown;
        private float _currentNonVRSequenceResetCooldown;
        private int _currentNonVRAnimationSequenceIndex;
        private NonVRAnimation[] _sequenceAbleNonVRAnims;
        private Dictionary<string, NonVRAnimation> _animationLookup;

        private bool _isInSocket;
        private bool _isInPlayerSocket;
        private bool _isNonVrInteractor;
        private bool _isChildInteractable;
        private bool _initialGrab = true;
        private float _defaultAngularSpeed;
        private MetaverseXRController _remoteSelector;

        private MetaverseInteractableAttachPoint _hand1AttachPoint;
        private MetaverseInteractableAttachPoint _hand2AttachPoint;

        private readonly Dictionary<IXRInteractor, Transform> _physicsAttachPoints = new();
        private readonly Dictionary<IXRInteractor, Rigidbody> _attachedPhysicsBonesMap = new();
        private float _attachmentBreakCheckCooldown;
        private bool _isPhysicsAttachment;
        private static readonly int UpperBodyStyleAnimationHash = Animator.StringToHash(UpperBodyStyleParam);
        
        private readonly Dictionary<IXRInteractor, Vector3> _interactorAttachPositionOffsets = new();

        #endregion

        #region Properties

        /// <summary>
        /// Whether to allow multiple interactors to select this interactable at the same time.
        /// </summary>
        public bool AllowMultipleInteractors => selectMode == InteractableSelectMode.Multiple;

        /// <summary>
        /// Whether to allow the player to steal this interactable from another player.
        /// </summary>
        public bool CanBeStolen
        {
            get => canBeStolen;
            set => canBeStolen = value;
        }

        /// <summary>
        /// Modifies the behavior of the interactable to be suitable for climbing.
        /// </summary>
        public bool IsClimbable { get; set; }

        /// <summary>
        /// Whether to allow the player to attach to flat surfaces when <see cref="IsClimbable"/> is true. This can prevent the 
        /// player from grabbing something they're standing on, or causing a "spring" effect if they pull themselves into a wall tightly
        /// and let go.
        /// </summary>
        public bool AllowFlatSurfaceClimbing { get; set; }

        /// <summary>
        /// Whether to allow the player to collide with the object they're holding.
        /// </summary>
        public bool CollideWithPlayer { get => enablePlayerCollision; set => enablePlayerCollision = value; }

        /// <summary>
        /// The maximum distance from the original grab point that the interactor can be before the grab breaks.
        /// </summary>
        public float PhysicsAttachmentBreakDistance { get => breakDistance; set => breakDistance = value; }

        /// <summary>
        /// The primary hand's attach point for this interactable.
        /// </summary>
        public MetaverseInteractableAttachPoint Hand1AttachPoint => _hand1AttachPoint;

        /// <summary>
        /// The secondary hand's attach point for this interactable.
        /// </summary>
        public MetaverseInteractableAttachPoint Hand2AttachPoint => _hand2AttachPoint;

        /// <summary>
        /// Invoked when a non VR animation is started.
        /// </summary>
        public event Action<NonVRAnimation> NonVRAnimationStarted;

        /// <summary>
        /// Invoked when a non VR animation is stopped.
        /// </summary>
        public event Action<NonVRAnimation> NonVRAnimationStopped;

        /// <summary>
        /// Gets the currently active interaction cooldown.
        /// </summary>
        public static float GlobalInteractionCooldownTime { get; private set; }

        #endregion

        #region Unity Events

        protected override void Awake()
        {
            _transform = transform;

            if (colliders is { Count: 0 })
                colliders.AddRange(GetComponentsInChildren<Collider>(true));

            base.Awake();
        }

        protected override void Reset()
        {
            selectMode = InteractableSelectMode.Multiple;

#if UNITY_EDITOR
            var interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                if (gameObject.layer != interactableLayer && UnityEditor.EditorUtility.DisplayDialog("Update Layer", "Would you like to change this object's layer to 'Interactable'?", "Yes", "No"))
                {
                    gameObject.layer = interactableLayer;

                    var defaultLayer = LayerMask.NameToLayer("Default");
                    var children = gameObject.GetComponentsInChildren<Transform>().Where(x => x.gameObject.layer != defaultLayer).ToArray();
                    foreach (var child in children)
                        child.gameObject.layer = interactableLayer;
                }
            }
#endif

            base.Reset();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                CalculateSequenceableAnimations();
        }

        private void Start()
        {
            colliders.Clear();
            colliders.AddRange(gameObject.GetTopLevelComponentsInChildrenOrdered<Collider, MetaverseInteractable>());

            if (_transform)
                _rootRigidbody = transform.GetComponent<Rigidbody>();
            if (_rootRigidbody)
            {
                _rootRigidbody.interpolation = RigidbodyInterpolation.None;
                MetaverseProgram.Logger.LogWarning(
                    "Rigidbody interpolation is not supported on interactables. " +
                           "This will cause issues when moving the object.");
            }
        }

        private void Update()
        {
            if (_interactors.Count <= 0) return;
            UpdateGrab(false);
        }

        private void LateUpdate()
        {
            if (_interactors.Count <= 0) return;
            UpdateGrab(false);
        }

        private void FixedUpdate()
        {
            if (_interactors.Count <= 0) return;
            UpdateGrab(true);
            ArtificialBreaking();
            TrackVelocity();
        }

        private void OnTransformParentChanged()
        {
            DetectIfChildInteractable();
        }

        #endregion

        #region Protected Methods

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            
            if (args.interactorObject == null || args.interactorObject.transform == null)
            {
                SelectionFail(args);
                return;
            }

            var controller = args.interactorObject.transform.GetComponent<MetaverseXRController>();
            
            if (!HandleSocketInteractions(args))
            {
                if (!controller || MVUtils.CachedTime < GlobalInteractionCooldownTime)
                {
                    SelectionFail(args);
                    return;
                }
            }

            InteractCooldown();

            TryInitPhysicalAttachment(args, controller, out var physicsBoneRb, out var physicsAttachPoint);

            if (attachPoints is { Length: > 0 })
            {
                var attachPoint = GetBestAttachPoint(controller && controller.Hand == MetaverseXRController.HandType.Left, _interactors.Count == 0);
                if (attachPoint == null)
                {
                    interactionManager.SelectExit(args.interactorObject, this);
                    return;
                }

                InitAttachPoint(controller, attachPoint, physicsAttachPoint);
            }
            else if (!TryComputePhysicsAttachPoint(controller, physicsAttachPoint))
            {
                SelectionFail(args);
                return;
            }

            BindToPhysicsAttachPoint(controller, physicsBoneRb, physicsAttachPoint);
            
            RegisterInteractor(args);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            if (!_interactors.Remove(args.interactorObject))
                return;
            
            _interactorAttachPositionOffsets.Remove(args.interactorObject);
            
            ExitPhysics(args);
            UpdatePrimaryInteractor();
            OnInteractorsChanged();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stop a specific animation if it is playing.
        /// </summary>
        /// <param name="animationName">The name of the animation to stop.</param>
        public void StopNonVRAnimation(string animationName)
        {
            if (!_isNonVrInteractor)
                return;

            if (_animationLookup == null || _playerAvatar == null)
                return;

            if (!_animationLookup.TryGetValue(animationName, out var anim) || 
                !anim.preset ||
                anim.PlayableData is not { Count: > 0 }) 
                return;
            
            foreach (var playingAnim in anim.PlayableData)
                _playerAvatar.StopAnimation(playingAnim, anim.preset.fadeOutTime);
        }

        /// <summary>
        /// Play the next available non VR animation.
        /// </summary>
        public void PlayNextNonVRAnimation()
        {
            CalculateSequenceableAnimations();

            if (_sequenceAbleNonVRAnims.Length == 0)
                return;

            var nextAnimation = nonVRAnimationSelectMode switch
            {
                UseAnimationSelectMode.Sequential => _sequenceAbleNonVRAnims[_currentNonVRAnimationSequenceIndex % _sequenceAbleNonVRAnims.Length],
                UseAnimationSelectMode.Random => _sequenceAbleNonVRAnims[UnityEngine.Random.Range(0, _sequenceAbleNonVRAnims.Length)],
                _ => null
            };

            if (!PlayNonVRAnimation(nextAnimation)) 
                return;
            
            _currentNonVRSequenceResetCooldown = MVUtils.CachedTime + nonVRSequenceResetCooldown;
            _currentNonVRAnimationSequenceIndex++;

            if (_currentNonVRAnimationSequenceIndex == 1)
                StartCoroutine(ResetSequenceIndex());
            return;

            IEnumerator ResetSequenceIndex()
            {
                while (Time.time < _currentNonVRSequenceResetCooldown)
                    yield return null;
                _currentNonVRAnimationSequenceIndex = 0;
            }
        }

        /// <summary>
        /// Play a non VR animation by it's name.
        /// </summary>
        /// <param name="animationName">The name of the non VR animation.</param>
        public void PlayNonVRAnimation(string animationName)
        {
            if (nonVRAnimations == null || nonVRAnimations.Length == 0)
                return;

            _animationLookup ??= new Dictionary<string, NonVRAnimation>();
            foreach (var a in nonVRAnimations)
                _animationLookup[a.name] = a;

            if (_animationLookup.TryGetValue(animationName, out var anim))
                PlayNonVRAnimation(anim);
        }

        /// <summary>
        /// Play a non-vr animation from a specific preset.
        /// </summary>
        /// <param name="anim">The animation preset to play.</param>
        public void PlayNonVRAnimation(AvatarPlayableAnimationPreset anim)
        {
            PlayNonVRAnimation(new NonVRAnimation { preset = anim });
        }

        /// <summary>
        /// Resets the non vr animation cooldown.
        /// </summary>
        public void ResetNonVRAnimationCooldown()
        {
            _currentNonVRAnimationCooldown = 0;
        }

        /// <summary>
        /// Call this to globally add a cooldown to interactions.
        /// </summary>
        public static void InteractCooldown()
        {
            GlobalInteractionCooldownTime = MVUtils.CachedTime + GlobalInteractionCooldown;
        }

        /// <summary>
        /// Gets the target position of this interactable relative to the interactor attach point and interactable attach point.
        /// </summary>
        /// <param name="interactableAttachPoint">The interactable's attach point.</param>
        /// <param name="interactorAttachPoint">The interactor's attach point.</param>
        /// <param name="nonXR">Whether this is a simulated XR grab or not.</param>
        /// <returns>The final world position that this interactor should be at.</returns>
        public Vector3 GetInteractPosition(Transform interactableAttachPoint, Transform interactorAttachPoint, bool nonXR)
        {
            const float yOffset = -0.04f;
            const float zOffset = -0.1f;
            var worldPos = interactableAttachPoint || nonXR 
                ? interactorAttachPoint.position + interactorAttachPoint.rotation * new Vector3(0, yOffset, zOffset) 
                : interactorAttachPoint.position + _transform.rotation * _interactor0RelativeAttachPosition;
            
            if (!interactableAttachPoint) 
                return worldPos;
            
            var offset = _transform.rotation * _transform.InverseTransformPointUnscaled(interactableAttachPoint.position);
            worldPos -= offset;
            return worldPos;
        }

        /// <summary>
        /// Gets the best attach point for the given hand.
        /// </summary>
        /// <param name="leftHand">Whether this is the left or right hand. If <see langword="true"/> then it will be counted as left, otherwise right.</param>
        /// <param name="firstGrab">Whether this is the initial grab or the second grab.</param>
        /// <returns>The best interactable attach point.</returns>
        public MetaverseInteractableAttachPoint GetBestAttachPoint(bool leftHand, bool firstGrab)
        {
            if (attachPoints == null || attachPoints.Length == 0)
                return null;

            var attachPoint = attachPoints.FirstOrDefault(x =>
                (leftHand ? x.allowedNodes.HasFlag(MetaverseInteractableAttachPoint.AllowedNodes.LeftHand) : x.allowedNodes.HasFlag(MetaverseInteractableAttachPoint.AllowedNodes.RightHand)) &&
                (x.attachIndex == 0 || (firstGrab ? x.attachIndex.HasFlag(MetaverseInteractableAttachPoint.AttachIndex.First) : x.attachIndex.HasFlag(MetaverseInteractableAttachPoint.AttachIndex.Second))));
            return attachPoint;
        }

        /// <summary>
        /// Call this to notify the interactable that it has been grabbed remotely.
        /// </summary>
        /// <param name="controller">The controller that is grabbing this interactable.</param>
        /// <param name="playerNetworkObject">The player object.</param>
        internal void OnRemoteSelectEnter(MetaverseXRController controller, NetworkObject playerNetworkObject)
        {
            if (_remoteSelector)
                return;

            _remoteSelector = controller;
            _isNonVrInteractor = controller && controller.IsNonVR;
            RegisterAvatarForPlayer(playerNetworkObject.gameObject);
            SetPassthroughCollision(playerNetworkObject.gameObject, true);
        }

        /// <summary>
        /// Call this to notify the interactable that it has been un-grabbed remotely.
        /// </summary>
        /// <param name="controller">The controller that is leaving the active selection.</param>
        /// <param name="playerNetworkObject"></param>
        internal void OnRemoteSelectExit(MetaverseXRController controller, NetworkObject playerNetworkObject)
        {
            if (!_remoteSelector || (controller && _remoteSelector != controller))
                return;
            
            UnregisterAvatar();
            if (playerNetworkObject)
                SetPassthroughCollision(playerNetworkObject.gameObject, false);
            _remoteSelector = null;
        }

        /// <summary>
        /// Determines whether this object is selectable by the given interactor.
        /// </summary>
        /// <param name="interactor">The interactor trying to select this interactable.</param>
        /// <returns>A value indicating whether this interactable is selectable.</returns>
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!enabled) return false;
            if (!interactor.IsSelecting(this) && MVUtils.CachedTime < GlobalInteractionCooldownTime) return false;
            if (IsClimbable) return CanClimb(interactor);
            if (_remoteSelector && !canBeStolen) return false;
            if (!CanSelectInSocket(interactor)) return false;
            return base.IsSelectableBy(interactor) && (AllowMultipleInteractors || _interactors.Count < 1 || _interactors.Contains(interactor));
        }

        /// <summary>
        /// Determines whether this object is hoverable by the given interactor.
        /// </summary>
        /// <param name="interactor">The interactor trying to hover this interactable.</param>
        /// <returns>A value indicating whether this interactable is hoverable.</returns>
        public override bool IsHoverableBy(IXRHoverInteractor interactor)
        {
            if (!enabled) return false;
            if (IsClimbable) return CanClimb(interactor);
            if (_remoteSelector && !canBeStolen) return false;
            return CanSelectInSocket(interactor) && base.IsHoverableBy(interactor);
        }
        
        /// <summary>
        /// Updates the player avatar animation based on the current upper body style.
        /// </summary>
        public void UpdateAvatarAnimation()
        {
            if (upperBodyStyleID == -1 || !_playerAvatar) return;
            if (_playerAvatar.OwnAnimator && _playerAvatar.OwnAnimator.runtimeAnimatorController)
                _playerAvatar.OwnAnimator.SetInteger(UpperBodyStyleAnimationHash, upperBodyStyleID);
            if (_playerAvatar.Avatar && _playerAvatar.Avatar.runtimeAnimatorController)
                _playerAvatar.Avatar.SetInteger(UpperBodyStyleAnimationHash, upperBodyStyleID);
            if (leftHandIKTarget)
                leftHandIKTarget.Animator = _playerAvatar.OwnAnimator;
        }

        /// <summary>
        /// Forces the local player (identified by the <see cref="MetaverseInteractableLocalPlayerInteractorIdentifier"/>
        /// component) to select this interactable.
        /// </summary>
        public void ForceLocalPlayerSelection()
        {
            var localPlayer = FindObjectOfType<MetaverseInteractableLocalPlayerInteractorIdentifier>();
            if (localPlayer && interactionManager)
                interactionManager.SelectEnter(localPlayer.GetComponent<IXRSelectInteractor>(), this);
        }

        /// <summary>
        /// Forces this interactable to be deselected by all currently selecting interactors.
        /// </summary>
        public void ForceDeselection()
        {
            if (!interactionManager) return;
            foreach (var interactor in _interactors)
                if (interactor != null && interactor.transform) interactionManager.SelectExit(interactor, this);
        }

        public bool MoveToSocketOrDeselect()
        {
            if (!_player)
                return false;
            if (!_isNonVrInteractor)
                return false;

            if (nonVRDetachSocketType == -1)
            {
                ExitAll();
                return true;
            }
            
            var bestEmptySocket = _player
                .GetComponentsInChildren<MetaverseSocketIdentifier>()
                .Where(x => x.socketType == nonVRDetachSocketType)
                .Select(x => x.GetComponent<XRSocketInteractor>())
                .FirstOrDefault(x => !x.hasSelection);
            
            if (!bestEmptySocket)
            {
                ExitAll();
                return false;
            }

            ExitAll();
            
            MetaverseDispatcher.AtEndOfFrame(() =>
            {
                if (!this) return;
                if (!bestEmptySocket) return;
                if (!bestEmptySocket.interactionManager) return;
                bestEmptySocket.interactionManager.SelectEnter(bestEmptySocket, (IXRSelectInteractable)this);
            });

            return true;
            
            void ExitAll()
            {
                foreach (var interactor in _interactors.ToArray())
                    interactionManager.SelectExit(interactor, this);
            }
        }

        #endregion

        #region Private Methods

        private bool CanSelectInSocket(IXRInteractor interactor)
        {
            return !_isInPlayerSocket ||
                   !interactor.transform.TryGetComponent(out MetaverseXRController controller) ||
                   !controller.IsNonVR;
        }

        private void CalculateSequenceableAnimations()
        {
            if (nonVRAnimations == null)
                return;
            if (_sequenceAbleNonVRAnims is null || _sequenceAbleNonVRAnims.Length != nonVRAnimations.Length)
                _sequenceAbleNonVRAnims = nonVRAnimations.Where(x => x.isSequencable).ToArray();
        }

        private void RegisterInteractor(SelectEnterEventArgs args)
        {
            if (_interactors.Count == 0)
                OnInitialSelectEnter(args);

            _interactors.Add(args.interactorObject);

            if (_player)
                SetPassthroughCollision(_player.gameObject, true);
            
            OnInteractorsChanged();
        }

        private void InitAttachPoint(MetaverseXRController controller, MetaverseInteractableAttachPoint attachPoint, Transform physicsAttachPoint)
        {
            if (_interactors.Count == 0)
            {
                _hand1AttachPoint = attachPoint;
                _hand1AttachPoint.IsInteracting = true;
                _hand1AttachPoint.events?.onSelectEntered?.Invoke();
            }
            else
            {
                _hand2AttachPoint = attachPoint;
                _hand2AttachPoint.IsInteracting = true;
                _hand2AttachPoint.events?.onSelectEntered?.Invoke();
            }

            if (physicsAttachPoint)
            {
                physicsAttachPoint.SetParent(attachPoint.transform, false);
                physicsAttachPoint.ResetLocalTransform();
                physicsAttachPoint.transform.localPosition = -GetPhysicsHandOffsetForController(controller);
            }
        }

        private void TryInitPhysicalAttachment(SelectEnterEventArgs args, MetaverseXRController controller, out Rigidbody bone, out Transform attachPoint)
        {
            bone = null;
            attachPoint = null;

            if (!CanPhysicallyAttachTo(controller))
                return;

            var physicsBone = controller.PhysicsRig.GetPhysicsBone(controller.Hand == MetaverseXRController.HandType.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (!physicsBone || !physicsBone.TryGetComponent(out bone))
                return;

            attachPoint = GenerateInteractorPhysicsAttachPoint();

            _attachmentBreakCheckCooldown = Time.time + 0.25f;
            _physicsAttachPoints[args.interactorObject] = attachPoint;
            _attachedPhysicsBonesMap[args.interactorObject] = bone;
            _isPhysicsAttachment = true;
        }

        private static Vector3 GetPhysicsHandOffsetForController(MetaverseXRController controller)
        {
            const float physicsPalmOffset = 0.1f;
            return new Vector3((controller.Hand == MetaverseXRController.HandType.Left ? physicsPalmOffset : -physicsPalmOffset) / 2f, 0, physicsPalmOffset);
        }

        private bool TryComputePhysicsAttachPoint(MetaverseXRController controller, Transform physicsAttachPoint)
        {
            if (!_isPhysicsAttachment)
                return true;

            if (!controller)
                return true;

            var handOffset = GetPhysicsHandOffsetForController(controller);
            var controllerTransform = controller.transform;
            var physicsTargetPos = controllerTransform.TransformPoint(handOffset + controller.IKOffset);
            var closestPoint = colliders.Select(x => new
            {
                Collider = x,
                ClosestPoint = x.ClosestPoint(physicsTargetPos)

            }).OrderBy(x => Vector3.Distance(x.ClosestPoint, physicsTargetPos)).First();

            var handNormal = controllerTransform.right;
            if (controller.Hand == MetaverseXRController.HandType.Left)
                handNormal = -handNormal;

            var rayDir = (closestPoint.ClosestPoint - physicsTargetPos).normalized;
            var normal = closestPoint.Collider.Raycast(new Ray(physicsTargetPos, rayDir), out var hit, Mathf.Infinity) ? hit.normal : handNormal;
            const float minimumGrabSurfaceAngle = 0.4f;
            if (Vector3.Dot(handNormal, normal) > minimumGrabSurfaceAngle)
            {
                if (!AllowFlatSurfaceClimbing && controller.Root && IsClimbable)
                {
                    const float maximumGrabSurfaceAngle = 0.65f;
                    if (Vector3.Dot(normal, controller.Root.up) > maximumGrabSurfaceAngle)
                        return false;
                }

                var fwd = Vector3.ProjectOnPlane(controllerTransform.forward, normal).normalized;
                var rot = Quaternion.LookRotation(fwd, normal) * Quaternion.Euler(0, 0, controller.Hand == MetaverseXRController.HandType.Left ? -90 : 90);
                physicsAttachPoint.SetPositionAndRotation(closestPoint.ClosestPoint - (rot * handOffset), rot);
            }
            else
            {
                physicsAttachPoint.SetPositionAndRotation(closestPoint.ClosestPoint - (physicsTargetPos - controllerTransform.TransformPoint(controller.IKOffset)), controllerTransform.rotation);
            }

            return true;
        }

        private void BindToPhysicsAttachPoint(MetaverseXRController controller, Rigidbody physicsBone, Transform attachPoint)
        {
            if (!_isPhysicsAttachment)
                return;

            if (!controller)
                return;

            var physicsJoint = new GameObject("Joint").transform;
            physicsJoint.SetParent(attachPoint, false);
            physicsJoint.gameObject.AddComponent<Rigidbody>();

            var rootBinding = physicsJoint.gameObject.AddComponent<FixedJoint>();
            rootBinding.connectedBody = _rootRigidbody;

            var physicsBoneTransform = physicsBone.transform;
            var oldPos = physicsBoneTransform.position;
            var oldRot = physicsBoneTransform.rotation;
            var controllerRotation = controller.transform.rotation;
            physicsBoneTransform.SetPositionAndRotation(
                physicsJoint.position + (Quaternion.Inverse(controllerRotation) * (controller.transform.TransformPoint(controller.IKOffset) - oldPos)),
                physicsJoint.rotation * (Quaternion.Inverse(controllerRotation) * oldRot));

            var attachPointBinding = physicsJoint.gameObject.AddComponent<ConfigurableJoint>();
            attachPointBinding.enableCollision = false;
            attachPointBinding.connectedBody = physicsBone;

            var drive = new JointDrive
            {
                maximumForce = 5000,
                positionSpring = float.MaxValue,
                positionDamper = 1,
            };
            attachPointBinding.xDrive = attachPointBinding.yDrive = attachPointBinding.zDrive = drive;
            if (!IsClimbable)
                attachPointBinding.angularXDrive = attachPointBinding.angularYZDrive = drive;

            physicsBoneTransform.SetPositionAndRotation(oldPos, oldRot);
        }

        private bool CanPhysicallyAttachTo(MetaverseXRController controller)
        {
            return (usePhysicsTracking || IsClimbable)
                && controller
                && controller.PhysicsRig && controller.PhysicsRig.IsActive
                && _rootRigidbody && (!_rootRigidbody.isKinematic || IsClimbable)
                && colliders.Count > 0;
        }

        private bool PlayNonVRAnimation(NonVRAnimation anim)
        {
            if (!_isNonVrInteractor)
                return false;

            if (!_playerAvatar)
                return false;

            if (nonVRAnimations == null || nonVRAnimations.Length == 0)
                return false;

            if (MVUtils.CachedTime < _currentNonVRAnimationCooldown)
                return false;

            if (anim == null || !anim.preset || !anim.preset.clip)
                return false;
            
            var playableClip = _playerAvatar.PlayAnimation(
                anim.preset.clip,
                anim.preset.clip.isLooping ? Mathf.Infinity : anim.preset.clip.length,
                anim.preset.mask,
                anim.preset.fadeInTime,
                anim.preset.fadeOutTime);

            anim.PlayableData ??= new List<AnimationClipPlayable>();
            anim.PlayableData.Add(playableClip);
            anim.onStarted?.Invoke();

            MetaverseDispatcher.WaitUntil(() => !this || !playableClip.IsValid() || playableClip.IsDone(), () =>
            {
                if (!this) return;
                anim.PlayableData.Remove(playableClip);
                anim.onStopped?.Invoke();
                NonVRAnimationStopped?.Invoke(anim);
            });
                
            _currentNonVRAnimationCooldown = MVUtils.CachedTime + anim.cooldown;
            NonVRAnimationStarted?.Invoke(anim);
            return true;

        }

        private void OnInitialSelectEnter(SelectEnterEventArgs args)
        {
            _currentInterpToTargetTime = 0;
            _interpPosition = _transform.position;
            _interpRotation = _transform.rotation;
            
            _isInSocket = args.interactorObject is XRSocketInteractor;

            DetectIfChildInteractable();

            _initialGrab = !_isChildInteractable;

            FindPlayerFromSelectionArgs(args);

            if (_rootRigidbody)
            {
                _defaultAngularSpeed = _rootRigidbody.maxAngularVelocity;
                _rootRigidbody.maxAngularVelocity = Mathf.Infinity;
                _originalRigidbodyExcludeLayers = _rootRigidbody.excludeLayers; 
            }

            if (args.interactorObject.transform.TryGetComponent(out MetaverseXRController controller))
                controller.SetAimMode(nonVRRightHandAim);
        }

        private void FreezeRootRigidbody()
        {
            if (!_rootRigidbody) return;

            var currentPosition = _targetPosition;
            var currentRotation = _targetRotation;

            Freeze();
            
            UniTask.Void(async token =>
            {
                Freeze();

                const int framesToFreezeFor = 3;
                var frameCount = 0;
                while (frameCount < framesToFreezeFor && !token.IsCancellationRequested)
                {
                    await UniTask.WaitForFixedUpdate(token);
                    frameCount++;
                    Freeze();
                }
                
                if (!token.IsCancellationRequested)
                    Freeze();
                
            }, this.GetCancellationTokenOnDestroy());
            return;

            void Freeze()
            {
                if (!this) return;
                // ReSharper disable Unity.InefficientPropertyAccess
                _rootRigidbody.isKinematic = true;
                _rootRigidbody.isKinematic = false;
                // ReSharper restore Unity.InefficientPropertyAccess
                _rootRigidbody.SetLinearVelocity(Vector3.zero);
                _rootRigidbody.angularVelocity = Vector3.zero;
                _rootRigidbody.Sleep();
                _rootRigidbody.WakeUp();
                _rootRigidbody.position = _transform.position = currentPosition;
                _rootRigidbody.rotation = _transform.rotation = currentRotation;
            }
        }

        private Transform GenerateInteractorPhysicsAttachPoint()
        {
            var physicsAttachPoint = new GameObject("Attach Point").transform;
            physicsAttachPoint.SetParent(_rootRigidbody.transform, false);
            return physicsAttachPoint;
        }

        private void ExitPhysics(SelectExitEventArgs args)
        {
            if (_physicsAttachPoints.TryGetValue(args.interactorObject, out var physicsAttachPoint))
            {
                if (physicsAttachPoint)
                    Destroy(physicsAttachPoint.gameObject);

                _physicsAttachPoints.Remove(args.interactorObject);
            }

            _attachedPhysicsBonesMap.Remove(args.interactorObject);

            if (args.interactorObject is XRSocketInteractor socket && _transform.IsChildOf(socket.attachTransform))
                _transform.SetParent(null);

            if (_player)
                SetPassthroughCollision(_player.gameObject, false);

            if (_interactors.Count > 0)
            {
                if (_player)
                    SetPassthroughCollision(_player.gameObject, true);
            }
            else
            {
                _isPhysicsAttachment = false;
            }
        }

        private void UpdatePrimaryInteractor()
        {
            if (_interactors.Count == 1)
            {
                if (_hand2AttachPoint != null)
                {
                    _hand2AttachPoint.IsInteracting = false;
                    _hand2AttachPoint.events?.onSelectExited?.Invoke();
                    _hand2AttachPoint = null;
                }

                if (_hand1AttachPoint != null)
                {
                    _hand1AttachPoint.IsInteracting = false;
                    _hand1AttachPoint.events?.onSelectExited?.Invoke();
                    _hand1AttachPoint = null;

                    var controller = _interactors[0].transform.GetComponent<XRController>();
                    var attachPoint = GetBestAttachPoint(controller && controller.controllerNode == UnityEngine.XR.XRNode.LeftHand, true);
                    if (attachPoint == null)
                    {
                        interactionManager.SelectExit(_interactors[0], this);
                        _interactors.Clear();
                    }
                    else
                    {
                        _hand1AttachPoint = attachPoint;
                        _hand1AttachPoint.IsInteracting = true;
                    }
                }
            }
        }

        private bool CanClimb(IXRInteractor interactor)
        {
            if (!IsClimbable) return false;
            if (_isNonVrInteractor) return false;
            if (interactor.transform.TryGetComponent(out MetaverseXRController xrController))
                return xrController.PhysicsHand;
            return false;
        }

        private void SelectionFail(SelectEnterEventArgs args)
        {
            if (_physicsAttachPoints.TryGetValue(args.interactorObject, out var physicsAttachPoint))
                Destroy(physicsAttachPoint.gameObject);

            _physicsAttachPoints.Remove(args.interactorObject);
            _attachedPhysicsBonesMap.Remove(args.interactorObject);

            interactionManager.SelectExit(args.interactorObject, this);
        }

        private void FindPlayerFromSelectionArgs(SelectEnterEventArgs args)
        {
            _isNonVrInteractor = 
                _isInSocket ||
                     (args.interactorObject.transform.TryGetComponent(
                         out MetaverseXRController controller) && controller.IsNonVR);

            var playerObject = FindPlayerObject(args);
            if (_isInSocket)
            {
                _isInPlayerSocket = playerObject;
                return;
            }

            if (playerObject)
                _player = playerObject.transform;

            if (_player)
            {
                _lastPlayerPos = _player.position;
                _lastPlayerRot = _player.rotation;

                if (_isNonVrInteractor)
                    RegisterAvatarForPlayer(_player.gameObject);
            }
        }

        private static Transform FindPlayerObject(SelectEnterEventArgs args)
        {
            var playerObject = args.interactorObject.transform.GetComponentsInParent<Transform>()
                .FirstOrDefault(x => x.CompareTag("Player"));
            if (!playerObject)
            {
                var origin = args.interactableObject.transform.GetComponentInParent<XROrigin>();
                if (origin)
                    playerObject = origin.transform;
            }

            return playerObject;
        }

        private void RegisterAvatarForPlayer(GameObject player)
        {
            _playerAvatar = player.GetComponentInChildren<PlayerAvatarContainer>(true);
            if (!_playerAvatar || !_playerAvatar.OwnAnimator) 
                return;
            
            _playerAvatar.Events.onAvatarSpawned.AddListener(OnAvatarSpawned);
            if (_playerAvatar.Avatar)
                OnAvatarSpawned(_playerAvatar.Avatar);

            if (leftHandIKTarget)
                leftHandIKTarget.Animator = _playerAvatar.OwnAnimator;
        }

        private void UnregisterAvatar()
        {
            if (leftHandIKTarget)
                leftHandIKTarget.Animator = null;

            if (_playerAvatar)
            {
                if (nonVRAnimations != null)
                    foreach (var anim in nonVRAnimations)
                    {
                        if (anim.PlayableData is not { Count: > 0 }) 
                            continue;
                        foreach (var playingAnim in anim.PlayableData)
                            playingAnim.Destroy();
                        anim.PlayableData.Clear();
                    }

                _playerAvatar.Events.onAvatarSpawned.RemoveListener(OnAvatarSpawned);

                if (_playerAvatar.OwnAnimator)
                    _playerAvatar.OwnAnimator.SetInteger(UpperBodyStyleAnimationHash, -1);
            }

            _playerAvatar = null;
        }

        private void OnAvatarSpawned(Animator avatar)
        {
            _playerAvatar.OwnAnimator.SetInteger(UpperBodyStyleAnimationHash, upperBodyStyleID);
            avatar.SetInteger(UpperBodyStyleAnimationHash, upperBodyStyleID);
        }

        private void OnDeselectedCompletely()
        {
            UnregisterAvatar();

            if (_rootRigidbody)
            {
                _rootRigidbody.maxAngularVelocity = _defaultAngularSpeed;
                _rootRigidbody.excludeLayers = _originalRigidbodyExcludeLayers;
                if (_isNonVrInteractor)
                    FreezeRootRigidbody();
                else
                    ApplyReleaseVelocity();
            }

            _isNonVrInteractor = false;
            _player = null;

            var childInteractables = GetComponentsInChildren<MetaverseInteractable>(true);
            foreach (var child in childInteractables)
                child.DetectIfChildInteractable();

            _initialGrab = !_isChildInteractable;

            if (_hand1AttachPoint is not null)
            {
                _hand1AttachPoint.IsInteracting = false;
                _hand1AttachPoint.events?.onSelectExited?.Invoke();
            }
            
            _hand1AttachPoint = null;

            if (_hand2AttachPoint is not null)
            {
                _hand2AttachPoint.IsInteracting = false;
                _hand2AttachPoint.events?.onSelectExited?.Invoke();
            }
            
            _hand2AttachPoint = null;
        }

        private void ApplyReleaseVelocity()
        {
            if (_isNonVrInteractor || _isPhysicsAttachment)
                return;
            if (!_rootRigidbody || _rootRigidbody.isKinematic)
                return;
            try
            {
                if (_velocityFrames == null || _velocityFrames.Length == 0)
                    return;
                
                var frames = Mathf.Min(MaxVelocityFrames, _velocityFrame);
                var velocity = Vector3.zero;
                var angularVelocity = Vector3.zero;
                for (var i = 0; i < frames; i++)
                {
                    velocity += _velocityFrames[i];
                    angularVelocity += _angularVelocityFrames[i];
                }
                
                velocity /= frames;
                angularVelocity /= frames;
                
                _rootRigidbody.SetLinearVelocity(velocity);
                _rootRigidbody.angularVelocity = angularVelocity;
                
            }
            finally
            {
                _velocityFrames = null;
                _angularVelocityFrames = null;
                _velocityFrame = 0;
            }
        }

        private void TrackVelocity()
        {
            if (_isNonVrInteractor || _isPhysicsAttachment)
                return;
            if (!_rootRigidbody || _rootRigidbody.isKinematic)
                return;
            
            var velocity = (_rootRigidbody.worldCenterOfMass - _lastPosition) / Time.fixedDeltaTime;
            _lastPosition = _rootRigidbody.worldCenterOfMass;
            
            var deltaRotation = _targetRotation * Quaternion.Inverse(_lastRotation);
            var angularVelocity = new Vector3(
                Mathf.DeltaAngle(0, Mathf.Round(deltaRotation.eulerAngles.x)),
                Mathf.DeltaAngle(0, Mathf.Round(deltaRotation.eulerAngles.y)),
                Mathf.DeltaAngle(0, Mathf.Round(deltaRotation.eulerAngles.z))) / Time.fixedDeltaTime * Mathf.Deg2Rad;
            _lastRotation = _targetRotation;
            
            _velocityFrames ??= new Vector3[MaxVelocityFrames];
            _velocityFrames[_velocityFrame % MaxVelocityFrames] = velocity;
            _angularVelocityFrames ??= new Vector3[MaxVelocityFrames];
            _angularVelocityFrames[_velocityFrame % MaxVelocityFrames] = angularVelocity;
            _velocityFrame++;
        }

        private bool HandleSocketInteractions(SelectEnterEventArgs args)
        {
            var socket = args.interactorObject as XRSocketInteractor;
            if (socket != null)
            {
                foreach (var interactor in _interactors)
                    interactionManager.SelectExit(interactor, this);
                _transform.SetParent(socket.attachTransform);
                return true;
            }

            var socketInteractors = _interactors.Where(x => x is XRSocketInteractor).ToArray();
            if (socketInteractors.Length != 0)
            {
                foreach (var socketInteractor in socketInteractors)
                    interactionManager.SelectExit(socketInteractor, this);
            }
            return false;
        }

        private void UpdateGrab(bool isFixedUpdate)
        {
            if (!isFixedUpdate && !IsClimbable)
                MoveWithPlayer();

            if (isFixedUpdate)
            {
                UpdateInterpolationTimer();
                UpdateAvatarAnimation();
            }

            if (!_isPhysicsAttachment)
            {
                if (isFixedUpdate)
                {
                    if (_rootRigidbody)
                    {
                        if (IsInterpolating())
                            _rootRigidbody.excludeLayers = ~0;
                        else if (_rootRigidbody.excludeLayers != _originalRigidbodyExcludeLayers)
                            _rootRigidbody.excludeLayers = _originalRigidbodyExcludeLayers;
                    }
                }
                UpdateRotation(isFixedUpdate);
                UpdatePosition(isFixedUpdate);
            }

            _initialGrab = false;
        }

        private void UpdateInterpolationTimer()
        {
            _currentInterpToTargetTime += Time.fixedDeltaTime;
        }

        private void ArtificialBreaking()
        {
            if (!usePhysicsTracking)
                return;

            if (Time.time <= _attachmentBreakCheckCooldown || !interactionManager) 
                return;
            
            foreach (var i in _interactors)
            {
                if (!_physicsAttachPoints.TryGetValue(i, out var attachPoint) || !attachPoint)
                    continue;
                if (Vector3.Distance(attachPoint.position, i.transform.position) <= breakDistance)
                    continue;
                interactionManager.SelectExit(i, this);
                break;
            }
        }

        private void OnInteractorsChanged()
        {
            if (_interactors.Count == 0)
            {
                OnDeselectedCompletely();
                return;
            }

            CalculateRotationOffset();
            CalculateInitialInteractionPoint();
        }

        private void DetectIfChildInteractable()
        {
            _isChildInteractable = 
                GetComponentsInParent<MetaverseInteractable>()
                    .Any(x => x != this && x.isSelected);
        }

        private void CalculateInitialInteractionPoint()
        {
            _interactor0RelativeAttachPosition = Quaternion.Inverse(_transform.rotation) * (_transform.position - CalculateInteractorAttachPositionInWorldSpace(0));
        }

        private Vector3 CalculateInteractorAttachPositionInWorldSpace(int interactorIndex, bool useNearestColliderPos = true)
        {
            var xrSelectInteractor = _interactors[interactorIndex];
            if (!useNearestColliderPos)
                return xrSelectInteractor.GetAttachTransform(this).position;
            
            var interactorAttachTransform = xrSelectInteractor.GetAttachTransform(this);
            if (_interactorAttachPositionOffsets.TryGetValue(xrSelectInteractor, out var offset))
                return _transform.position + _transform.rotation * offset;
            
            Vector3 worldPos;
            if (colliders.Count == 0)
                worldPos = interactorAttachTransform.position;
            else
            {
                // First iteration we find the closest point
                var closestPoint = colliders.Select(x => new
                {
                    Collider = x,
                    ClosestPoint = x.ClosestPoint(interactorAttachTransform.position)
                }).OrderBy(x => Vector3.Distance(x.ClosestPoint, interactorAttachTransform.position)).First();
                
                worldPos = closestPoint.ClosestPoint;
            }
            
            _interactorAttachPositionOffsets[xrSelectInteractor] = Quaternion.Inverse(_transform.rotation) * (worldPos - _transform.position);
            return worldPos;
        }

        private void CalculateRotationOffset()
        {
            if (_interactors.Count == 1)
            {
                if (_hand1AttachPoint != null && _hand1AttachPoint.transform)
                    _initialRotationOffset = Quaternion.Inverse(_hand1AttachPoint.transform.rotation) * _transform.rotation;
                else
                {
                    if (!_isNonVrInteractor) _initialRotationOffset = Quaternion.Inverse(_interactors[0].GetAttachTransform(this).rotation) * transform.rotation;
                    else _initialRotationOffset = Quaternion.identity;
                }

                return;
            }

            if (enableRotation) InitializeDelta();
        }

        private void InitializeDelta()
        {
            var interactorA = CalculateInteractorAttachPositionInWorldSpace(0, false);
            var interactorB = CalculateInteractorAttachPositionInWorldSpace(1, false);
            _initialDelta = interactorB - interactorA;
            if (enableRotation) InitializeRotation();
        }

        private void InitializeRotation()
        {
            if (_hand2AttachPoint == null || !_hand2AttachPoint.transform)
            {
                _initialRotationOffset = Quaternion.LookRotation(_initialDelta.normalized);
                _initialRotation = _transform.rotation;
                return;
            }

            var hasAttachPoint1 = _hand1AttachPoint != null && _hand1AttachPoint.transform;
            var pos1 = hasAttachPoint1 ? _hand1AttachPoint.transform.position : CalculateInteractorAttachPositionInWorldSpace(0, false);
            var pos2 = _hand2AttachPoint.transform.position;
            var delta = (pos2 - pos1).normalized;
            var lookRot = Quaternion.LookRotation(delta.normalized, _interactors[0].transform.up);
            var rotation = _transform.rotation;
            _initialRotationOffset = Quaternion.Inverse(lookRot) * rotation;
            _initialRotation = rotation;
        }

        private void MoveWithPlayer()
        {
            if (_isChildInteractable || !_player)
                return;

            var rotationDelta = Quaternion.Inverse(_lastPlayerRot) * _player.rotation;
            if (rotationDelta != Quaternion.identity)
            {
                var lastPositionOffset = Quaternion.Inverse(_lastPlayerRot) * (_transform.position - _lastPlayerPos);
                var lastRotationOffset = Quaternion.Inverse(_lastPlayerRot) * _transform.rotation;
                if (!_rootRigidbody || !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotation) || _rootRigidbody.isKinematic)
                {
                    var targetRot = _player.rotation * lastRotationOffset;
                    if (_rootRigidbody && !_rootRigidbody.isKinematic)
                    {
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotationX))
                            targetRot.x = _transform.rotation.x;
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotationY))
                            targetRot.y = _transform.rotation.y;
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotationZ))
                            targetRot.z = _transform.rotation.z;
                        targetRot.Normalize();
                    }

                    _transform.rotation = targetRot;
                }

                if (!_rootRigidbody || !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePosition) || _rootRigidbody.isKinematic)
                {
                    var targetPos = _lastPlayerPos + (_player.rotation * lastPositionOffset);
                    if (_rootRigidbody && !_rootRigidbody.isKinematic)
                    {
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePositionX))
                            targetPos.x = _transform.position.x;
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePositionY))
                            targetPos.y = _transform.position.y;
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePositionZ))
                            targetPos.z = _transform.position.z;
                    }

                    _transform.position = targetPos;
                }

                if (_interactors.Count > 1)
                    InitializeDelta();
            }

            if (!_rootRigidbody || !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePosition) || _rootRigidbody.isKinematic)
            {
                var positionDelta = _player.position - _lastPlayerPos;
                if (positionDelta.sqrMagnitude > 0)
                {
                    var targetPos = _transform.position + positionDelta;
                    if (_rootRigidbody && !_rootRigidbody.isKinematic)
                    {
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePositionX))
                            targetPos.x = _transform.position.x;
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePositionY))
                            targetPos.y = _transform.position.y;
                        if (_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePositionZ))
                            targetPos.z = _transform.position.z;
                    }

                    _transform.position = targetPos;
                }
            }

            _lastPlayerPos = _player.position;
            _lastPlayerRot = _player.rotation;
        }

        private void UpdatePosition(bool isFixedUpdate)
        {
            _targetPosition = GetInteractPosition(_hand1AttachPoint?.transform, _interactors[0].GetAttachTransform(this), _isNonVrInteractor);
            if (isFixedUpdate || !IsInterpolating())
            {
                if (!IsInterpolating())
                    _interpPosition = _targetPosition;
                else
                {
                    var t = _currentInterpToTargetTime / InterpToTargetPositionDuration;
                    _interpPosition = Vector3.Lerp(_interpPosition, _targetPosition, t);
                }
            }

            switch (usePhysicsTracking)
            {
                case true when isFixedUpdate && _rootRigidbody && !_rootRigidbody.isKinematic && !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePosition) && (!_initialGrab || _isNonVrInteractor):
                    VelocityTrackPosition(_interpPosition);
                    break;
                case true when isFixedUpdate && _rootRigidbody && !_rootRigidbody.isKinematic && !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezePosition):
                    _rootRigidbody.position = _interpPosition;
                    break;
                default:
                {
                    if (isFixedUpdate && _rootRigidbody && !_rootRigidbody.isKinematic)
                        VelocityTrackPosition(_interpPosition);
                    _transform.position = _interpPosition;
                    break;
                }
            }
        }

        private bool IsInterpolating()
        {
            return _currentInterpToTargetTime <= InterpToTargetPositionDuration;
        }

        private void UpdateRotation(bool isFixedUpdate)
        {
            if (_interactors.Count == 0)
                return;

            if (!enableRotation)
                return;

            if (_interactors.Count == 1)
            {
                _targetRotation = _interactors[0].GetAttachTransform(this).rotation * _initialRotationOffset;
                
                InterpolateRotation(isFixedUpdate);
                
                switch (usePhysicsTracking)
                {
                    case true when isFixedUpdate && _rootRigidbody && !_rootRigidbody.isKinematic && !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotation) && (!_initialGrab || _isNonVrInteractor):
                        VelocityTrackRotation(_interpRotation);
                        break;
                    default:
                    {
                        if (isFixedUpdate &&_rootRigidbody && !_rootRigidbody.isKinematic)
                            VelocityTrackRotation(_interpRotation);
                        _transform.rotation = _interpRotation;
                        break;
                    }
                }
                
                return;
            }

            var posA = CalculateInteractorAttachPositionInWorldSpace(0, false);
            var posB = CalculateInteractorAttachPositionInWorldSpace(1, false);
            var currentDelta = posB - posA;

            if (_hand2AttachPoint == null || !_hand2AttachPoint.transform)
            {
                var rotationDelta = Quaternion.FromToRotation(_initialDelta.normalized, currentDelta.normalized);
                _targetRotation = rotationDelta * _initialRotation;
            }
            else
                _targetRotation = Quaternion.LookRotation(currentDelta.normalized, _interactors[0].transform.up) * _initialRotationOffset;

            InterpolateRotation(isFixedUpdate);
            
            switch (usePhysicsTracking)
            {
                case true when isFixedUpdate &&_rootRigidbody && !_rootRigidbody.isKinematic && !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotation) && (!_initialGrab || _isNonVrInteractor):
                    VelocityTrackRotation(_interpRotation);
                    break;
                case true when isFixedUpdate &&_rootRigidbody && !_rootRigidbody.isKinematic && !_rootRigidbody.constraints.HasFlag(RigidbodyConstraints.FreezeRotation):
                    _rootRigidbody.rotation = _interpRotation;
                    break;
                default:
                {
                    if (isFixedUpdate &&_rootRigidbody && !_rootRigidbody.isKinematic)
                        VelocityTrackRotation(_interpRotation);
                    _transform.rotation = _interpRotation;
                    break;
                }
            }
        }
        
        private void InterpolateRotation(bool isFixedUpdate)
        {
            if (isFixedUpdate || !IsInterpolating())
            {
                if (!IsInterpolating())
                    _interpRotation = _targetRotation;
                else
                {
                    var t = _currentInterpToTargetTime / InterpToTargetPositionDuration;
                    _interpRotation = Quaternion.Slerp(_interpRotation, _targetRotation, t);
                }
            }
        }

        private void VelocityTrackPosition(Vector3 targetPos)
        {
            // Do velocity tracking
            if (!_rootRigidbody || _rootRigidbody.isKinematic)
                return;

            // Scale initialized velocity by prediction factor
            _rootRigidbody.SetLinearVelocity(_rootRigidbody.GetLinearVelocity() * (1f - VelocityDamping));
            var positionDelta = targetPos - _transform.position;
            var velocity = positionDelta / Time.deltaTime;

            if (!float.IsNaN(velocity.x))
                _rootRigidbody.SetLinearVelocity(_rootRigidbody.GetLinearVelocity() + (velocity * VelocityScale));
        }

        private void VelocityTrackRotation(Quaternion targetRot)
        {
            if (!_rootRigidbody)
                return;

            _rootRigidbody.angularVelocity *= 0;

            var rotationDelta = targetRot * Quaternion.Inverse(_transform.rotation);
            rotationDelta.ToAngleAxis(out var angleInDegrees, out var rotationAxis);
            if (angleInDegrees > 180f)
                angleInDegrees -= 360f;

            if (Mathf.Abs(angleInDegrees) > Mathf.Epsilon)
            {
                var angularVelocity = (rotationAxis * (angleInDegrees * Mathf.Deg2Rad)) / Time.deltaTime;
                _rootRigidbody.angularVelocity += (angularVelocity * 1);
            }
        }

        private void SetPassthroughCollision(GameObject player, bool ignoreCollision)
        {
            if (!player) return;
            var cols = player.GetComponentsInChildren<Collider>(true)
                .Where(x => !x.TryGetComponent<MetaverseXRController>(out _) && !x.GetComponentInParent<MetaverseXRController>())
                .ToArray();
            foreach (var c1 in colliders)
            foreach (var c2 in cols)
            {
                var setIgnore = ignoreCollision;
                if (ignoreCollision && enablePlayerCollision)
                {
                    if (c2.attachedRigidbody && c2.attachedRigidbody.GetComponent<Joint>())
                    {
                        if (!_attachedPhysicsBonesMap.ContainsValue(c2.attachedRigidbody))
                            setIgnore = false;
                    }
                }

                Physics.IgnoreCollision(c1, c2, setIgnore);
            }
        }

        #endregion
    }
}
#elif UNITY_EDITOR
using System;
using JetBrains.Annotations;
using MetaverseCloudEngine.Unity.UI;
using TriInspectorMVCE;
using UnityEditor;
using UnityEditor.PackageManager;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    [HideMonoScript]
    public class MetaverseInteractable : TriInspectorMonoBehaviour
    {
        private void Awake()
        {
            EditorCompatibilityError.For(gameObject, "com.unity.xr.interaction.toolkit", destroyCancellationToken);
        }

        [UsedImplicitly]
        [InfoBox("This component requires XR Interaction Toolkit to be installed.", TriMessageType.Error)]
        [Button("Install XR Interaction Toolkit")]
        public void InstallXRInteractionToolkit()
        {
            if (!EditorUtility.DisplayDialog("Install XR Interaction Toolkit", "This will install the XR Interaction Toolkit package. Do you want to continue?", "Yes", "No"))
                return;
            var req = Client.Add("com.unity.xr.interaction.toolkit");
            while (!req.IsCompleted)
            {
                EditorUtility.DisplayProgressBar("Installing XR Interaction Toolkit", "Please wait...", 0);
                Thread.Sleep(100);
            }
        }
    }
}
#endif