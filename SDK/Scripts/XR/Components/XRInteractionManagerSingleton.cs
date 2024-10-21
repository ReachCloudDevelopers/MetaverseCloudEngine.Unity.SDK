using System.Linq;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

#if MV_XR_TOOLKIT_3
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
using XRBaseInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
#else
using XRBaseInteractable = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable;
using XRBaseInteractor = UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor;
#endif

namespace MetaverseCloudEngine.Unity.XR.Components
{
    /// <summary>
    /// This script destroys all XRInteractionManager instances in newly
    /// loaded scenes to prevent duplicate instances from existing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class XRInteractionManagerSingleton : MonoBehaviour
    {
        private static XRInteractionManager _singletonManager;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            new GameObject($"[{nameof(XRInteractionManagerSingleton)}]").AddComponent<XRInteractionManagerSingleton>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _singletonManager = gameObject.AddComponent<XRInteractionManager>();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshInteractions(scene);

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene) => RefreshInteractions(newScene);

        private static void RefreshInteractions(Scene scene)
        {
            var managers =
                scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<XRInteractionManager>(true));
            foreach (var manager in managers)
            {
                if (manager == _singletonManager)
                    continue;
                Destroy(manager);
            }
            
            MetaverseDispatcher.WaitForSeconds(0.1f, () =>
            {
                if (_singletonManager)
                {
                    _singletonManager.enabled = false;
                    _singletonManager.enabled = true;
                }

                #if UNITY_6000_0_OR_NEWER
                FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None)
                    .ForEach(x => x.enabled = false)
                    .ForEach(x => x.enabled = true);
            
                FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None)
                    .ForEach(x => x.enabled = false)
                    .ForEach(x => x.enabled = true);
                #else
                FindObjectsOfType<XRBaseInteractor>()
                    .ForEach(x => x.enabled = false)
                    .ForEach(x => x.enabled = true);
            
                FindObjectsOfType<XRBaseInteractable>()
                    .ForEach(x => x.enabled = false)
                    .ForEach(x => x.enabled = true);
                #endif
            });
        }
    }
}