using MetaverseCloudEngine.Unity.Attributes;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Vehicles
{
    [AddComponentMenu(MetaverseConstants.MenuItems.MenuRootPath + "Vehicles/Land/Drivetrain/Vehicles - Drive Force", 3)]

    // The class for RPMs and torque sent through the drivetrain
    public class DriveForce : MonoBehaviour
    {
        [ReadOnly] public float rpm;
        [ReadOnly] public float torque;
        [ReadOnly] public AnimationCurve curve; // Torque curve
        [ReadOnly] public float feedbackRPM; // RPM sent back through the drivetrain
        [ReadOnly] public bool active = true;

        public void SetDrive(DriveForce from) {
            rpm = from.rpm;
            torque = from.torque;
            curve = from.curve;
        }

        // Same as previous, but with torqueFactor multiplier for torque
        public void SetDrive(DriveForce from, float torqueFactor) {
            rpm = from.rpm;
            torque = from.torque * torqueFactor;
            curve = from.curve;
        }
    }
}