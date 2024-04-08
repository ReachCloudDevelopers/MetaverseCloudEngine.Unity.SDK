using UnityEngine;
using MetaverseCloudEngine.Unity.Assets.MetaSpaces;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.XR.Components.Rendering
{
    public class XRResolutionScaleHelper : MonoBehaviour
    {
        [Tooltip("Whether the resolution scale should be set on start.")]
        public bool setOnStart = true;
        [Tooltip("If true, will use a multiplier for the resolution scale based on the current XR device.")]
        public bool useDeviceMultiplier = true;
        [HideIf(nameof(useDeviceMultiplier))]
        [Tooltip("The resolution scale to use.")]
        [Range(0.1f, 2f)] [SerializeField] private float scale = 1f;

        private MetaSpace _metaSpace;

        public float Scale {
            get => scale;
            set => scale = value;
        }

        public bool WasApplied { get; private set; }

        private void Awake()
        {
            _metaSpace = MetaSpace.Instance;
        }

        private void Start()
        {
            if (setOnStart) Set();
        }

        public void Set()
        {
            if (!isActiveAndEnabled)
                return;

            if (!MVUtils.IsVRCompatible()) 
                return;
            
            if (useDeviceMultiplier)
            {
                MVUtils.SafelyAdjustXRResolutionScale(
                    MetaverseConstants.XR.DefaultXRResolutionScale * (_metaSpace ? MetaverseConstants.XR.DefaultInWorldResolutionMultiplier : 1f));   
            }
            else
            {
                MVUtils.SafelyAdjustXRResolutionScale(scale);
            }
                
            WasApplied = true;
        }

        public void Set(float value)
        {
            Scale = value;
            Set();
        }
    }
}
