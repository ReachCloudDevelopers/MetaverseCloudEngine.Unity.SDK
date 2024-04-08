using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    /// <summary>
    /// Adds the ability to use lightmaps that are isolated to prefab instances.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Rendering/Prefab Lightmap Data")]
    [HideMonoScript]
    public class PrefabLightmapData : TriInspectorMonoBehaviour
    {
        [System.Serializable]
        private struct RendererInfo
        {
            public Renderer renderer;
            public int lightmapIndex;
            public Vector4 lightmapOffsetScale;
        }

        [System.Serializable]
        private struct LightInfo
        {
            public Light light;
            public int lightmapBakeType;
            public int mixedLightingMode;
        }

        [SerializeField, ReadOnly] private RendererInfo[] rendererInfo;
        [SerializeField, ReadOnly] private Texture2D[] lightmaps;
        [SerializeField, ReadOnly] private Texture2D[] lightmapsDir;
        [SerializeField, ReadOnly] private Texture2D[] shadowMasks;
        [SerializeField, ReadOnly] private LightInfo[] lightInfo;
        
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                InitializeLightmaps();
            }
            else
            {
                MetaverseDispatcher.AtEndOfFrame(InitializeLightmaps);
            }

            if (Application.isPlaying)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void InitializeLightmaps()
        {
            if (rendererInfo == null || rendererInfo.Length == 0)
                return;

            var lightMaps = LightmapSettings.lightmaps;
            var offsetsIndexes = new int[lightmaps.Length];
            var countTotal = lightMaps.Length;
            var combinedLightmaps = new List<LightmapData>();

            for (var i = 0; i < lightmaps.Length; i++)
            {
                var exists = false;
                for (var j = 0; j < lightMaps.Length; j++)
                {
                    if (lightmaps[i] != lightMaps[j].lightmapColor)
                        continue;

                    exists = true;
                    offsetsIndexes[i] = j;
                }

                if (exists)
                    continue;

                offsetsIndexes[i] = countTotal;
                var newLightmapData = new LightmapData
                {
                    lightmapColor = this.lightmaps[i],
                    lightmapDir = lightmapsDir.Length == this.lightmaps.Length ? lightmapsDir[i] : default,
                    shadowMask = shadowMasks.Length == this.lightmaps.Length ? shadowMasks[i] : default,
                };

                combinedLightmaps.Add(newLightmapData);

                countTotal += 1;
            }

            var combinedLightmaps2 = new LightmapData[countTotal];

            lightMaps.CopyTo(combinedLightmaps2, 0);
            combinedLightmaps.ToArray().CopyTo(combinedLightmaps2, lightMaps.Length);

            var directional = lightmapsDir.All(t => t != null);
            LightmapSettings.lightmapsMode = lightmapsDir.Length == this.lightmaps.Length && directional
                ? LightmapsMode.CombinedDirectional
                : LightmapsMode.NonDirectional;

            ApplyRendererInfo(rendererInfo, offsetsIndexes, lightInfo);
            LightmapSettings.lightmaps = combinedLightmaps2;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InitializeLightmaps();
        }

        private static void ApplyRendererInfo(IEnumerable<RendererInfo> infos, IReadOnlyList<int> lightmapOffsetIndex, IList<LightInfo> lightsInfo)
        {
            foreach (var info in infos)
            {
                if (!info.renderer)
                    continue;

                info.renderer.lightmapIndex = lightmapOffsetIndex[info.lightmapIndex];
                info.renderer.lightmapScaleOffset = info.lightmapOffsetScale;

                // You have to release shaders.
                var mat = info.renderer.sharedMaterials;
                foreach (var t in mat)
                {
                    if (t != null && Shader.Find(t.shader.name) != null)
                        t.shader = Shader.Find(t.shader.name);
                }
            }

            for (var i = 0; i < lightsInfo.Count; i++)
            {
                var bakingOutput = new LightBakingOutput
                {
                    isBaked = true,
                    lightmapBakeType = (LightmapBakeType) lightsInfo[i].lightmapBakeType,
                    mixedLightingMode = (MixedLightingMode) lightsInfo[i].mixedLightingMode
                };

                if (lightsInfo[i].light)
                {
                    lightsInfo[i].light.bakingOutput = bakingOutput;
                }
            }
        }

#if UNITY_EDITOR
        [Button("Bake All"), HideIf(nameof(HideBakeAllButton)), InfoBox("This will bake all prefabs in the current scene.")]
        private void BakeAll() => GenerateLightmapInfo();

        [Button("Clear Baked Data"), ShowIf(nameof(HasBakedData))]
        private void ClearBakedData()
        {
            rendererInfo = null;
            lightmaps = null;
            lightmapsDir = null;
            shadowMasks = null;
            lightInfo = null;
            
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private bool HasBakedData()
        {
            return 
                rendererInfo is { Length: > 0 } || 
                lightmaps is { Length: > 0 } ||
                lightmapsDir is { Length: > 0 } ||
                shadowMasks is { Length: > 0 } ||
                lightInfo is { Length: > 0 };
        }

        /// <summary>
        /// Whether or not the Bake All button should be hidden.
        /// </summary>
        /// <returns>True if the object is part of a scene and the game is not running.</returns>
        private bool HideBakeAllButton() => !gameObject.scene.IsValid() || Application.isPlaying;
        
        [UnityEditor.MenuItem("Assets/Bake Prefab Lightmaps (In Current Scene)")]
        private static void GenerateLightmapInfo()
        {
            if (UnityEditor.Lightmapping.giWorkflowMode != UnityEditor.Lightmapping.GIWorkflowMode.OnDemand)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Disable Auto-Baking",
                    "Please disable Auto-Generate Lighting Before Baking Prefab Lightmaps", 
                    "Ok");
                return;
            }
            
            UnityEditor.Lightmapping.Bake();

            var prefabs = MVUtils.FindObjectsOfTypeNonPrefabPooled<PrefabLightmapData>(true);
            if (prefabs.Length == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "No Prefabs Found",
                    "There are no game objects with 'Prefab Lightmap Data' in the scene.",
                    "Ok");
                return;
            }
            
            int savedPrefabCount = 0;
            
            foreach (var instance in prefabs)
            {
                var gameObject = instance.gameObject;
                var rendererInfos = new List<RendererInfo>();
                var lightmaps = new List<Texture2D>();
                var lightmapsDir = new List<Texture2D>();
                var shadowMasks = new List<Texture2D>();
                var lightsInfos = new List<LightInfo>();

                GenerateLightmapInfo(gameObject, rendererInfos, lightmaps, lightmapsDir, shadowMasks, lightsInfos);

                instance.rendererInfo = rendererInfos.ToArray();
                instance.lightmaps = lightmaps.ToArray();
                instance.lightmapsDir = lightmapsDir.ToArray();
                instance.lightInfo = lightsInfos.ToArray();
                instance.shadowMasks = shadowMasks.ToArray();
#if UNITY_2018_3_OR_NEWER
                var targetPrefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance.gameObject);
                if (targetPrefab == null)
                    continue;

                var root = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(instance.gameObject);
                if (root != null)
                {
                    var rootPrefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                    var rootPath = UnityEditor.AssetDatabase.GetAssetPath(rootPrefab);
                    UnityEditor.PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots(root, UnityEditor.PrefabUnpackMode.OutermostRoot);
                    try
                    {
                        UnityEditor.PrefabUtility.ApplyPrefabInstance(instance.gameObject, UnityEditor.InteractionMode.AutomatedAction);
                    }
                    catch
                    {
                        /* ignored */
                    }
                    finally
                    {
                        UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(root, rootPath, UnityEditor.InteractionMode.AutomatedAction);
                        savedPrefabCount++;
                    }
                }
                else
                {
                    UnityEditor.PrefabUtility.ApplyPrefabInstance(instance.gameObject, UnityEditor.InteractionMode.AutomatedAction);
                    savedPrefabCount++;
                }

                if (savedPrefabCount > 0)
                {
                    UnityEditor.EditorUtility.DisplayDialog(
                        "Applied Lightmaps",
                        $"Applied lightmaps to {savedPrefabCount} prefab(s).",
                        "Great!");
                }
                else
                {
                    UnityEditor.EditorUtility.DisplayDialog(
                        "Applying Lightmaps Failed",
                        "Failed to apply lightmaps to any prefabs. Check the console for any errors.",
                        "Ok");
                }
#else
                var targetPrefab = UnityEditor.PrefabUtility.GetPrefabParent(gameObject) as GameObject;
                if (targetPrefab != null)
                {
                    //UnityEditor.Prefab
                    UnityEditor.PrefabUtility.ReplacePrefab(gameObject, targetPrefab);
                }
#endif
            }
        }

        private static void GenerateLightmapInfo(
            GameObject root,
            ICollection<RendererInfo> rendererInfos,
            IList<Texture2D> lightmaps,
            ICollection<Texture2D> lightmapsDir,
            ICollection<Texture2D> shadowMasks,
            ICollection<LightInfo> lightsInfo)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.lightmapIndex == -1)
                    continue;

                var info = new RendererInfo
                {
                    renderer = renderer
                };

                if (renderer.lightmapScaleOffset == Vector4.zero)
                    continue;

                //1ibrium's pointed out this issue : https://docs.unity3d.com/ScriptReference/Renderer-lightmapIndex.html
                if (renderer.lightmapIndex is < 0 or 0xFFFE) continue;
                info.lightmapOffsetScale = renderer.lightmapScaleOffset;

                var lightmapIndex = renderer.lightmapIndex;
                var lightmap = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
                var lightmapDir = LightmapSettings.lightmaps[lightmapIndex].lightmapDir;
                var shadowMask = LightmapSettings.lightmaps[lightmapIndex].shadowMask;

                info.lightmapIndex = lightmaps.IndexOf(lightmap);
                if (info.lightmapIndex == -1)
                {
                    info.lightmapIndex = lightmaps.Count;
                    lightmaps.Add(lightmap);
                    lightmapsDir.Add(lightmapDir);
                    shadowMasks.Add(shadowMask);
                }

                rendererInfos.Add(info);
            }

            var lights = root.GetComponentsInChildren<Light>(true);

            foreach (var l in lights)
            {
                var lightInfo = new LightInfo
                {
                    light = l,
                    lightmapBakeType = (int) l.lightmapBakeType
                };
#if UNITY_2020_1_OR_NEWER
                lightInfo.mixedLightingMode = (int) UnityEditor.Lightmapping.lightingSettings.mixedBakeMode;
#elif UNITY_2018_1_OR_NEWER
                lightInfo.mixedLightingMode = (int)UnityEditor.LightmapEditorSettings.mixedBakeMode;
#else
                lightInfo.mixedLightingMode = (int)l.bakingOutput.lightmapBakeType;
#endif
                lightsInfo.Add(lightInfo);
            }
        }
#endif
    }
}