using System.Linq;
using MetaverseCloudEngine.Unity.Async;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

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
            
                FindObjectsOfType<XRBaseInteractor>()
                    .ForEach(x => x.enabled = false)
                    .ForEach(x => x.enabled = true);
            
                FindObjectsOfType<XRBaseInteractable>()
                    .ForEach(x => x.enabled = false)
                    .ForEach(x => x.enabled = true);
            });
        }
    }
}