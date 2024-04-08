using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using MetaverseCloudEngine.Unity.Physix.Components;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Vehicle Controllers/Vehicles - Vehicle Debug", 3)]
    public class VehicleDebug : MonoBehaviour
    {
        public InputActionProperty resetRotationInput;
        public InputActionProperty resetPositionInput;
        public Vector3 spawnPos;
        public Vector3 spawnRot;

        [Tooltip("Y position below which the vehicle will be reset")]
        public float fallLimit = -10;

        private void Start()
        {
            resetRotationInput.action.Enable();
            resetPositionInput.action.Enable();
        }

        private void OnEnable()
        {
            resetRotationInput.action.performed += OnResetRotationInput;
            resetPositionInput.action.performed += OnResetPositionInput;
        }

        private void OnDisable()
        {
            resetRotationInput.action.performed -= OnResetRotationInput;
            resetPositionInput.action.performed -= OnResetPositionInput;
        }

        void Update() {
            if (FloatingOrigin.UnityToOrigin(transform.position).y < fallLimit) {
                StartCoroutine(ResetPosition());
            }
        }

        // This waits for the next fixed update before resetting the rotation of the vehicle
        IEnumerator ResetRotation() {
            if (GetComponent<VehicleDamage>()) {
                GetComponent<VehicleDamage>().Repair();
            }

            yield return new WaitForFixedUpdate();
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            transform.Translate(Vector3.up, Space.World);
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }

        // This waits for the next fixed update before resetting the position of the vehicle
        IEnumerator ResetPosition() {
            if (GetComponent<VehicleDamage>()) {
                GetComponent<VehicleDamage>().Repair();
            }

            transform.position = FloatingOrigin.OriginToUnity(spawnPos);
            yield return new WaitForFixedUpdate();
            transform.rotation = Quaternion.LookRotation(spawnRot, Vector3.up);
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
        }

        private void OnResetRotationInput(InputAction.CallbackContext obj) => StartCoroutine(ResetRotation());
        private void OnResetPositionInput(InputAction.CallbackContext obj) => StartCoroutine(ResetPosition());
    }
}