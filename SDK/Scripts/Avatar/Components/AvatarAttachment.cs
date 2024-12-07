using System;
using System.Linq;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.XR;
using MetaverseCloudEngine.Unity.XR.Components;
using TriInspectorMVCE;
#if MV_XRCOREUTILS
using Unity.XR.CoreUtils;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.Avatar.Components
{
    [HideMonoScript]
    [DefaultExecutionOrder(int.MaxValue)]
    public class AvatarAttachment : TriInspectorMonoBehaviour
    {
        [SerializeField] private bool autoAttach = true;
        [SerializeField] private bool autoFindAvatarContainer = true;
        [HideIf(nameof(autoFindAvatarContainer))]
        [SerializeField] private PlayerAvatarContainer avatarContainer;
        [SerializeField] private HumanBodyBones bone;
        [SerializeField] private bool followPosition = true;
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private bool followRotation = true;
        [SerializeField] private Vector3 rotationOffset;

        [Header("Debug")]
        [Tooltip("If you're in the SDK you can use this flag to force the bone transform to track the left/right hands when XR is enabled.")]
        [SerializeField] private bool trackXRControllersForOffline;

        [Header("Events")]
        [SerializeField] private UnityEvent<Transform> onAttached;
        [SerializeField] private UnityEvent onDetached;

        private Transform _boneT;
        private bool _attached;
        private Transform _transform;
        private Quaternion _avatarRotationOffset = Quaternion.identity;

        public bool FollowPosition { get => followPosition; set => followPosition = value; }
        public bool FollowRotation { get => followRotation; set => followRotation = value; }
        public PlayerAvatarContainer AvatarContainer
        {
            get => avatarContainer;
            set => avatarContainer = value;
        }

        private void Awake() => _transform = transform;

        private void Start()
        {
            AutoAttach();

            if (trackXRControllersForOffline && !MetaverseProgram.IsCoreApp)
                XRInputTrackingAPI.HmdConnected += OnHmdConnected;
        }

        private void OnDestroy() => XRInputTrackingAPI.HmdConnected -= OnHmdConnected;

        private void OnEnable() => AutoAttach();

        private void OnTransformParentChanged() => AutoAttach();

        private void OnDisable() => Detach();

        private void LateUpdate() => UpdateTransform();

        private void OnHmdConnected(InputDevice obj) => MetaverseDispatcher.AtEndOfFrame(() => AutoAttach());

        private void UpdateTransform()
        {
            if ((followPosition || followRotation) && _attached)
            {
                try
                {
                    var rotation = _boneT.rotation * Quaternion.Euler(rotationOffset) * _avatarRotationOffset;
                    if (followRotation) _transform.rotation = rotation;
                    if (followPosition) _transform.position = _boneT.position + rotation * positionOffset;
                }
                catch (MissingReferenceException)
                {
                    Detach();
                }
                catch (NullReferenceException)
                {
                    Detach();
                }
            }
        }

        public void Attach()
        {
            if (_attached)
                return;

            if (autoFindAvatarContainer)
            {
                if (!avatarContainer)
                    avatarContainer = gameObject.GetComponentInParent<PlayerAvatarContainer>(true);
                if (!avatarContainer)
                    avatarContainer = gameObject.GetComponentInChildren<PlayerAvatarContainer>(true);
                if (!avatarContainer)
                    avatarContainer = gameObject.GetNearestComponent<PlayerAvatarContainer>();
            }

            if (!avatarContainer)
                return;

            RegisterEvents();

            if (avatarContainer.Avatar)
            {
                AttachToAvatar(avatarContainer.Avatar);
            }
        }

        public void Detach()
        {
            Detach(true);
        }

        public void Detach(bool detachFromContainer)
        {
            if (!_attached)
                return;

            onDetached?.Invoke();

            _boneT = null;
            _attached = false;

            if (detachFromContainer)
            {
                UnRegisterEvents();
                if (autoFindAvatarContainer)
                    avatarContainer = null;
            }
        }

        private void AutoAttach()
        {
            if (autoAttach)
                Attach();
        }

        private void AttachToAvatar(Animator avatar)
        {
            if (!avatar || !avatar.avatar)
            {
                MetaverseProgram.Logger.LogError("Cannot attach, no avatar.");
                return;
            }

            if (avatar.TryGetComponent(out AvatarMetaData metaData) &&
                bone is HumanBodyBones.LeftHand or HumanBodyBones.RightHand or HumanBodyBones.Head)
            {
                _avatarRotationOffset = bone switch
                {
                    HumanBodyBones.LeftHand => metaData.GetLeftHandRotationOffset(),
                    HumanBodyBones.RightHand => metaData.GetRightHandRotationOffset(),
                    HumanBodyBones.Head => metaData.GetHeadRotationOffset()
                };
            }
            else
            {
                _avatarRotationOffset = Quaternion.identity;
            }

            if (!MetaverseProgram.IsCoreApp && 
                XRSettings.isDeviceActive && 
                trackXRControllersForOffline &&
                bone is HumanBodyBones.LeftHand or HumanBodyBones.RightHand)
            {
#if MV_XRCOREUTILS && MV_XR_TOOLKIT
                var xrOrigin = avatar.GetNearestComponent<XROrigin>();
                if (xrOrigin)
                {
                    switch (bone)
                    {
                        case HumanBodyBones.LeftHand:
                            var leftController = xrOrigin.GetComponentsInChildren<MetaverseXRController>()
                                .FirstOrDefault(x => x.Hand == MetaverseXRController.HandType.Left);
                            if (leftController)
                                _boneT = leftController.transform;
                            break;
                        case HumanBodyBones.RightHand:
                            var rightController = xrOrigin.GetComponentsInChildren<MetaverseXRController>()
                                .FirstOrDefault(x => x.Hand == MetaverseXRController.HandType.Right);
                            if (rightController)
                                _boneT = rightController.transform;
                            break;
                    }
                }
#endif
            }
            else
            {
                _boneT = avatar.GetBoneTransform(bone);
                if (!_boneT) return;
            }

            onAttached?.Invoke(_boneT);
            _attached = true;
        }

        private void DetachFromAvatar() => Detach(false);

        private void RegisterEvents()
        {
            if (!avatarContainer) return;
            avatarContainer.Events.onAvatarSpawned.AddListener(AttachToAvatar);
            avatarContainer.Events.onAvatarDeSpawned.AddListener(DetachFromAvatar);
        }

        private void UnRegisterEvents()
        {
            if (!avatarContainer) return;
            avatarContainer.Events?.onAvatarSpawned?.RemoveListener(AttachToAvatar);
            avatarContainer.Events?.onAvatarDeSpawned?.RemoveListener(DetachFromAvatar);
        }
    }
}