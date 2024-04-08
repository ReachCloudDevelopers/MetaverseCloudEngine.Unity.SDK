using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// Events that a landplot exposes to the Unity front-end.
    /// </summary>
    [System.Serializable]
    public class LandPlotEvents
    {
        [Header("Loading Events")]
        [Tooltip("Invoked when the landplot has started loading.")]
        public UnityEvent onLoadStarted = new();
        [Tooltip("Invoked when the landplot has finished loading.")]
        public UnityEvent onLoadFinished = new();
        [Tooltip("Invoked when the landplot has finished loading successfully.")]
        public UnityEvent onLoadSuccess = new();
        [Tooltip("Invoked when the landplot has finished loading but failed to actually load completely.")]
        public UnityEvent onLoadFailed = new();

        [Header("Saving Events")]
        [Tooltip("Invoked when the landplot has started saving.")]
        public UnityEvent onSaveStarted = new();
        [Tooltip("Invoked when the landplot has finished saving.")]
        public UnityEvent onSaveFinished = new();
        [Tooltip("Invoked when the landplot has finished saving successfully.")]
        public UnityEvent onSaveSuccess = new();
        [Tooltip("Invoked when the landplot has finished saving but failed to actually save completely.")]
        public UnityEvent onSaveFailed = new();

        [Header("Build Mode Events")]
        [Tooltip("Invoked when build mode has started.")]
        public UnityEvent onBuilderEntered = new();
        [Tooltip("Invoked when build mode has stopped.")]
        public UnityEvent onBuilderExited = new();

        [Header("Access Events")]
        [Tooltip("Invoked when the local user has the authority to build on this plot.")]
        public UnityEvent onHasBuildAccess = new();
        [Tooltip("Invoked when the local user does not have the authority to build on this plot.")]
        public UnityEvent onNoBuildAccess = new();

        [Header("Limits")]
        [Tooltip("Invoked when the size limit percentage (from 0 - 1) has changed.")]
        public UnityEvent<float> onSizeLimitPercentage = new();
        [Tooltip("Invoked with the current size in bytes of the landplot.")]
        public UnityEvent<string> onCurrentSizeString = new();
        [Tooltip("Invoked with the total allowed size in bytes of the landplot.")]
        public UnityEvent<string> onTotalSizeString = new();
        [Space]
        public UnityEvent<float> onObjectCountLimitPercentage = new();
        public UnityEvent<string> onCurrentObjectCountString = new();
        public UnityEvent<string> onTotalObjectCountString = new();
    }
}