using UnityEngine;
using UnityEngine.InputSystem;

namespace MetaverseCloudEngine.Unity.Vehicles.Flight
{
    public class HeliInput : MonoBehaviour
    {
        [SerializeField] private Heli heli;
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private InputActionProperty moveInput;
        [SerializeField] private InputActionProperty engineInput;
        [SerializeField] private InputActionProperty liftInput;
        [SerializeField] private InputActionProperty yawInput;

        private Transform _transform;

        /// <summary>
        /// Gets or sets a value indicating whether or not input is enabled.
        /// </summary>
        public bool InputEnabled { get => inputEnabled; set => inputEnabled = value; }

        private void Reset()
        {
            heli = this.GetNearestComponent<Heli>();
        }

        private void Awake()
        {
            _transform = transform;
        }

        private void OnEnable()
        {
            moveInput.action?.Enable();
            engineInput.action?.Enable();
            liftInput.action?.Enable();
            yawInput.action?.Enable();
        }

        private void OnDisable()
        {
            moveInput.action?.Disable();
            engineInput.action?.Disable();
            liftInput.action?.Disable();
            yawInput.action?.Disable();
        }

        private void OnDestroy()
        {
            moveInput.action?.Dispose();
            engineInput.action?.Dispose();
            liftInput.action?.Dispose();
            yawInput.action?.Dispose();
        }

        private void Update()
        {
            if (!inputEnabled)
            {
                heli.Move(Vector3.zero);
                heli.Lift(0);
                heli.Yaw(0);
                return;
            }

            Vector2 input = moveInput.action.ReadValue<Vector2>();
            Vector3 localMoveDir = Quaternion.AngleAxis(_transform.localEulerAngles.y, _transform.parent ? _transform.parent.up : Vector3.up) * new Vector3(input.x, 0, input.y);
            heli.Move(localMoveDir);
            heli.Yaw(yawInput.action.ReadValue<float>());

            if (engineInput.action.triggered)
            {
                if (heli.IsFlying) heli.Land();
                else heli.TakeOff();
            }

            heli.Lift(liftInput.action.ReadValue<float>());
        }
    }
}
