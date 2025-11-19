#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0035

#if MV_OCULUS_PLUGIN && METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED && UNITY_ANDROID
#define MV_USING_OCULUS_SDK
#endif
#if MV_XR_HANDS
using TriInspectorMVCE;
#pragma warning restore IDE0079 // Remove unnecessary suppression
using MetaverseCloudEngine.Unity.Async;
#if MV_USING_OCULUS_SDK
using Unity.XR.Oculus.Input;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
#if MV_OPENXR
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.Hands.OpenXR;
#endif
using XRController = UnityEngine.InputSystem.XR.XRController;

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// Manages the state of the XR hands and controllers.
    /// </summary>
    [HideMonoScript]
    [AddComponentMenu(MetaverseConstants.ProductName + "/XR/XR Hand Manager")]
    public class XRHandManager : TriInspectorMonoBehaviour
    {
        [Header("Update")]
        [Tooltip("If true, will update the hands and controllers on the before render event.")]
        [SerializeField] private bool updateOnBeforeRender = true;
        [Tooltip("If true, will modify the transforms of the hands and controllers.")]
        [SerializeField] private bool updateTransforms = true;
        [Tooltip("If true, will change the active state of the hands and controllers.")]
        [SerializeField] private bool changeActiveState = true;

        [Header("Origin")] 
        [SerializeField] private Transform origin;
        
        [Header("Hands")]
        [Tooltip("If false, will use the device position and rotation instead of the pointer position and rotation.")]
        [SerializeField] private bool lHandUsePointerPose = true;
        [SerializeField] private GameObject leftHand;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 leftHandPositionOffset;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 leftHandRotationOffset;
        [SerializeField] private bool rHandUsePointerPose = true;
        [SerializeField] private GameObject rightHand;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 rightHandPositionOffset;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 rightHandRotationOffset;
        
        [Header("Controllers")]
        [Tooltip("If false, will use the device position and rotation instead of the pointer position and rotation.")]
        [SerializeField] private bool lControllerUsePointerPose = true;
        [SerializeField] private GameObject leftController;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 leftControllerPositionOffset;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 leftControllerRotationOffset;
        [Tooltip("If false, will use the device position and rotation instead of the pointer position and rotation.")]
        [SerializeField] private bool rControllerUsePointerPose = true;
        [SerializeField] private GameObject rightController;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 rightControllerPositionOffset;
        [ShowIf(nameof(updateTransforms))]
        [SerializeField] private Vector3 rightControllerRotationOffset;
        
        private bool _lControllerIsHand;
        private bool _rControllerIsHand;

#if !UNITY_WEBGL && !UNITY_IOS && !UNITY_OSX
        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (!origin)
                origin = transform;
        }

        private void Reset()
        {
            origin = transform;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnEnable()
        {
            _lControllerIsHand = leftHand == leftController;
            _rControllerIsHand = rightHand == rightController;

            DeactivateCompletely();
        }

        private void LateUpdate()
        {
            UpdateControllers();
        }

        private void UpdateControllers()
        {
            var lUpdated = UpdateDeviceState(XRController.leftHand, true, lControllerUsePointerPose, leftController);
            var rUpdated = UpdateDeviceState(XRController.rightHand, false, rControllerUsePointerPose, rightController);
            if (lUpdated || rUpdated)
            {
                if (changeActiveState)
                {
                    if (!_lControllerIsHand) SetActiveSafe(leftController, lUpdated);
                    if (!_rControllerIsHand) SetActiveSafe(rightController, rUpdated);
                    if (!_lControllerIsHand) SetActiveSafe(leftHand, false);
                    if (!_rControllerIsHand) SetActiveSafe(rightHand, false);
                }
            }
            else
            {
                const bool usingOpenXR =
#if MV_USING_OCULUS_SDK
                    false;
#else
                    true;
#endif

                if (usingOpenXR)
                {
#pragma warning disable CS0162 // Unreachable code detected
                    lUpdated |= lHandUsePointerPose && UpdateDeviceState_UnityBugWorkaround(MetaAimHand.left, true, lHandUsePointerPose, leftHand);
#pragma warning restore CS0162 // Unreachable code detected
                    rUpdated |= rHandUsePointerPose && UpdateDeviceState_UnityBugWorkaround(MetaAimHand.right, false, rHandUsePointerPose, rightHand);

#if MV_OPENXR
                    if (HandTracking.subsystem != null)
                    {
                        if (!lUpdated)
                            lUpdated |= !lHandUsePointerPose && UpdateFallbackHandState(
                                HandTracking.subsystem.leftHand, leftHand);
                        if (!rUpdated)
                            rUpdated |= !rHandUsePointerPose && UpdateFallbackHandState(
                                HandTracking.subsystem.rightHand, rightHand);
                    }
#endif
                }
                else
                {
                    lUpdated |= UpdateOvrHandState(true, lHandUsePointerPose, leftHand);
                    rUpdated |= UpdateOvrHandState(false, rHandUsePointerPose, rightHand);
                }

                if (lUpdated || rUpdated)
                {
                    if (changeActiveState)
                    {
                        if (!_lControllerIsHand) SetActiveSafe(leftHand, lUpdated);
                        if (!_rControllerIsHand) SetActiveSafe(rightHand, rUpdated);
                    }
                }
                else
                {
                    if (changeActiveState)
                    {
                        if (!_lControllerIsHand) SetActiveSafe(leftHand, false);
                        if (!_rControllerIsHand) SetActiveSafe(rightHand, false);
                    }
                }
                
                if (!_lControllerIsHand) SetActiveSafe(leftController, false);
                if (!_rControllerIsHand) SetActiveSafe(rightController, false);
            }

            if (changeActiveState)
            {
                if (_lControllerIsHand) SetActiveSafe(leftController, lUpdated);
                if (_rControllerIsHand) SetActiveSafe(rightController, rUpdated);
            }
        }

        private void DeactivateCompletely()
        {
            if (!changeActiveState) return;
            if (leftHand) leftHand.SetActive(false);
            if (rightHand) rightHand.SetActive(false);
            if (leftController) leftController.SetActive(false);
            if (rightController) rightController.SetActive(false);
        }

        private static void SetActiveSafe(GameObject gameObject, bool active)
        {
            if (gameObject)
                gameObject.SetActive(active);
        }

        private bool UpdateOvrHandState(bool isLeftHand, bool usePointerPose, GameObject deviceGameObject)
        {
#if MV_USING_OCULUS_SDK
            OVRPlugin.HandState handState = default;
            if (!OVRPlugin.GetHandState(
                    OVRPlugin.Step.Render,
                    isLeftHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight,
                    ref handState))
                return false;
            
            if (!handState.Status.HasFlag(OVRPlugin.HandStatus.HandTracked)) 
                return false;

            if (!updateTransforms || !deviceGameObject) 
                return true;
            
            var pose = usePointerPose ? handState.PointerPose : handState.RootPose;
            var rawPos = pose.Position.FromFlippedZVector3f();
            var rawRot = pose.Orientation.FromFlippedZQuatf();

            if (!usePointerPose)
            {
                var openXROffset = isLeftHand
                    ? Quaternion.Euler(0, 90, -180)
                    : Quaternion.Euler(0f, -90f, 0f);
                rawRot *= openXROffset;
            }
            
            deviceGameObject.transform.position = origin.TransformPoint(rawPos + GetHandPositionOffset(rawRot, isLeftHand));
            deviceGameObject.transform.rotation = origin.rotation * rawRot * GetHandRotationOffset(isLeftHand);
#endif
            return true;

        }

        private bool UpdateFallbackHandState(
            XRHand hand,
            GameObject deviceGameObject)
        {
            if (!hand.isTracked || hand.handedness == Handedness.Invalid)
                return false;
            if (hand.rootPose.position.sqrMagnitude <= 0)
                return false;
            if (!updateTransforms || !deviceGameObject)
                return true;

            var pose = hand.rootPose;
            deviceGameObject.transform.position =
                origin.TransformPoint(pose.position + GetHandPositionOffset(hand.rootPose.rotation, hand.handedness == Handedness.Left));
            deviceGameObject.transform.rotation =
                origin.rotation * (pose.rotation * GetHandRotationOffset(hand.handedness == Handedness.Left));
            
            return true;
        }

        private bool UpdateDeviceState_UnityBugWorkaround(
            TrackedDevice device,
            bool lHand,
            bool usePointerPose,
            GameObject deviceGameObject)
        {
            if (!IsDeviceValid(device)) 
                return false;

            if (device is MetaAimHand metaAimHand)
            {
                if (!((MetaAimFlags)metaAimHand.aimFlags.value).HasFlag(MetaAimFlags.Valid))
                    return false;
            }
            
#if MV_OPENXR
            if (HandTracking.subsystem != null &&
                (lHand
                    ? !HandTracking.subsystem.leftHand.isTracked
                    : !HandTracking.subsystem.rightHand.isTracked))
                return false;
#endif

            if (deviceGameObject && updateTransforms)
            {
                var pose = GetControllerPose(device, lHand, usePointerPose);
                deviceGameObject.transform.SetPositionAndRotation(origin.TransformPoint(pose.position), origin.rotation * pose.rotation);
                return true;
            }

            return true;
        }

        private bool UpdateDeviceState(TrackedDevice device, bool lHand, bool usePointerPose, GameObject deviceGameObject = null)
        {
            if (!IsDeviceValid(device))
                return false;

            if (!device.isTracked.isPressed)
                return false;

            if (!((InputTrackingState)device.trackingState.value).HasFlag(
                    InputTrackingState.Position |
                    InputTrackingState.Rotation))
                return false;

            if (deviceGameObject && updateTransforms)
            {
                var pose = GetControllerPose(device, lHand, usePointerPose);
                deviceGameObject.transform.position = origin.TransformPoint(pose.position);
                deviceGameObject.transform.rotation = origin.rotation * pose.rotation;
                return true;
            }

            return true;
        }

        private static bool IsDeviceValid(TrackedDevice device)
        {
            return device != null && device.devicePosition.value.sqrMagnitude > 0 && device.enabled;
        }

        private Pose GetMetaQuestControllerPoseFallback(
            TrackedDevice controller, 
            bool isLHand, 
            bool usePointerPose, 
            UnityEngine.InputSystem.Controls.Vector3Control devicePositionControl, 
            UnityEngine.InputSystem.Controls.QuaternionControl deviceRotationControl, 
            UnityEngine.InputSystem.Controls.Vector3Control pointerPositionControl, 
            UnityEngine.InputSystem.Controls.QuaternionControl pointerRotationControl)
        {
#if MV_META_CORE
            OVRPlugin.HandState handState = default;
            if (OVRPlugin.GetHandState(OVRPlugin.Step.Render,
                    isLHand ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight, ref handState) && handState.Status == OVRPlugin.HandStatus.HandTracked)
                return new Pose(
                    devicePositionControl.value + handState.PointerPose.Position.FromFlippedZVector3f() + GetControllerPositionOffset(deviceRotationControl.value, Quaternion.identity, isLHand),
                    deviceRotationControl.value * handState.PointerPose.Orientation.FromFlippedZQuatf() * GetControllerRotationOffset(isLHand));
            var ovrPose = OVRManager.GetOpenVRControllerOffset(
                isLHand ? XRNode.LeftHand : XRNode.RightHand);
            return new Pose(
                devicePositionControl.value + ovrPose.position + GetControllerPositionOffset(deviceRotationControl.value, ovrPose.orientation, isLHand, new Vector3(0, 0, 0.045f)),
                deviceRotationControl.value * ovrPose.orientation * GetControllerRotationOffset(isLHand));
#else
            if (usePointerPose)
            {
                return new Pose(
                    pointerPositionControl.value + GetControllerPositionOffset(deviceRotationControl.value, Quaternion.identity, isLHand),
                    pointerRotationControl.value * GetControllerRotationOffset(isLHand));
            }
            else
            {
                return new Pose(
                    devicePositionControl.value + GetControllerPositionOffset(deviceRotationControl.value, Quaternion.identity, isLHand),
                    deviceRotationControl.value * GetControllerRotationOffset(isLHand));
            }
#endif
        }

        private Pose GetControllerPose(TrackedDevice controller, bool isLHand, bool usePointerPose)
        {
            if (usePointerPose)
            {
                switch (controller)
                {
                    case MetaAimHand aimHand:
                        return new Pose(aimHand.devicePosition.value + GetHandPositionOffset(aimHand.deviceRotation.value, isLHand), aimHand.deviceRotation.value * GetHandRotationOffset(isLHand));
#if MV_OPENXR
                    case MetaQuestTouchPlusControllerProfile.QuestTouchPlusController metaPlus:
                        
                        return GetMetaQuestControllerPoseFallback(
                            metaPlus, 
                            isLHand, 
                            usePointerPose, 
                            metaPlus.devicePosition, 
                            metaPlus.deviceRotation, 
                            metaPlus.pointerPosition, 
                            metaPlus.pointerRotation);
                    
                    case MetaQuestTouchProControllerProfile.QuestProTouchController meta:
                        
                        return GetMetaQuestControllerPoseFallback(
                            meta, 
                            isLHand, 
                            usePointerPose, 
                            meta.devicePosition, 
                            meta.deviceRotation, 
                            meta.pointerPosition, 
                            meta.pointerRotation);
                    
                    case OculusTouchControllerProfile.OculusTouchController oculus:
                        
                        return GetMetaQuestControllerPoseFallback(
                            oculus, 
                            isLHand, 
                            usePointerPose, 
                            oculus.devicePosition, 
                            oculus.deviceRotation, 
                            oculus.pointerPosition, 
                            oculus.pointerRotation);

                    case ValveIndexControllerProfile.ValveIndexController valve:
                        
                        return new Pose(
                            valve.pointerPosition.value + GetControllerPositionOffset(valve.deviceRotation.value, Quaternion.identity, isLHand),
                            valve.pointerRotation.value * GetControllerRotationOffset(isLHand));
                    
                    case HTCViveControllerProfile.ViveController vive:
                        
                        return new Pose(
                            vive.pointerPosition.value + GetControllerPositionOffset(vive.deviceRotation.value, Quaternion.identity, isLHand),
                            vive.pointerRotation.value * GetControllerRotationOffset(isLHand));
#endif
#if MV_USING_OCULUS_SDK
                    case OculusTouchController oculusTouchController:
                    {
                        return GetMetaQuestControllerPoseFallback(
                            oculusTouchController, 
                            isLHand, 
                            usePointerPose, 
                            oculusTouchController.devicePosition, 
                            oculusTouchController.deviceRotation, 
                            oculusTouchController.pointerPosition, 
                            oculusTouchController.pointerRotation);
                    }
#endif
                    default:
                    {
                        return new Pose(
                            controller.devicePosition.value + GetControllerPositionOffset(controller.deviceRotation.value, Quaternion.identity, isLHand),
                            controller.deviceRotation.value * GetControllerRotationOffset(isLHand));
                    }
                }
            }

            print("Using Device Pose: " + controller.name + " | " + controller.GetType().FullName);
            return new Pose(
                controller.devicePosition.value + GetControllerPositionOffset(controller.deviceRotation.value, Quaternion.identity, isLHand),
                controller.deviceRotation.value * GetControllerRotationOffset(isLHand));
        }
        
        private Vector3 GetControllerPositionOffset(Quaternion deviceRot, Quaternion offset, bool isLHand, Vector3 additionalOffset = default)
        {
            return deviceRot * offset * GetControllerRotationOffset(isLHand) * (isLHand ? leftControllerPositionOffset + additionalOffset : rightControllerPositionOffset + additionalOffset);
        }
        
        private Quaternion GetControllerRotationOffset(bool isLHand)
        {
            return Quaternion.Euler(isLHand ? leftControllerRotationOffset : rightControllerRotationOffset);
        }
        
        private Vector3 GetHandPositionOffset(Quaternion rotation, bool isLHand)
        {
            return rotation * GetHandRotationOffset(isLHand) * (isLHand ? leftHandPositionOffset : rightHandPositionOffset);
        }
        
        private Quaternion GetHandRotationOffset(bool isLHand)
        {
            return Quaternion.Euler(isLHand ? leftHandRotationOffset : rightHandRotationOffset);
        }

        #region Bugfix - Weird XR Controllers Tracking Loss

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DelayedRefreshControllers();

        private void OnSceneUnloaded(Scene scene) => DelayedRefreshControllers();

        #endregion

        private void DelayedRefreshControllers()
        {
            MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                if (!this) return;
                if (isActiveAndEnabled)
                    DeactivateCompletely();
            });
        }
        
#endif
    }
}
#endif