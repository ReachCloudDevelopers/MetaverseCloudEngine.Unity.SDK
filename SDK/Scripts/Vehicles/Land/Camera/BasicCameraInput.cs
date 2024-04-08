using UnityEngine;
using UnityEngine.InputSystem;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Camera/Vehicles - Basic Camera Input", 1)]

    // Class for setting the camera input with the input manager
    public class BasicCameraInput : MonoBehaviour
    {
        public InputActionProperty xInputAxis;
        public InputActionProperty yInputAxis;

        private CameraControl _cam;

        void Start() {
            // Get camera controller
            _cam = GetComponent<CameraControl>();
            xInputAxis.action.Enable();
            yInputAxis.action.Enable();
        }

        void FixedUpdate() {
            // Set camera rotation input if the input axes are valid
            if (_cam) {
                _cam.SetInput(
                    xInputAxis.action.ReadValue<float>(), 
                    yInputAxis.action.ReadValue<float>());
            }
        }
    }
}