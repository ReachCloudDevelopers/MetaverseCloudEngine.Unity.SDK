using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    [DeclareFoldoutGroup("Offsets & Adjustments")]
    [DeclareFoldoutGroup("References (Optional)")]
    [DeclareFoldoutGroup("Outputs")]
    [HideMonoScript]
    public class GetAngle : TriInspectorMonoBehaviour
    {
        public enum AngleRange
        {
            [InspectorName("0 to 360")] ZeroTo360,
            [InspectorName("-180 to +180")] Negative180ToPositive180
        }

        public enum CalculationMode
        {
            [InspectorName("localRotation.eulerAngles")]
            EulerAngle,
            [InspectorName("Vector3.Dot")]
            Vector3Angle,
        }

        [Group("Outputs")] public UnityEvent<float> onOutputAngleX;
        [Group("Outputs")] public UnityEvent<float> onOutputAngleY;
        [Group("Outputs")] public UnityEvent<float> onOutputAngleZ;

        [Group("References (Optional)")] public Transform source;
        [Group("References (Optional)")] public Transform parent;

        [Title("Offsets")]
        [Group("Offsets & Adjustments")] public float outputAngleXOffset;
        [Group("Offsets & Adjustments")] public float outputAngleYOffset;
        [Group("Offsets & Adjustments")] public float outputAngleZOffset;

        [Title("Inversion")]
        [Group("Offsets & Adjustments")] public bool outputAngleXInvert;
        [Group("Offsets & Adjustments")] public bool outputAngleYInvert;
        [Group("Offsets & Adjustments")] public bool outputAngleZInvert;

        [Title("Range")]
        [Group("Offsets & Adjustments")] public AngleRange outputAngleXRange;
        [Group("Offsets & Adjustments")] public AngleRange outputAngleYRange;
        [Group("Offsets & Adjustments")] public AngleRange outputAngleZRange;
        [Group("Offsets & Adjustments")] public CalculationMode calculationMode;

        private Quaternion _currentRotation;

        /// <summary>
        /// The output angle in degrees around the X axis.
        /// </summary>
        [ReadOnly]
        [ShowInInspector]
        public float OutputAngleX { get; private set; }

        /// <summary>
        /// The output angle in degrees around the Y axis.
        /// </summary>
        [ReadOnly]
        [ShowInInspector]
        public float OutputAngleY { get; private set; }

        /// <summary>
        /// The output angle in degrees around the Z axis.
        /// </summary>
        [ReadOnly]
        [ShowInInspector]
        public float OutputAngleZ { get; private set; }

        private void Start()
        {
            if (source == null)
                source = transform;
            if (parent == null && source == transform)
                parent = source.parent;
        }

        private void Update()
        {
            Tick();
        }

        private void FixedUpdate()
        {
            Tick();
        }

        private void Tick()
        {
            try
            {
                _currentRotation = source.rotation * Quaternion.Euler(
                    outputAngleXOffset,
                    outputAngleYOffset,
                    outputAngleZOffset
                );

                ProcessAndSetOutputAngles();
            }
            catch (NullReferenceException e)
            {
                MetaverseProgram.Logger.LogError(e);
                enabled = false;
            }
            catch (MissingReferenceException e)
            {
                MetaverseProgram.Logger.LogError(e);
                enabled = false;
            }
        }

        private void ProcessAndSetOutputAngles()
        {
            var parentRotation = GetParentRotation();
            var localRotation = Quaternion.Inverse(parentRotation) * _currentRotation;

            // Extract the individual Euler angles from the local rotation quaternion
            float pitch;
            float yaw;
            float roll;

            if (calculationMode == CalculationMode.EulerAngle)
            {
                var eulerAngles = localRotation.eulerAngles;
                pitch = eulerAngles.x;
                yaw = eulerAngles.y;
                roll = eulerAngles.z;
                
                if (outputAngleXInvert) pitch = 360 - pitch;
                if (outputAngleYInvert) yaw = 360 - yaw;
                if (outputAngleZInvert) roll = 360 - roll;

                if (outputAngleXRange == AngleRange.Negative180ToPositive180) pitch = pitch > 180 ? pitch - 360 : pitch;
                if (outputAngleYRange == AngleRange.Negative180ToPositive180) yaw = yaw > 180 ? yaw - 360 : yaw;
                if (outputAngleZRange == AngleRange.Negative180ToPositive180) roll = roll > 180 ? roll - 360 : roll;
            }
            else
            {
                var localForward = localRotation * Vector3.forward;
                var localRight = localRotation * Vector3.right;
                
                var forwardDot = Vector3.Dot(Vector3.ProjectOnPlane(localForward, Vector3.right).normalized, Vector3.forward);
                var upDot = Vector3.Dot(Vector3.ProjectOnPlane(localForward, Vector3.up).normalized, Vector3.forward);
                var rightDot = Vector3.Dot(Vector3.ProjectOnPlane(localRight, Vector3.forward).normalized, Vector3.right);
                
                var forwardAngle = Mathf.Acos(forwardDot) * Mathf.Rad2Deg;
                var upAngle = Mathf.Acos(upDot) * Mathf.Rad2Deg;
                var rightAngle = Mathf.Acos(rightDot) * Mathf.Rad2Deg;

                pitch = forwardAngle;
                yaw = upAngle;
                roll = rightAngle;
                
                if (localForward.y > 0) pitch = -pitch;
                if (localForward.x < 0) yaw = -yaw;
                if (localRight.z < 0) roll = -roll;
                
                if (outputAngleXInvert) pitch = -pitch;
                if (outputAngleYInvert) yaw = -yaw;
                if (outputAngleZInvert) roll = -roll;
                
                if (outputAngleXRange == AngleRange.ZeroTo360) pitch = pitch < 0 ? 360 + pitch : pitch;
                if (outputAngleYRange == AngleRange.ZeroTo360) yaw = yaw < 0 ? 360 + yaw : yaw;
                if (outputAngleZRange == AngleRange.ZeroTo360) roll = roll < 0 ? 360 + roll : roll;
            }

            // Set the output angles
            OutputAngleX = pitch;
            OutputAngleY = yaw;
            OutputAngleZ = roll;

            // Invoke events or do any other processing with the output angles
            onOutputAngleX?.Invoke(OutputAngleX);
            onOutputAngleY?.Invoke(OutputAngleY);
            onOutputAngleZ?.Invoke(OutputAngleZ);
        }

        private Quaternion GetParentRotation()
        {
            return parent ? parent.rotation : Quaternion.identity;
        }
    }
}