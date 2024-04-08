using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MetaverseCloudEngine.Unity.SceneManagement.Components
{
    public class LoadScene : MonoBehaviour
    {
        private static int _loadCount;
        private static int _unloadCount;

        [System.Serializable]
        public class Events
        {
            [Header("Load")]
            public UnityEvent onBeginLoad;
            public UnityEvent onFinishLoad;

            [Header("Unload")]
            public UnityEvent onBeginUnload;
            public UnityEvent onFinishUnload;
        }

        public Events events;

        private void Start()
        {
            if (_loadCount > 0) events.onBeginLoad?.Invoke();
            if (_unloadCount > 0) events.onBeginUnload?.Invoke();
        }

        public void LoadSceneAdditive(int id)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(id, LoadSceneMode.Additive);
            operation.completed += OnLoadCompleted;

            if (_loadCount == 0)
                TriggerLoadEvents();
            _loadCount++;
        }

        public void LoadSceneAdditive(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            operation.completed += OnLoadCompleted;

            if (_loadCount == 0)
                TriggerLoadEvents();
            _loadCount++;
        }

        private static void TriggerLoadEvents()
        {
            SceneLoadHelper[] sceneHelpers = FindObjectsOfType<SceneLoadHelper>();
            foreach (SceneLoadHelper helper in sceneHelpers)
                if (helper) helper.events.onBeginLoad?.Invoke();
        }

        private static void OnLoadCompleted(AsyncOperation operation)
        {
            _loadCount--;
            if (_loadCount != 0) return;
            SceneLoadHelper[] sceneHelpers = FindObjectsOfType<SceneLoadHelper>();
            foreach (SceneLoadHelper helper in sceneHelpers)
                if (helper) helper.events.onFinishLoad?.Invoke();
        }

        public void UnloadScene(int id)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(id);
            operation.completed += OnUnloadCompleted;

            if (_unloadCount == 0)
                TriggerUnloadEvents();
            _unloadCount++;
        }

        public void UnloadScene(string sceneName)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);
            operation.completed += OnUnloadCompleted;

            if (_unloadCount == 0)
                TriggerUnloadEvents();
            _unloadCount++;
        }

        private static void TriggerUnloadEvents()
        {
            SceneLoadHelper[] sceneHelpers = FindObjectsOfType<SceneLoadHelper>();
            foreach (SceneLoadHelper helper in sceneHelpers)
                if (helper) helper.events.onFinishUnload?.Invoke();
        }

        private static void OnUnloadCompleted(AsyncOperation operation)
        {
            _unloadCount--;
            if (_unloadCount != 0) return;
            SceneLoadHelper[] sceneHelpers = FindObjectsOfType<SceneLoadHelper>();
            foreach (SceneLoadHelper helper in sceneHelpers)
                if (helper) helper.events.onFinishUnload?.Invoke();
        }
    }
}