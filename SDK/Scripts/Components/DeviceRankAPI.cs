using System;
using MetaverseCloudEngine.Unity.Attributes;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A component that helps rank devices based on their hardware specifications.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder.PreInitialization)]
    public class DeviceRankAPI : MonoBehaviour
    {
        [Range(0, 1)]
        [SerializeField] private float maxRank = 1f;
        [Range(0, 1)]
        [SerializeField] private float minRank;

        [SerializeField] private UnityEvent onInsideRank = new();
        [SerializeField] private UnityEvent onOutsideRank = new();

        private void Awake()
        {
            if (IsInsideRank())
                onInsideRank.Invoke();
            else
                onOutsideRank.Invoke();
        }

        private bool IsInsideRank()
        {
            var currentDeviceRank = Mathf.Clamp01(MetaverseProgram.DeviceRank);
            return currentDeviceRank >= minRank && currentDeviceRank <= maxRank;
        }

        private void OnValidate()
        {
            if (maxRank < minRank)
                maxRank = minRank;
            if (minRank > maxRank)
                minRank = maxRank;
        }
    }
}