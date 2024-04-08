using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using MetaverseCloudEngine.Unity.Rendering;

using UnityEngine;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    /// <summary>
    /// Land plots are an asset type within the engine that allows users to place
    /// objects and build user-generated content at runtime.
    /// </summary>
    public partial class LandPlot : Asset<LandPlotMetadata>, IMeasureCameraDistance
    {
        private MetaSpace _currentMetaSpace; // A reference to the current meta space.
        private Transform _transform; // Used for caching the transform for faster property access.

        [Tooltip("Settings for the in-app builder.")]
        public LandPlotBuilderSettings builderSettings = new();
        [Tooltip("Requirements that must be met in order for the land plot, or objects, to load.")]
        public LandPlotLoadRequirements loadRequirements = new();
        [Tooltip("Events that are invoked by the land plot that can be used for custom user logic.")]
        public LandPlotEvents events = new();

        /// <summary>
        /// A value indicating whether the land plot has fully loaded.
        /// </summary>
        public bool IsLoaded { get; private set; }
        /// <summary>
        /// A value indicating whether the land plot is currently loading.
        /// </summary>
        public bool IsLoading { get; private set; }
        /// <summary>
        /// A value indicating whether the land plot is currently saving.
        /// </summary>
        public bool IsSaving { get; private set; }
        /// <summary>
        /// The source ID to use for the land plot, this value should be used to determine
        /// the unique identifier within the scene for this land plot.
        /// </summary>
        public string BuildingSourceID => ID == null ? blockchainSource : ID.Value.ToString();
        /// <summary>
        /// A value indicating whether the user is allowed to build on this land plot.
        /// </summary>
        public bool IsAllowedToBuild {
            get {
                var canBuild = false;
                CanBuildInternal(ref canBuild);
                return canBuild;
            }
        }
        /// <summary>
        /// A container that is used to detect default objects. This is how you can specify templates
        /// of land plots.
        /// </summary>
        private GameObject DefaultObjectsContainer { get; set; }

        /// <summary>
        /// The position of the land plot that is captured to determine camera distance measurements.
        /// </summary>
        Vector3 IMeasureCameraDistance.CameraMeasurementPosition => _transform.position;

        protected override void Awake()
        {
            base.Awake();

            // Cache our current transform.
            _transform = transform;

            // Assign the default container.
            FindDefaultContainer();
        }

        private void Start()
        {
            // Invoke the internal startup logic.
            StartInternal();

            // Try loading on start if possible.
            TryLoadOnStart();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            loadRequirements.Validate();
        }

        private void OnDrawGizmosSelected() => loadRequirements.DrawGizmos(transform);

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            base.OnDestroy();
            CancelLoad();
            CancelSaveInternal();
            CameraDistanceManager.Instance.RemoveMeasurer(this);
            if (_currentMetaSpace != null)
                _currentMetaSpace.Initialized -= Load;
            OnDestroyInternal();
        }

        void IMeasureCameraDistance.OnCameraDistance(Camera cam, float sqrDistance)
        {
            // If we're not enabled, no need to do anything.
            if (!isActiveAndEnabled) return;

            // Get the distance in meters.
            var distance = Mathf.Sqrt(sqrDistance);

            switch (IsLoaded)
            {
                // Determine whether we should or shouldn't load and perform the
                // load or unload based on that.
                case false when !IsLoading && loadRequirements.loadRange.ShouldLoad(distance):
                    Load();
                    break;
                case true or true when loadRequirements.loadRange.ShouldUnload(distance):
                    Unload();
                    break;
            }
        }

        /// <summary>
        /// Cancel the current loading operation.
        /// </summary>
        public void CancelLoad() => CancelLoadInternal();

        /// <summary>
        /// Begin loading the land plot.
        /// </summary>
        public void Load() => LoadInternal();

        /// <summary>
        /// Completely unload the land plot.
        /// </summary>
        public void Unload() => UnloadInternal();

        /// <summary>
        /// Saves the data inside of this land plot.
        /// </summary>
        public void Save() => SaveInternal();

        /// <summary>
        /// Start the build mode system.
        /// </summary>
        public void StartBuilding() => StartBuildingInternal();

        /// <summary>
        /// Stop the build mode system.
        /// </summary>
        public void StopBuilding() => StopBuildingInternal();

        /// <summary>
        /// Assigns the default container containing the template
        /// objects for the land plot.
        /// </summary>
        private void FindDefaultContainer()
        {
            var defaultContainer = transform.Find("Default");
            if (defaultContainer)
                (DefaultObjectsContainer = defaultContainer.gameObject).SetActive(false);
        }

        /// <summary>
        /// Performs logic to determine whether the land plot
        /// is able to load at the start of the scene.
        /// </summary>
        private void TryLoadOnStart()
        {
            // Make sure we don't load on start if we're
            // measuring camera distance. The camera distance
            // measurment logic will trigger the load/unload.
            if (loadRequirements.loadRange.loadDistance > 0)
            {
                CameraDistanceManager.Instance.AddMeasurer(this);
                return;
            }

            // If we don't want to load on start, then
            // just bail.
            if (!loadRequirements.loadOnStart) return;

            if (MetaSpace.Instance)
            {
                // Wait until the meta space is initialized
                // before loading.
                if (MetaSpace.Instance.IsInitialized) Load();
                else
                {
                    _currentMetaSpace = MetaSpace.Instance;
                    MetaSpace.Instance.Initialized += Load;
                }
            }
            // If there's no meta space in the scene, just load.
            else Load();
        }

        partial void StartInternal();
        partial void OnDestroyInternal();
        partial void LoadInternal();
        partial void UnloadInternal();
        partial void SaveInternal();
        partial void CancelSaveInternal();
        partial void CancelLoadInternal();
        partial void StartBuildingInternal();
        partial void StopBuildingInternal();
        partial void CanBuildInternal(ref bool canBuild);
    }
}
