using System.Collections.Generic;
using System.Linq;
using MetaverseCloudEngine.Common.Enumerations;
using MetaverseCloudEngine.Common.Models.DataTransfer;
using MetaverseCloudEngine.Unity.Rendering.Components;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.Assets.MetaPrefabs
{
    /// <summary>
    /// A type of <see cref="Asset{TMetaData}"/> that represents a prefab within the Metaverse. A
    /// prefab is a piece of content that can be spawned into the world.
    /// </summary>
    [HideMonoScript]
    [HelpURL(
        "https://reach-cloud.gitbook.io/reach-explorer-documentation/docs/development-guide/unity-engine-sdk/components/assets/meta-prefab")]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Metaverse Assets/Prefabs/Meta Prefab")]
    public class MetaPrefab : Asset<MetaPrefabMetadata>
    {
        [SerializeField, HideInInspector] private bool scriptableRenderPipelineSupported;

        private bool _registered;
        private List<Material> _createdMaterials;

        /// <summary>
        /// The spawner that spawned this prefab. This is set by the spawner.
        /// </summary>
        public MetaPrefabSpawner Spawner { get; set; }

        /// <summary>
        /// Whether or not this prefab is supported by the scriptable render pipeline.
        /// </summary>
        public bool UsesScriptableRenderPipeline => scriptableRenderPipelineSupported;

        private static Material _standardPipelineFallbackMaterial;
        private static Material _universalPipelineFallbackMaterial;
        private static Material _standardTransparentPipelineFallbackMaterial;
        private static Material _universalTransparentPipelineFallbackMaterial;
        private Dictionary<Renderer, Material[]> _originalMaterialMap;

        private bool _appliedToChildren;
        private static readonly int ModeShaderPropertyID = Shader.PropertyToID("_Mode");
        private static readonly int SurfaceShaderPropertyIndex = Shader.PropertyToID("_Surface");

#if UNITY_EDITOR
        public static void PreProcessBuild()
        {
            var metaPrefabs = UnityEditor.AssetDatabase.FindAssets("t:GameObject")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(x => x && x.GetComponentsInChildren<MetaPrefab>(true).Length > 0)
                .SelectMany(x => x.GetComponentsInChildren<MetaPrefab>(true))
                .ToArray();

            foreach (var metaPrefab in metaPrefabs)
                metaPrefab.scriptableRenderPipelineSupported = 
#if MV_RENDER_PIPELINE_17
                    GraphicsSettings.defaultRenderPipeline;
#else
                    GraphicsSettings.renderPipelineAsset;
#endif
        }
#endif

        protected override void Reset()
        {
            base.Reset();
            MetaData.Listings = AssetListings.Unlisted;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (!this)
                return;
            
            if (gameObject.name != MetaData.Name)
                MetaData.Name = gameObject.name;

#if UNITY_EDITOR
            if (!Application.isPlaying && MetaverseProgram.IsBuildingAssetBundle)
            {
                var renderPipelineSupported = GraphicsSettings.defaultRenderPipeline != null && !MetaverseProgram.IsCoreApp;
                if (scriptableRenderPipelineSupported != renderPipelineSupported)
                    scriptableRenderPipelineSupported = renderPipelineSupported;
            }
#endif
        }

        protected virtual void Start()
        {
            Allocate();
        }

        protected override void OnDestroy()
        {
            if (MetaverseProgram.IsQuitting) return;
            base.OnDestroy();
            if (ID is not null && _registered)
                MetaPrefabLoadingAPI.UnRegisterPrefabInstance(ID.Value);
            DisposeCreatedMaterials();
        }

        private void DisposeCreatedMaterials()
        {
            if (_createdMaterials is not null)
                foreach (var material in _createdMaterials.Where(material => material))
                    if (material) Destroy(material);
            _createdMaterials = null;
        }

        private void OnDrawGizmosSelected()
        {
            MetaData.loadRange.DrawGizmos(transform);
        }

        protected override void UpdateFromDtoInternal(MetaPrefabMetadata md, AssetDto dto)
        {
            base.UpdateFromDtoInternal(md, dto);

            if (dto is not PrefabDto pfDto)
                return;

            md.loadRange = new ObjectLoadRange
            {
                loadDistance = pfDto.PrefabLoadDistance,
                unloadDistance = pfDto.PrefabUnloadDistance,
            };
            md.builderCategories = pfDto.PrefabBuildingCategory;
            md.isAvatar = pfDto.IsAvatar;
            md.isBuildable = pfDto.IsBuildable;
        }

        public static void OnRenderPipelineChanged()
        {
            var prefabs = Resources.FindObjectsOfTypeAll<MetaPrefab>();
            MetaverseProgram.Logger.Log(
                $"Checking {prefabs.Length} prefabs for render pipeline upgrade-ability: {string.Join(", ", prefabs.Select(x => x.name))}");
            foreach (var prefab in prefabs)
                if (prefab)
                    prefab.CheckRenderPipeline();
        }

        public void Allocate()
        {
            if (ID is null)
            {
                return;
            }
            
            MetaPrefabLoadingAPI.RegisterPrefabInstance(ID.Value);
            _registered = true;
        }

        public bool CheckRenderPipeline(bool addMapperToChildren = false)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(gameObject))
            {
                return false;
            }
#endif

            MetaPrefabSrpMaterialReMapper rm = null;
            if (!_appliedToChildren && addMapperToChildren && TryGetComponent(out rm))
            {
                var children = GetComponentsInChildren<MetaPrefab>(true);
                for (var index = children.Length - 1; index >= 0; index--)
                {
                    var child = children[index];
                    if (child == this) continue;
                    if (child.TryGetComponent(out MetaPrefabSrpMaterialReMapper childRm)) continue;
                    childRm = child.gameObject.AddComponent<MetaPrefabSrpMaterialReMapper>();
                    childRm.SetMap(rm.GetMap());
                }
            }

            _appliedToChildren = true;

            var isScriptablePipeline = (bool)GraphicsSettings.defaultRenderPipeline;
            var useUnsupportedMaterial =
                (isScriptablePipeline && !UsesScriptableRenderPipeline) ||
                (!isScriptablePipeline && UsesScriptableRenderPipeline);

            if (!useUnsupportedMaterial)
            {
                RevertMaterials();
                return true;
            }

            _standardPipelineFallbackMaterial = _standardPipelineFallbackMaterial
                ? _standardPipelineFallbackMaterial
                : Resources.Load<Material>("Includes/Materials/Standard");
            _universalPipelineFallbackMaterial = _universalPipelineFallbackMaterial
                ? _universalPipelineFallbackMaterial
                : Resources.Load<Material>("Includes/Materials/URP");

            _standardTransparentPipelineFallbackMaterial = _standardTransparentPipelineFallbackMaterial
                ? _standardTransparentPipelineFallbackMaterial
                : Resources.Load<Material>("Includes/Materials/StandardTransparent");
            _universalTransparentPipelineFallbackMaterial = _universalTransparentPipelineFallbackMaterial
                ? _universalTransparentPipelineFallbackMaterial
                : Resources.Load<Material>("Includes/Materials/URPTransparent");

            var fallbackOpaqueMaterial = isScriptablePipeline
                ? _universalPipelineFallbackMaterial
                : _standardPipelineFallbackMaterial;
            var fallbackTransparentMaterial = isScriptablePipeline
                ? _universalTransparentPipelineFallbackMaterial
                : _standardTransparentPipelineFallbackMaterial;

            if (!fallbackOpaqueMaterial || !fallbackTransparentMaterial)
            {
                return false;
            }

            var reMappedShaders = new Dictionary<Material, Material>();
            if (rm || TryGetComponent(out rm))
            {
                reMappedShaders = rm.GetMap(isScriptablePipeline);
            }

            var renderers = gameObject.GetTopLevelComponentsInChildrenOrdered<Renderer, MetaPrefab>();
            for (var rendererIndex = renderers.Length - 1; rendererIndex >= 0; rendererIndex--)
            {
                var ren = renderers[rendererIndex];
                if (ren is not MeshRenderer and not SkinnedMeshRenderer)
                {
                    continue;
                }

                var mats = ren.materials;
                if (mats is null || mats.Length == 0)
                {
                    continue;
                }

                var modified = false;
                for (var matIndex = mats.Length - 1; matIndex >= 0; matIndex--)
                {
                    var originalMat = mats[matIndex];
                    if (!originalMat || !originalMat.shader)
                    {
                        continue;
                    }

                    Material newMat;
                    if (reMappedShaders.TryGetValue(originalMat, out var mappedMaterial) && mappedMaterial && mappedMaterial.shader)
                    {
                        newMat = new Material(mappedMaterial);
                        newMat.shader = Shader.Find(newMat.shader.name);
                        mats[matIndex] = newMat;
                        (_createdMaterials ??= new List<Material>()).Add(newMat);
                        modified = true;
                        continue;
                    }

                    bool isTransparent;
                    if (isScriptablePipeline)
                    {
                        isTransparent =
                            originalMat.HasProperty(ModeShaderPropertyID) &&
                            originalMat.GetFloat(ModeShaderPropertyID) >= 2;
                    }
                    else
                    {
                        isTransparent =
                            originalMat.HasProperty(SurfaceShaderPropertyIndex) &&
                            originalMat.GetInt(SurfaceShaderPropertyIndex) == 1;
                    }

                    newMat = new Material(isTransparent ? fallbackTransparentMaterial : fallbackOpaqueMaterial);
                    newMat.shader = Shader.Find(newMat.shader.name);
                    newMat.color = originalMat.color;

                    if (isScriptablePipeline && isTransparent)
                        newMat.renderQueue = (int)RenderQueue.Transparent + 1;

                    if (newMat.HasProperty("_MainTex") && originalMat.HasProperty("_MainTex"))
                        newMat.mainTexture = originalMat.mainTexture;

                    (_createdMaterials ??= new List<Material>()).Add(newMat);
                    mats[matIndex] = newMat;
                    modified = true;
                }

                if (!modified)
                {
                    continue;
                }

                _originalMaterialMap ??= new Dictionary<Renderer, Material[]>();
                _originalMaterialMap[ren] = ren.materials;
                ren.materials = mats;
            }

            return true;
        }

        private void RevertMaterials()
        {
            if (_originalMaterialMap is null)
            {
                return;
            }

            MetaverseProgram.Logger.Log($"Reverting materials for '{name}'.");

            foreach (var kvp in _originalMaterialMap.Where(kvp => kvp.Key))
            {
                /*foreach (var mat in kvp.Value)
                    if (mat && mat.shader)
                        mat.shader = Shader.Find(mat.shader.name);*/
                if (kvp.Key)
                    kvp.Key.materials = kvp.Value;
            }

            foreach (var mat in _createdMaterials.Where(mat => mat))
            {
                Destroy(mat);
            }
            
            _originalMaterialMap = null;
        }
    }
}