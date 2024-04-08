using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Audio.Abstract;
using MetaverseCloudEngine.Unity.Components;
using TMPro;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Audio.Components
{
    public class MicrophoneDropdown : MetaSpaceBehaviour
    {
        public string defaultMicrophoneName = "System Default";
        public TMP_Dropdown dropdown;

        private IMicrophoneService _microphoneService;

        protected override void Awake()
        {
            base.Awake();
            
            if (!dropdown)
                enabled = false;
        }

        private void Reset()
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        private void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            RefreshDeviceList();
        }

        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        protected override void OnMetaSpaceBehaviourInitialize()
        {
            base.OnMetaSpaceBehaviourInitialize();
            _microphoneService = MetaSpace.GetService<IMicrophoneService>();
            RefreshDeviceList();
        }

        private void OnAudioConfigurationChanged(bool wasChanged)
        {
            RefreshDeviceList();
        }

        public void RefreshDeviceList()
        {
            dropdown.onValueChanged.RemoveListener(OnValueChanged);
            dropdown.options = new List<TMP_Dropdown.OptionData>
            {
                new(defaultMicrophoneName)
            };
            
            dropdown.AddOptions(_microphoneService?.ConnectedAudioRecordingDevices.ToList() ?? new List<string>());
            int selectedIndex = dropdown.options.FindIndex(x => x.text == _microphoneService?.GetActiveAudioRecordingDevice());
            if (selectedIndex == -1) selectedIndex = 0;
            dropdown.value = selectedIndex;
            dropdown.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnValueChanged(int deviceIndex)
        {
            if (deviceIndex == -1 || deviceIndex >= dropdown.options.Count)
                deviceIndex = 0;
            var deviceName = dropdown.options[deviceIndex].text;
            if (deviceName == defaultMicrophoneName)
                deviceName = null;
            _microphoneService?.SetActiveAudioRecordingDevice(deviceName);
        }
    }
}