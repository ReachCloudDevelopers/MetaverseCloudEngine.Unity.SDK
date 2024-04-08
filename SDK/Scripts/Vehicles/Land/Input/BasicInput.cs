using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleParent))]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Input/Vehicles - Basic Input", 0)]

    // Class for setting the input with the input manager
    public class BasicInput : MonoBehaviour
    {
        public bool requireCursorLocked = true;
        public InputActionProperty accelAxis;
        public InputActionProperty brakeAxis;
        public InputActionProperty steerAxis;
        public InputActionProperty ebrakeAxis;
        public InputActionProperty boostButton;
        public InputActionProperty upshiftButton;
        public InputActionProperty downshiftButton;
        public InputActionProperty pitchAxis;
        public InputActionProperty yawAxis;
        public InputActionProperty rollAxis;

        private VehicleParent _vehicle;
        
        private void Awake()
        {
            _vehicle = GetComponent<VehicleParent>();
        }

        private void OnEnable()
        {
            accelAxis.action.Enable();
            brakeAxis.action.Enable();
            steerAxis.action.Enable();
            ebrakeAxis.action.Enable();
            boostButton.action.Enable();
            upshiftButton.action.Enable();
            downshiftButton.action.Enable();
            pitchAxis.action.Enable();
            yawAxis.action.Enable();
            rollAxis.action.Enable();
            upshiftButton.action.performed += OnUpShiftPressed;
            downshiftButton.action.performed += OnDownShiftPressed;
        }

        private void OnDisable()
        {
            upshiftButton.action.performed -= OnUpShiftPressed;
            downshiftButton.action.performed -= OnDownShiftPressed;
        }

        private void FixedUpdate()
        {
            if (FreezeInput())
            {
                ZeroInput();
                return;
            }
            
            _vehicle.SetAccel(accelAxis.action.ReadValue<float>());
            _vehicle.SetBrake(brakeAxis.action.ReadValue<float>());
            _vehicle.SetSteer(steerAxis.action.ReadValue<float>());
            _vehicle.SetEbrake(ebrakeAxis.action.ReadValue<float>());
            _vehicle.SetBoost(boostButton.action.IsPressed());
            _vehicle.SetPitch(pitchAxis.action.ReadValue<float>());
            _vehicle.SetYaw(yawAxis.action.ReadValue<float>());
            _vehicle.SetRoll(rollAxis.action.ReadValue<float>());
            _vehicle.SetUpshift(upshiftButton.action.ReadValue<float>());
            _vehicle.SetDownshift(downshiftButton.action.ReadValue<float>());
        }

        private bool FreezeInput()
        {
            if (Application.isMobilePlatform)
                return false;
            if (XRSettings.isDeviceActive)
                return false;
            return requireCursorLocked && !Cursor.lockState.Equals(CursorLockMode.Locked);
        }

        private void ZeroInput()
        {
            _vehicle.SetAccel(0);
            _vehicle.SetBrake(0);
            _vehicle.SetSteer(0);
            _vehicle.SetEbrake(0);
            _vehicle.SetBoost(false);
            _vehicle.SetPitch(0);
            _vehicle.SetYaw(0);
            _vehicle.SetRoll(0);
            _vehicle.SetUpshift(0);
            _vehicle.SetDownshift(0);
        }

        private void OnUpShiftPressed(InputAction.CallbackContext obj)
        {
            if (FreezeInput())
                return;
            _vehicle.PressUpshift();
        }

        private void OnDownShiftPressed(InputAction.CallbackContext obj)
        {
            if (FreezeInput())
                return;
            _vehicle.PressDownshift();
        }
    }
}