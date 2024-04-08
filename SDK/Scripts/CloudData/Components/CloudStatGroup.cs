using System;
using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Unity.Async;
using Newtonsoft.Json;
using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public class CloudStatGroup : TriInspectorMonoBehaviour
    {
        [Tooltip("The Cloud Data Record that this stat group is stored in.")]
        [SerializeField] private CloudDataRecord dataRecord;
        [Tooltip("The interval in seconds that the stat group will be saved.")]
        [Min(1f)] [SerializeField] private float saveIntervalSeconds = 5;

        private bool _isOffline;
        private bool _saveQueued;
        private bool _isInitialized;
        private readonly List<Guid> _localStatIDs = new();
        private static readonly Dictionary<Guid, CloudStat> StatMap = new();

        /// <summary>
        /// Invoked when a stat is registered.
        /// </summary>
        public static event Action<CloudStat> StatRegistered;

        private void Awake()
        {
            _isOffline = !dataRecord;
            if (!_isOffline && dataRecord.LoadOnStart)
                dataRecord.Events.onLoadFinished.AddListener(OnLoad);
            else OnLoad();
        }

        private void OnLoad()
        {
            if (_isOffline)
                return;
            
            _saveQueued = false;
            
            var json = dataRecord.StringValue;
            IDictionary<Guid, float> statValues = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { statValues = JsonConvert.DeserializeObject<Dictionary<Guid, float>>(json); }
                catch { statValues = null; }
            }

            var stats = GetComponentsInChildren<CloudStat>(true);
            foreach (var stat in stats)
            {
                if (!stat.StatID.HasValue)
                    continue;

                StatMap[stat.StatID.Value] = stat;
                _localStatIDs.Add(stat.StatID.Value);

                if (statValues is not null && 
                    statValues.TryGetValue(stat.StatID.Value, out var value))
                    stat.SetStatValue(value);
            }

            foreach (var stat in stats)
                StatRegistered?.Invoke(stat);

            _isInitialized = true;
        }

        private void Reset()
        {
            dataRecord = GetComponent<CloudDataRecord>();
        }

        private void OnDestroy()
        {
            foreach (var statID in _localStatIDs)
                StatMap.Remove(statID);
        }

        public void QueueSave()
        {
            if (!_isInitialized) return;
            if (_isOffline) return;
            if (_saveQueued) return;
            _saveQueued = true;
            MetaverseDispatcher.WaitForSeconds(saveIntervalSeconds, () =>
            {
                if (!this) return;
                if (!_saveQueued) return;
                _saveQueued = false;
                dataRecord.StringValue = JsonConvert.SerializeObject(
                    StatMap.Where(x => x.Value.CanBeSaved).ToDictionary(
                        kvp => kvp.Key, kvp => kvp.Value.StatValue));
                dataRecord.Save();
            });
        }

        public static CloudStat FindStat(Guid statID)
        {
            return StatMap.GetValueOrDefault(statID);
        }
    }
}