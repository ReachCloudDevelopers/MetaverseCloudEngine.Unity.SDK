using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Inputs.Components
{
    public class GyroscopeAPI : MonoBehaviour
    {
        public bool autoEnableGyroScope = true;
        public bool autoDisableGyroScope = true;
        public UnityEvent<Vector3> onAcceleration;
        public UnityEvent<Vector3> onRotation;
        public UnityEvent<Quaternion> onRotationQ;

        private void OnEnable()
        {
            if (autoEnableGyroScope)
                Input.gyro.enabled = true;
        }

        private void OnDisable()
        {
            if (autoDisableGyroScope)
                Input.gyro.enabled = false;
        }

        private void Update()
        {
            onAcceleration.Invoke(Input.acceleration);
            onRotation.Invoke(Input.gyro.attitude.eulerAngles);
            onRotationQ.Invoke(Input.gyro.attitude);
        }
        
        public void SetGyroEnabled(bool e)
        {
            Input.gyro.enabled = e;
        }
    }
}