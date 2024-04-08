using System;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    [HideMonoScript]
    public class CloudStat : TriInspectorMonoBehaviour
    {
        [ReadOnly]
        [SerializeField] private string statID;
        [SerializeField] private string statName = "New Stat";
        [SerializeField] private string statDescription = "";
        [SerializeField] private float statValue = 1;
        [SerializeField] private float statMinValue;
        [SerializeField] private float statMaxValue = 1;
        [SerializeField] private bool saveOnChange = true;
        [SerializeField] private bool canBeSaved = true;
        
        private CloudStatGroup _cloudStatGroup;
        
        public event Action<float> StatValueChanged;

        /// <summary>
        /// The value of the stat.
        /// </summary>
        public float StatValue => statValue;

        /// <summary>
        /// The name of the stat.
        /// </summary>
        public string StatName
        {
            get => statName;
            set => statName = value;
        }

        /// <summary>
        /// Whether or not the stat can be saved.
        /// </summary>
        public bool CanBeSaved
        {
            get => canBeSaved;
            set => canBeSaved = value;
        }

        /// <summary>
        /// The ID of the stat.
        /// </summary>
        public Guid? StatID
        {
            get
            {
                if (string.IsNullOrEmpty(statID))
                    return null;
                if (!Guid.TryParse(statID, out var guid))
                    return null;
                return guid;
            }
        }

        /// <summary>
        /// The maximum value of the stat.
        /// </summary>
        public float StatMaxValue
        {
            get => statMaxValue;
            set => statMaxValue = value;
        }

        /// <summary>
        /// The minimum value of the stat.
        /// </summary>
        public float StatMinValue
        {
            get => statMinValue;
            set => statMinValue = value;
        }

        /// <summary>
        /// Whether or not to save the stat when it changes.
        /// </summary>
        public bool SaveOnChange
        {
            get => saveOnChange;
            set => saveOnChange = value;
        }

        /// <summary>
        /// Gets or sets the description of the stat.
        /// </summary>
        public string StatDescription
        {
            get => statDescription;
            set => statDescription = value;
        }

        private void Awake()
        {
            _cloudStatGroup = GetComponentInParent<CloudStatGroup>(true);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(statID))
                statID = Guid.NewGuid().ToString();
            if (statMaxValue < statMinValue)
                statMaxValue = statMinValue;
            if (statValue > statMaxValue)
                statValue = statMaxValue;
            if (statValue < statMinValue)
                statValue = statMinValue;
        }

        public void IncreaseStatValue(float amount)
        {
            if (!isActiveAndEnabled)
                return;
            SetStatValue(statValue + amount);
        }

        public void SetBoolValue(bool boolean)
        {
            if (!isActiveAndEnabled)
                return;
            SetStatValue(boolean ? 1 : 0);
        }

        public void DecreaseStatValue(float amount)
        {
            if (!isActiveAndEnabled)
                return;
            SetStatValue(statValue - amount);
        }

        public void SetStatValue(float value)
        {
            if (!isActiveAndEnabled)
                return;
            statValue = value;
            if (statValue > statMaxValue)
                statValue = statMaxValue;
            if (statValue < statMinValue)
                statValue = statMinValue;
            StatValueChanged?.Invoke(value);
            if (saveOnChange)
                _cloudStatGroup.QueueSave();
        }
    }
}