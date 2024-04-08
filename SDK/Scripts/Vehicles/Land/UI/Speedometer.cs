using MetaverseCloudEngine.Unity.Vehicles;
using TMPro;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity
{
    /// <summary>
    /// A class that allows you to display a <see cref="VehicleParent"/> speed in MPH.
    /// </summary>
    [DeclareFoldoutGroup("Needle")]
    [DeclareFoldoutGroup("UI")]
    public class Speedometer : TriInspectorMonoBehaviour
    {
        [Tooltip("The vehicle used to calculate the speed.")]
        public VehicleParent vehicle;

        [Tooltip("Optional text to use for displaying the vehicle's speed.")]
        [Group("UI")] public TMP_Text speedText;
        [Tooltip("The text format string to use. '{0}' will be replaced with the MPH value.")]
        [Group("UI")] public string speedTextFormat = "{0} MPH";

        [Tooltip("The speed gauge / needle transform.")]
        [Group("Needle")] public Transform speedGauge;
        [Tooltip("The minimum angle of the needle.")]
        [Group("Needle")] public float minAngle = 15;
        [Tooltip("The maximum angle of the needle.")]
        [Group("Needle")] public float maxAngle = -197;
        [Tooltip("The minimum speed on the speed gauge.")]
        [Group("Needle")] [Min(0)] public float minSpeed = 0;
        [Tooltip("The maximum speed on the speed gauge.")]
        [Group("Needle")] [Min(0)] public float maxSpeed = 260;

        private void FixedUpdate()
        {
            if (!vehicle) return;
            float mph = vehicle.velMag * 2.23694f;
            if (speedText) speedText.text = string.Format(speedTextFormat, mph.ToString("0"));
            if (speedGauge) speedGauge.localRotation = Quaternion.Euler(speedGauge.localEulerAngles.x, speedGauge.localEulerAngles.y, Mathf.LerpUnclamped(minAngle, maxAngle, Mathf.InverseLerp(minSpeed, maxSpeed, mph)));
        }
    }
}
