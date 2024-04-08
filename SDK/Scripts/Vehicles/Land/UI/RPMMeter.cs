using MetaverseCloudEngine.Unity.Vehicles;
using TMPro;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    /// A class that allows you to display a <see cref="VehicleParent"/> RPM.
    /// </summary>
    [DeclareFoldoutGroup("UI")]
    [DeclareFoldoutGroup("Needle")]
    public class RPMMeter : TriInspectorMonoBehaviour
    {
        [Tooltip("The vehicle used to calculate the RPM.")]
        public VehicleParent vehicle;

        [Tooltip("Optional text to use for displaying the vehicle's RPM.")]
        [Group("UI")] public TMP_Text rpmText;
        [Tooltip("The text format string to use. '{0}' will be replaced with the MPH value.")]
        [Group("UI")] public string rpmTextFormat = "{0} RPM";

        [Tooltip("The RPM gauge / needle transform.")]
        [Group("Needle")] public Transform rpmGauge;
        [Tooltip("The minimum angle of the needle.")]
        [Group("Needle")] public float minAngle = 15;
        [Tooltip("The maximum angle of the needle.")]
        [Group("Needle")] public float maxAngle = -197;
        [Tooltip("The maximum RPM on the gauge.")]
        [Group("Needle")][Min(0)] public float minRPM = 0;
        [Tooltip("The maximum RPM on the gauge.")]
        [Group("Needle")][Min(0)] public float maxRPM = 9;

        private DriveForce _driveForce;

        private void Awake()
        {
            if (vehicle && vehicle.engine)
                _driveForce = vehicle.engine.GetComponent<DriveForce>();
        }

        private void FixedUpdate()
        {
            if (!vehicle) return;
            float rpm = _driveForce.rpm;
            if (rpmText) rpmText.text = string.Format(rpmTextFormat, rpm.ToString("0"));
            if (rpmGauge) rpmGauge.localRotation = Quaternion.Euler(rpmGauge.localEulerAngles.x, rpmGauge.localEulerAngles.y, Mathf.LerpUnclamped(minAngle, maxAngle, Mathf.InverseLerp(minRPM, maxRPM, rpm)));
        }
    }
}
