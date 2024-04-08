using MetaverseCloudEngine.Common.Enumerations;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    /// <summary>
    /// A helper class that adds the ability to modify the Quality Level of the
    /// application.
    /// </summary>
    [HideMonoScript]
    public class QualitySettingsAPI : TriInspectorMonoBehaviour
    {
        public enum AntiAliasing
        {
            Disabled = 0,
            _2x = 2,
            _4x = 4,
            _8x = 8,
            _16x = 16
        }
        
        public enum MaxTextureSize
        {
            FullResolution = 0,
            HalfResolution = 1,
            QuarterResolution = 2,
            EighthResolution = 3,
        }
        
        [Header("Behavior")]
        [SerializeField] private bool applyOnStart = true;
        [ShowIf(nameof(applyOnStart))]
        [SerializeField] private Platform applyOnPlatform = Platform.All;
        
        [Title("Shadows")]
        [SerializeField] private ShadowQuality shadowQuality = ShadowQuality.All;
        [SerializeField] private ShadowResolution shadowResolution = ShadowResolution.High;
        [Min(0.0f)]
        [SerializeField] private float maxShadowDistance = 150.0f;
        
        [Title("Anti-Aliasing")]
        [SerializeField] private AntiAliasing antiAliasing = AntiAliasing._2x;
        
        [Title("Texture Quality")]
        [SerializeField] private AnisotropicFiltering anisotropicFiltering = AnisotropicFiltering.Disable;
        [SerializeField] private MaxTextureSize globalTextureLimit = MaxTextureSize.FullResolution;
        
        [Title("Particles")]
        [SerializeField] private bool softParticles = true;
        [Range(0, 4096)]
        [SerializeField] private int particleRaycastBudget = 2048;
        
        [Title("Terrain Quality")]
        [SerializeField] private bool billboardsFaceCameraPosition = true;
        [SerializeField] private bool useLegacyDetailDistribution = true;
        [SerializeField] private bool overridePixelError;
        [EnableIf(nameof(overridePixelError))]
        [SerializeField] private int pixelError = 1;
        [SerializeField] private bool overrideBaseMapDist;
        [EnableIf(nameof(overrideBaseMapDist))]
        [SerializeField] private int baseMapDist = 1000;
        [SerializeField] private bool overrideDetailDensityScale;
        [EnableIf(nameof(overrideDetailDensityScale))]
        [Range(0, 1f)]
        [SerializeField] private float detailDensityScale = 1.0f;
        [SerializeField] private bool overrideDetailDistance;
        [EnableIf(nameof(overrideDetailDistance))]
        [SerializeField] private int detailDistance = 80;
        [SerializeField] private bool overrideTreeDistance;
        [EnableIf(nameof(overrideTreeDistance))]
        [SerializeField] private int treeDistance = 5000;
        [SerializeField] private bool overrideMaxMeshTrees;
        [EnableIf(nameof(overrideMaxMeshTrees))]
        [SerializeField] private int maxMeshTrees = 50;
        [SerializeField] private bool overrideBillboardStart;
        [EnableIf(nameof(overrideBillboardStart))]
        [SerializeField] private int billboardStart = 50;
        [SerializeField] private bool overrideFadeLength;
        [EnableIf(nameof(overrideFadeLength))]
        [SerializeField] private int fadeLength = 5;
        
        [Title("Lighting")]
        [Range(0, 8)]
        [SerializeField] private int pixelLightCount = 4;
        [SerializeField] private bool realtimeReflectionProbes = true;

        [Title("Models")] 
        [Range(0, 5)]
        [SerializeField] private float lodBias = 1.0f;
        [Range(0, 5)]
        [SerializeField] private int maximumLODLevel;
        [SerializeField] private SkinWeights skinWeights = SkinWeights.TwoBones;
        
        private bool _hasStarted;
        
        private void Start()
        {
            _hasStarted = true;
            if (!applyOnStart)
                return;
            if (applyOnPlatform.HasFlag(MetaverseProgram.GetCurrentPlatform(allowSimulation: true)))
                Apply();
        }

        public void Apply()
        {
            if (!isActiveAndEnabled || (applyOnStart && !_hasStarted))
                return;

            QualitySettings.shadows = shadowQuality;
            QualitySettings.shadowResolution = shadowResolution;
            QualitySettings.shadowDistance = maxShadowDistance;
            QualitySettings.antiAliasing = (int)antiAliasing;
            QualitySettings.anisotropicFiltering = anisotropicFiltering;
            QualitySettings.pixelLightCount = pixelLightCount;
            QualitySettings.realtimeReflectionProbes = realtimeReflectionProbes;
            QualitySettings.lodBias = lodBias;
            QualitySettings.skinWeights = skinWeights;
            QualitySettings.softParticles = softParticles;
            QualitySettings.particleRaycastBudget = particleRaycastBudget;
            QualitySettings.billboardsFaceCameraPosition = billboardsFaceCameraPosition;
            QualitySettings.useLegacyDetailDistribution = useLegacyDetailDistribution;
            QualitySettings.maximumLODLevel = maximumLODLevel;
            QualitySettings.globalTextureMipmapLimit = (int)globalTextureLimit;
            
            if (overridePixelError) QualitySettings.terrainPixelError = pixelError;
            if (overrideBaseMapDist) QualitySettings.terrainBasemapDistance = baseMapDist;
            if (overrideDetailDensityScale) QualitySettings.terrainDetailDensityScale = detailDensityScale;
            if (overrideDetailDistance) QualitySettings.terrainDetailDistance = detailDistance;
            if (overrideTreeDistance) QualitySettings.terrainDetailDistance = treeDistance;
            if (overrideMaxMeshTrees) QualitySettings.terrainMaxTrees = maxMeshTrees;
            if (overrideBillboardStart) QualitySettings.terrainBillboardStart = billboardStart;
            if (overrideFadeLength) QualitySettings.terrainFadeLength = fadeLength;
            if (overrideTreeDistance) QualitySettings.terrainTreeDistance = treeDistance;
            
            QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), true);
        }
    }
}