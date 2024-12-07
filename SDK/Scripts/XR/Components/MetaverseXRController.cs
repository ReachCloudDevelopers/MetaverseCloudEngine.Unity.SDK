#if MV_XR_TOOLKIT
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

using MetaverseCloudEngine.Unity.Physix;
using System.Linq;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine.Events;
using UnityEngine.InputSystem;
#if MV_XR_TOOLKIT_3
using IXRSelectInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor;
#else
using IXRSelectInteractor = UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractor;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// An advanced XR controller that's compatible with the <see cref="MetaverseInteractable"/> component. This is recommended for
    /// use over the <see cref="XRController"/> component.
    /// </summary>
    [DefaultExecutionOrder(-int.MaxValue)]
    public class MetaverseXRController : ActionBasedController
    {
        #region Enums

        /// <summary>
        /// The hand that this <see cref="MetaverseXRController"/> represents.
        /// </summary>
        public enum HandType
        {
            /// <summary>
            /// This is the left hand.
            /// </summary>
            Left,
            /// <summary>
            /// This is the right hand.
            /// </summary>
            Right,
        }

        #endregion

        #region Inspector

        [Header("Player")]
        [Tooltip("The root object. This should be the main player object.")]
        [SerializeField] private Transform root;

        [Header("Hands")]
        [Tooltip("The hand type for this XR controller.")]
        [SerializeField] private HandType hand;
        [Tooltip("Whether this is a Non-VR XR controller (i.e. simulated).")]
        [SerializeField] private bool isNonVR;
        [Tooltip("Whenever the Non-VR selection desires to be in aim mode.")]
        [SerializeField] private UnityEvent nonVRAimMode;
        [Tooltip("Whenever the Non-VR selection desires to be in free mode.")]
        [SerializeField] private UnityEvent nonVRFreeMode;

        [Header("IK")]
        [Tooltip("A transform that is used for inverse kinematics when interacting with an object.")]
        [SerializeField] private Transform ikTransform;

        [Header("Physics")]
        [Tooltip("The physics hand bone of the VRPhysicsRig (if there is one).")]
        [SerializeField] private VRPhysicsRig physicsRig;

        #endregion

        #region Fields

        private Transform _transform;
        private bool _isFollowingPhysicsHand;
        private bool _hasIK;
        private bool _disabled = true;

        #endregion

        #region Properties

        /// <summary>
        /// The hand type for this XR controller.
        /// </summary>
        public HandType Hand => hand;

        /// <summary>
        /// Whether this is a Non-VR XR controller (i.e. simulated).
        /// </summary>
        public bool IsNonVR => isNonVR;

        /// <summary>
        /// The XR node based on the <see cref="HandType"/>.
        /// </summary>
        public XRNode XRNode => hand == HandType.Left ? XRNode.LeftHand : XRNode.RightHand;

        /// <summary>
        /// If a <see cref="VRPhysicsRig"/> exists, this is a reference to that.
        /// </summary>
        public VRPhysicsRig PhysicsRig => physicsRig;

        /// <summary>
        /// The physics hand bone of the <see cref="VRPhysicsRig"/> (if there is one).
        /// </summary>
        public Transform PhysicsHand => physicsRig ? physicsRig.GetPhysicsBone(hand == HandType.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) : null;

        /// <summary>
        /// A transform that is used for inverse kinematics when interacting with an object.
        /// </summary>
        public Transform IKTransform => ikTransform;

        /// <summary>
        /// The offset of the IK transform.
        /// </summary>
        public Vector3 IKOffset { get; private set; }

        /// <summary>
        /// The root object. This should be the main player object.
        /// </summary>
        public Transform Root => root;

        #endregion

        #region Unity Events

        protected override void Awake()
        {
            _transform = transform;
            _hasIK = ikTransform;
            if (_hasIK)
                IKOffset = _transform.InverseTransformPoint(ikTransform.position);
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _disabled = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _disabled = true;
        }

        private void Start()
        {
            RegisterPhysicsRigEvents();
        }

        private void OnDestroy()
        {
            UnregisterPhysicsRigEvents();
        }

#if UNITY_2022_2_OR_NEWER
        protected override void FixedUpdate()
        {
            if (_disabled)
                return;
            base.FixedUpdate();
            FollowPhysicsHand();
        }
#endif

        #endregion

        #region Protected Methods

        protected override void UpdateController()
        {
            if (_disabled)
            {
                MetaverseProgram.Logger.LogError("XR Controller is disabled. Cannot update.");
                return;
            }
            
            base.UpdateController();
            FollowPhysicsHand();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Forces a deselection on all selected objects in the attached <see cref="IXRSelectInteractor"/>.
        /// </summary>
        public void ForceDeselectAll()
        {
            if (!TryGetComponent(out IXRSelectInteractor interactor))
                return;

            var xrInteractionManagers = 
#if UNITY_2023_1_OR_NEWER
                FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None);
#else
                FindObjectsOfType<XRInteractionManager>();
#endif
            foreach (var manager in xrInteractionManagers.ToArray())
                foreach (var selection in interactor.interactablesSelected.ToArray())
                    manager.SelectExit(interactor, selection);
        }

        /// <summary>
        /// Sets the xr controller into aim mode or free mode. This is
        /// generally only for non-VR.
        /// </summary>
        /// <param name="aimMode">The aim mode.</param>
        public void SetAimMode(bool aimMode)
        {
            if (!isNonVR)
                return;

            if (aimMode) nonVRAimMode?.Invoke();
            else nonVRFreeMode?.Invoke();
        }

        #endregion

        #region Private Methods

        private void FollowPhysicsHand()
        {
            if (!_isFollowingPhysicsHand)
                return;

            var physicsHand = PhysicsHand;
            if (!physicsHand)
            {
                _isFollowingPhysicsHand = false;
                ResetLocalTransform();
            }
            else
            {
                if (_hasIK) _transform.position = physicsHand.position + (_transform.rotation * -IKOffset);
                else _transform.position = physicsHand.position;
            }
        }

        private void RegisterPhysicsRigEvents()
        {
            if (!physicsRig)
                return;

            physicsRig.onGenerated.AddListener(OnPhysicsRigGenerated);
            physicsRig.onDestroyed.AddListener(OnPhysicsRigDestroyed);

            if (physicsRig.IsActive)
                OnPhysicsRigGenerated();
        }

        private void UnregisterPhysicsRigEvents()
        {
            if (!physicsRig)
                return;

            physicsRig.onGenerated.RemoveListener(OnPhysicsRigGenerated);
            physicsRig.onDestroyed.RemoveListener(OnPhysicsRigDestroyed);
        }

        private void OnPhysicsRigGenerated()
        {
            _isFollowingPhysicsHand = true;
        }

        private void OnPhysicsRigDestroyed()
        {
            _isFollowingPhysicsHand = false;
            ResetLocalTransform();
        }

        private void ResetLocalTransform()
        {
            _transform.localPosition = Vector3.zero;
            _transform.localRotation = Quaternion.identity;
        }

        #endregion

    }
}
#endif