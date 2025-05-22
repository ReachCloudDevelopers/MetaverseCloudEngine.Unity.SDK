//#define USE_CONDITIONAL_APPROACH // Comment this line to use the legacy approach instead of the conditional one.
//#define ENABLE_LEGACY_FIRST // Comment this line to disable the legacy renderer after enabling the new one.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MetaverseCloudEngine.Unity.Assets.MetaPrefabs;
using MetaverseCloudEngine.Unity.Async;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace MetaverseCloudEngine.Unity.Rendering.Components
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    [AddComponentMenu(MetaverseConstants.ProductName + "/Rendering/Render Pipeline API")]
    public class RenderPipelineAPI : TriInspectorMonoBehaviour
    {
        [SerializeField] private RenderPipelineAsset renderPipeline;
        [SerializeField] private bool setOnStart = true;

        private Scene _setInScene;
        private static bool _isFirstTime = true;

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer)
                return;
            if (setOnStart && !Application.isPlaying && !MetaverseProgram.IsBuildingAssetBundle)
                GraphicsSettings.defaultRenderPipeline = renderPipeline;
#endif
        }

        private void Start()
        {
            if (!setOnStart)
                return;

            if (SceneManager.GetActiveScene() == gameObject.scene)
                Set(renderPipeline);
            else
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene old, Scene scene)
        {
            if (!setOnStart)
                return;

            if (scene != gameObject.scene)
                return;

            Set(renderPipeline);
        }

        public void Set()
        {
            Set(renderPipeline);
        }

        public void SetNull()
        {
            Set(null);
        }

        public void Set(RenderPipelineAsset pipeline)
        {
            if (_setInScene == gameObject.scene)
                return;

            if (GraphicsSettings.defaultRenderPipeline == pipeline)
                return;

            MetaverseProgram.Logger.Log("[RenderPipelineAPI] Setting the render pipeline to " + ((pipeline ? pipeline.name : null) ?? "Standard") + " in scene " + gameObject.scene.name + ".");
            SwitchRenderPipeline(pipeline, () =>
            {
                MetaverseProgram.Logger.Log("[RenderPipelineAPI] Render pipeline set to " + ((pipeline ? pipeline.name : null) ?? "Standard") + " in scene " + gameObject.scene.name + ".");
            });
            _setInScene = gameObject.scene;
        }
        
        public static void SwitchToBuiltInRenderPipeline(Action onComplete = null)
        {
            MetaverseProgram.Logger.Log("[RenderPipelineAPI] Clearing the render pipeline.");
            SwitchRenderPipeline(null, onComplete);
        }

        private static void SwitchRenderPipeline([CanBeNull] RenderPipelineAsset pipeline, Action onComplete = null)
        {
            MetaverseDispatcher.Instance.StartCoroutine(AfterUpdate(() =>
            {
                using var _ = NativeLoadingOverlay.Create();

                if (GraphicsSettings.defaultRenderPipeline == pipeline)
                {
                    MetaverseProgram.Logger.LogWarning(
                        $"[RenderPipelineAPI] Default render pipeline update skipped because it was already {(pipeline ? pipeline.name : "null")}");
                    onComplete?.Invoke();
                    return;
                }
            
                // This is a heavy task, so we'll black out the screen while it's happening to avoid
                // people getting sick from the flickering.
#if ENABLE_LEGACY_FIRST
#if USE_CONDITIONAL_APPROACH
                var enabledLegacyRenderer =
#endif
                    EnableLegacyRendering(pipeline);
#if USE_CONDITIONAL_APPROACH
                if (!enabledLegacyRenderer)
#endif
#endif
                    GraphicsSettings.defaultRenderPipeline = pipeline;
#if !ENABLE_LEGACY_FIRST
                EnableLegacyRendering(pipeline);
#endif
                MetaPrefab.OnRenderPipelineChanged();
            
                MetaverseDispatcher.AtEndOfFrame(() =>
                {
                    QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), true);
                    
                    if (MVUtils.IsMobileVR())
                        QualitySettings.antiAliasing = MetaverseConstants.XR.DefaultAntiAliasing;
                });
            
                onComplete?.Invoke();
            }));
        }
        
        private static IEnumerator AfterUpdate(Action onComplete = null)
        {
            if (_isFirstTime)
            {
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                _isFirstTime = false;
            }
            
            onComplete?.Invoke();
        }

        private static bool EnableLegacyRendering([CanBeNull] RenderPipelineAsset pipeline)
        {
            if (pipeline) 
                return false;
            
            var enabledLegacyRenderer = false;
            var displaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displaySubsystems);
            MetaverseProgram.Logger.Log("[RenderPipelineAPI] Got XR display subsystems: " + displaySubsystems.Count);
            foreach (var displaySubsystem in displaySubsystems.Where(displaySubsystem => displaySubsystem.disableLegacyRenderer))
            {
                MetaverseProgram.Logger.Log("[RenderPipelineAPI] Re-enabled legacy renderer for XR display subsystem.");
                displaySubsystem.disableLegacyRenderer = false;
                enabledLegacyRenderer = true;
            }

            return enabledLegacyRenderer;
        }
    }
}