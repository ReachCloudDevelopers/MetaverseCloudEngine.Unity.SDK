using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if MV_XR_TOOLKIT
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

namespace MetaverseCloudEngine.Unity.UI
{
    public static class EventSystemFallback
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            SceneManager.activeSceneChanged += OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateActiveScene();
        }

        private static void OnSceneLoaded(Scene oldScene, Scene newScene)
        {
            UpdateActiveScene();
        }

        private static void UpdateActiveScene()
        {
            var eventSystems = Object.FindObjectsOfType<EventSystem>(true);
            foreach (var eventSystem in eventSystems)
                Object.Destroy(eventSystem.gameObject);
            var go = new GameObject("[EventSystem]");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            go.AddComponent<EventSystem>();
            if (Application.platform == RuntimePlatform.WebGLPlayer) go.AddComponent<StandaloneInputModule>();
#if MV_XR_TOOLKIT
            else go.AddComponent<XRUIInputModule>();
#else
            else go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}