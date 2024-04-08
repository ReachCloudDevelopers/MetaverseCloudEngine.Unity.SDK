using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    [HideMonoScript]
    [DeclareFoldoutGroup("Descriptor Events")]
    [DeclareFoldoutGroup("Value Events")]
    public class CloudStatAPI : TriInspectorMonoBehaviour
    {
        [Required] public string statID;

        [Group("Descriptor Events")] public UnityEvent<string> onStatName;
        [Group("Descriptor Events")] public UnityEvent<string> onStatDescription;

        [Title("Float")]
        [Group("Value Events")] public UnityEvent<float> onStatValueFloat;
        [Group("Value Events")] public UnityEvent<float> onStatMaxValueFloat;
        [Group("Value Events")] public UnityEvent<float> onStatMinValueFloat;

        [Title("Int")]
        [Group("Value Events")] public UnityEvent<int> onStatValueInt;
        [Group("Value Events")] public UnityEvent<int> onStatMaxValueInt;
        [Group("Value Events")] public UnityEvent<int> onStatMinValueInt;

        [Title("Limits")]
        [Group("Value Events")] public UnityEvent onStatValueAtMin;
        [Group("Value Events")] public UnityEvent onStatValueNotAtMin;
        [Group("Value Events")] public UnityEvent onStatValueAtMax;
        [Group("Value Events")] public UnityEvent onStatValueNotAtMax;

        private Guid _statID;
        private CloudStat _stat;
        private float _lastRecordedValue;

        private void OnValidate()
        {
            if (!Guid.TryParse(statID, out _))
                statID = null;
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(statID) || !Guid.TryParse(statID, out _statID))
            {
                Debug.LogError("Stat ID is not valid.");
                return;
            }

            if (!_stat)
                _stat = CloudStatGroup.FindStat(_statID);
            
            if (_stat)
            {
                if (Math.Abs(_lastRecordedValue - _stat.StatValue) > Mathf.Epsilon)
                    TriggerValueChangeEvents();
                _stat.StatValueChanged += OnStatValueChanged;
                return;
            }

            CloudStatGroup.StatRegistered += OnStatRegistered;
        }

        private void OnDisable()
        {
            CloudStatGroup.StatRegistered -= OnStatRegistered;
            if (_stat)
                _stat.StatValueChanged -= OnStatValueChanged;
        }

        private void OnStatRegistered(CloudStat stat)
        {
            if (stat.StatID != _statID) return;
            _stat = stat;
            CloudStatGroup.StatRegistered -= OnStatRegistered;

            onStatName?.Invoke(_stat.StatName);
            onStatDescription?.Invoke(_stat.StatDescription);

            TriggerValueChangeEvents();
            onStatMaxValueFloat?.Invoke(_stat.StatMaxValue);
            onStatMinValueFloat?.Invoke(_stat.StatMinValue);
            onStatMaxValueInt?.Invoke((int)_stat.StatMaxValue);
            onStatMinValueInt?.Invoke((int)_stat.StatMinValue);

            _stat.StatValueChanged += OnStatValueChanged;
        }

        private void TriggerValueChangeEvents()
        {
            if (!_stat) return;
            if (Math.Abs(_lastRecordedValue - _stat.StatValue) < Mathf.Epsilon) return;
            try
            {
                onStatValueFloat?.Invoke(_stat.StatValue);
                onStatValueInt?.Invoke((int)_stat.StatValue);
            
                if (Math.Abs(_stat.StatValue - _stat.StatMinValue) < Mathf.Epsilon)
                    onStatValueAtMin?.Invoke();
                else onStatValueNotAtMin?.Invoke();
            
                if (Math.Abs(_stat.StatValue - _stat.StatMaxValue) < Mathf.Epsilon)
                    onStatValueAtMax?.Invoke();
                else onStatValueNotAtMax?.Invoke();
            }
            finally
            {
                _lastRecordedValue = _stat.StatValue;
            }
        }

        private void OnStatValueChanged(float val) => TriggerValueChangeEvents();
        
        public void IncreaseStatValue(float amount)
        {
            if (!isActiveAndEnabled)
                return;
            if (_stat)
                _stat.IncreaseStatValue(amount);
        }

        public void DecreaseStatValue(float amount)
        {
            if (!isActiveAndEnabled)
                return;
            if (_stat)
                _stat.DecreaseStatValue(amount);
        }
    }
}