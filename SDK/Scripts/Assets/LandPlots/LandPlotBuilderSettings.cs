using System;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// Defins a building style for land plots.
    /// </summary>
    public enum LandPlotBuildStyle
    {
        /// <summary>
        /// The overhead builder is similar to Sims' building system.
        /// </summary>
        Overhead,
        /// <summary>
        /// The intricate builder allows you to build from a first person view.
        /// </summary>
        Intricate
    }

    /// <summary>
    /// These are settings that the land plot uses when building.
    /// </summary>
    [Serializable]
    public class LandPlotBuilderSettings
    {
        [Tooltip("Every change will result in the save logic being ran on the land plot automatically.")]
        public bool autoSave = true;
        [Tooltip("If an error occurs when the landplot loads, it will be displayed via a popup.")]
        public bool displayLoadErrors;
        [Tooltip("If an error occurs when the landplot saves, it will be displayed via a popup.")]
        public bool displaySaveErrors = true;
        [Tooltip("Whether to allow built-in objects to be spawned. If you don't want people to spawn a spawn-point for example. Usually this should be left false.")]
        public bool allowBuiltInPrefabs;

        [Header("Interaction")]
        [Tooltip("The land plot's building style.")]
        public LandPlotBuildStyle buildStyle = LandPlotBuildStyle.Overhead;
        [Tooltip("A grid-snap interval to use for buildable object positions. This is useful for making objects snap together that weren't necessarily designed to.")]
        public float positionSnapInterval = 0.25f;

        [Header("Limits")]
        [Tooltip("Bounding colliders that are considered when placing an object, so that it will be detected as a valid placement. Any object that attempts to spawn outside this area will not be allowed.")]
        public BoxCollider[] boundingColliders = Array.Empty<BoxCollider>();
        [Tooltip("The maximum amount of objects that this land plot can contain.")]
        [Min(-1)] public int maximumObjectCount = -1;
        [Tooltip("The maximum total file size of the objects placed in this land plot.")]
        [Min(-1)] public long maximumSizeBytes = -1;

        [Header("Underground")]
        [Tooltip("Checks if the object is under the ground before spawning to prevent objects from being placed underneath terrain.")]
        public bool checkTerrainHeight = true;
        [Tooltip("The maximum distance that an object can be below the terrain before it is unplacable.")]
        public float maxUndergroundDistance = 5;
    }
}