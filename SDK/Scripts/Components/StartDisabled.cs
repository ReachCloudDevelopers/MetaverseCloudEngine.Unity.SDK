using System;
using System.Linq;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A component which forces the game object to start in a disabled state.
    /// </summary>
    [DefaultExecutionOrder(-int.MaxValue)]
    [ExecuteAlways]
    [HierarchyIcon("animationvisibilitytoggleoff@2x")]
    [HideMonoScript]
    public class StartDisabled : TriInspectorMonoBehaviour
    {
        /// <summary>
        /// The mode to use when disabling the game object or components.
        /// </summary>
        [Flags]
        public enum StartDisabledMode
        {
            /// <summary>
            /// Do not disable the game object or any of components.
            /// </summary>
            None = 0,
            /// <summary>
            /// Disable the game object.
            /// </summary>
            DisableGameObject = 1,
            /// <summary>
            /// Disable the components specified in <see cref="StartDisabled.behavioursToDisable"/>.
            /// </summary>
            DisableBehaviours = 2,
        }
        
        [InfoBox("Use this component to force the game object or script(s) to start in a disabled state.")]
        [Tooltip("The mode to use when disabling the game object or components.")]
        [SerializeField] private StartDisabledMode mode = StartDisabledMode.DisableGameObject;
        [Tooltip("The behaviours to disable when the game object is started.")]
        [ShowIf(nameof(ShowBehavioursToDisable))]
        [SerializeField] private Behaviour[] behavioursToDisable;
        
#if UNITY_EDITOR
        private bool ShowBehavioursToDisable => mode.HasFlag(StartDisabledMode.DisableBehaviours);
        
        // On before enter play mode, disable the game object.
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnBeforeEnterPlayMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange stateChange)
        {
            switch (stateChange)
            {
                case UnityEditor.PlayModeStateChange.ExitingEditMode:
                {
                    foreach (var startDisabled in FindObjectsOfType<StartDisabled>(true))
                        startDisabled.Deactivate();
                    

                    break;
                }
            }
        }
        
        public static void PreProcessBuild()
        {
            // Loop through objects in all game objects in the project.
            var prefabs = UnityEditor.AssetDatabase.FindAssets("t:GameObject")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(x => x && x.GetComponentsInChildren<StartDisabled>(true).Length > 0)
                .SelectMany(x => x.GetComponentsInChildren<StartDisabled>(true))
                .ToArray();
            foreach (var prefab in prefabs)
                prefab.Deactivate();
            
            // Loop through objects in all scenes.
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var scene = SceneManager.GetSceneByBuildIndex(index);
                if (!scene.isLoaded)
                    continue;
                foreach (var startDisabled in scene.GetRootGameObjects()
                             .SelectMany(x => x.GetComponentsInChildren<StartDisabled>(true)))
                    startDisabled.Deactivate();
            }
        }
#endif

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer || MetaverseProgram.IsBuildingAssetBundle)
            {
                Deactivate();
            }
#endif
        }

        private void Deactivate()
        {
            if (mode.HasFlag(StartDisabledMode.DisableGameObject) && gameObject.activeSelf)
                gameObject.SetActive(false);
            if (mode.HasFlag(StartDisabledMode.DisableBehaviours))
                foreach (var component in behavioursToDisable)
                    if (component && component.enabled)
                        component.enabled = false;
        }
    }
}