using System;
using System.Globalization;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Robotics
{
    [HideMonoScript]
    public class PololuCompactServoSetTargetCommand : TriInspectorMonoBehaviour
    {
        [Tooltip("The channel to send the command to.")]
        [SerializeField] private byte channel;
        [Tooltip("The current value that's going to processed then sent to the servo.")]
        [SerializeField] private float rawValue;
        [Tooltip("An min value to apply inverse interpolation to the raw value.")]
        [SerializeField] private float minValue;
        [Tooltip("An max value to apply inverse interpolation to the raw value.")]
        [SerializeField] private float maxValue = 1;
        [Min(0)]
        [Tooltip("The minimum microseconds. This value will depend on the manufacturer.")]
        [LabelText("\u00B5s Min")]
        [SerializeField] private float pMin = 800;
        [Min(0)]
        [Tooltip("The maximum microseconds. This value will depend on the manufacturer.")]
        [LabelText("\u00B5s Max")]
        [SerializeField] private float pMax = 2200;
        [Tooltip("Automatically write to the servo in FixedUpdate().")]
        [SerializeField] private bool writeInFixedUpdate = true;
        [SerializeField] private UnityEvent<byte[]> onWriteBytes = new();
        [ShowIf(nameof(writeInFixedUpdate))]
        [Tooltip("Specify settings to do software based dampening.")]
        [SerializeField] private SmoothDamp smoothDampSettings = new();

        [Serializable]
        public class SmoothDamp
        {
            [DisableInPlayMode]
            public bool enabled;
            [DisableInPlayMode]
            public bool angle;
            [Min(0)]
            public float maxSpeed = 0.5f;
            [Min(0)]
            public float smoothTime = 0.5f;
            public float initialValue;
            
            [NonSerialized] public float CurrentVelocity;
            [NonSerialized] public float CurrentValue;
            [NonSerialized] public bool IsInitialized;
        }

        public float RawValue
        {
            get => rawValue;
            set => this.rawValue = value;
        }

        public int RawValueInt
        {
            get => (int)rawValue;
            set => this.rawValue = value;
        }

        public string RawValueString
        {
            get => rawValue.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (float.TryParse(value, out var result))
                    rawValue = result;
            }
        }

        private void OnValidate()
        {
            if (pMin > pMax)
                pMin = pMax;
            if (pMax < pMin)
                pMax = pMin;
        }

        private void FixedUpdate()
        {
            if (writeInFixedUpdate)
                WriteCommand();
        }

        public void WriteCommand()
        {
            WriteCommand(channel);
        }

        /// <summary>
        /// Writes the command to the serial port.
        /// </summary>
        public void WriteCommand(int channel)
        {
            if (!isActiveAndEnabled)
                return;
            
            var servoValue = GetServoValue();
            var command = new byte[4];
            command[0] = 0x84; // Set Target
            command[1] = (byte)channel;
            command[2] = (byte)(servoValue & 0x7F); // low bits
            command[3] = (byte)((servoValue >> 7) & 0x7F); // high bits
            onWriteBytes?.Invoke(command);
        }

        private int GetServoValue()
        {
            var inputValue = SmoothDampInput(rawValue);
            var value = (int)Mathf.Lerp(
                pMin * 4,
                pMax * 4, 
                Mathf.InverseLerp(minValue, maxValue, inputValue));

            return value;
        }

        private float SmoothDampInput(float inputValue)
        {
            if (smoothDampSettings?.enabled != true) 
                return inputValue;
            
            if (!smoothDampSettings.IsInitialized)
            {
                smoothDampSettings.IsInitialized = true;
                smoothDampSettings.CurrentValue = smoothDampSettings.initialValue;
            }

            if (smoothDampSettings.angle)
            {
                return Mathf.SmoothDampAngle(
                    smoothDampSettings.CurrentValue,
                    rawValue,
                    ref smoothDampSettings.CurrentVelocity, smoothDampSettings.smoothTime,
                    smoothDampSettings.maxSpeed, Time.fixedDeltaTime);
            }

            return Mathf.SmoothDamp(
                smoothDampSettings.CurrentValue,
                rawValue,
                ref smoothDampSettings.CurrentVelocity, smoothDampSettings.smoothTime,
                smoothDampSettings.maxSpeed, Time.fixedDeltaTime);
        }

#if UNITY_EDITOR
        [Button("Pololu Command Documentation")]
        // ReSharper disable once UnusedMember.Local
        private void OpenPololuDocs()
        {
            Application.OpenURL("https://www.pololu.com/docs/0J40/5.e");
        }
#endif
    }
}