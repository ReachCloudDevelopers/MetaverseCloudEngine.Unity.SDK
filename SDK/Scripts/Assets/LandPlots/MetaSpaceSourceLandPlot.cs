using MetaverseCloudEngine.Unity.Attributes;
using MetaverseCloudEngine.Unity.Components;

using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.Assets.LandPlots
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LandPlot))]
    [DefaultExecutionOrder(ExecutionOrder.PreInitialization)]
    public partial class MetaSpaceSourceLandPlot : MetaSpaceBehaviour
    {
        public bool loadOnIDFound = true;
        public UnityEvent onIDFound;
        public UnityEvent onIDNotFound;
        
        private LandPlot _landPlot;
        public LandPlot LandPlot
        {
            get
            {
                if (_landPlot == null)
                    _landPlot = GetComponent<LandPlot>();
                return _landPlot;
            }
        }

        public static MetaSpaceSourceLandPlot Instance;

        protected override void Awake()
        {
            Instance = this;
            ConfigureLandPlot();
            base.Awake();
        }

        private void OnValidate()
        {
            ConfigureLandPlot();
        }

        private void ConfigureLandPlot()
        {
            LandPlot.loadRequirements.loadOnStart = false;
            LandPlot.ID = null;
        }
    }
}