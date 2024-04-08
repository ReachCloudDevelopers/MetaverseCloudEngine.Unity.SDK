using MetaverseCloudEngine.Unity.Services.Options;

using System;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.MetaSpaces
{
    public enum MetaSpaceQualityLevel
    {
        VeryLow = 1,
        Low,
        Medium,
        High,
        VeryHigh,
        Ultra
    }

    /// <summary>
    /// MetaSpaceRuntimeOptions is a class that defines runtime options for a MetaSpace.
    /// </summary>
    [Serializable]
    public class MetaSpaceRuntimeOptions : IMetaSpaceStateOptions
    {
        [SerializeField, Tooltip("Determines whether the Meta Space should automatically start when the scene loads.")]
        private bool autoStart = true;
        [SerializeField, Tooltip("Determines whether the Meta Space should automatically end when the scene unloads.")]
        private bool autoEnd = false;
        [SerializeField, Tooltip("Determines whether the Meta Space should allow new connections after it has started.")]
        private bool allowConnectionsAfterStart = true;

        /// <summary>
        /// Gets a value indicating whether the Meta Space should automatically start when the scene loads.
        /// </summary>
        public bool AutoStart => autoStart;

        /// <summary>
        /// Gets a value indicating whether the Meta Space should automatically end when the scene unloads.
        /// </summary>
        public bool AutoEnd => autoEnd;

        /// <summary>
        /// Gets a value indicating whether the Meta Space should allow new connections after it has started.
        /// </summary>
        public bool AllowConnectionsAfterStart => allowConnectionsAfterStart;
    }
}
