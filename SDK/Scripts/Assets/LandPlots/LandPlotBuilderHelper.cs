using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// A helper class that allows Unity level interaction with the Land Plot Builder system.
    /// </summary>
    public partial class LandPlotBuilderHelper : MonoBehaviour
    {
        [Header("Global Events")]
        [Tooltip("Invoked when build mode is activated.")]
        public UnityEvent onBuildModeActive;
        [Tooltip("Invoked when build mode is deactivated.")]
        public UnityEvent onBuildModeInactive;

        [Header("Interaction Events")]
        [Tooltip("Invoked when this game object is selected in build mode.")]
        public UnityEvent onSelected;
        [Tooltip("Invoked when this game object is deselected in build mode.")]
        public UnityEvent onDeselected;

        /// <summary>
        /// Exits the current build mode state.
        /// </summary>
        public void ExitBuildMode()
        {
            ExitBuildModeInternal();
        }
        partial void ExitBuildModeInternal();

        /// <summary>
        /// Tells the builder to move the selected buildable objects to the camera view.
        /// </summary>
        public void MoveSelectedObjectsToView()
        {
            MoveSelectedObjectsToViewInternal();
        }
        partial void MoveSelectedObjectsToViewInternal();

        /// <summary>
        /// Tells the builder to forcefully select this game object.
        /// </summary>
        public void ForceSelection()
        {
            ForceSelectionInternal();
        }
        partial void ForceSelectionInternal();

        /// <summary>
        /// Tells the builder to forcefully deselect this game object.
        /// </summary>
        public void ForceDeselection()
        {
            ForceDeselectionInternal();
        }
        partial void ForceDeselectionInternal();
    }
}