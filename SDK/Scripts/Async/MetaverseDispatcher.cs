using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Async
{
    /// <summary>
    /// A dispatcher is a class that's meant to allow for asynchronous tasks
    /// to be run on the main thread. This is useful for scenarios where you
    /// need to run a task on the main thread, but you don't want to cause
    /// a block. It also helps to integrate asynchronous tasks with synchronous
    /// code.
    /// </summary>
    /// <example>
    /// <code>
    /// using System;
    /// using System.Threading;
    /// using System.Threading.Tasks;
    /// using MetaverseCloudEngine.Unity.Async;
    /// using UnityEngine;
    /// 
    /// public class ExampleClass : MonoBehaviour
    /// {
    ///     private void Start()
    ///     {
    ///         MetaverseDispatcher.Await(SomeAsyncTask(), () => Debug.Log("Task completed!"));
    ///     }
    /// }
    /// </code>
    /// </example>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-int.MaxValue)]
    public class MetaverseDispatcher : MonoBehaviour
    {
        /// <summary>
        /// This is a simple class that's meant to
        /// track wait until callbacks.
        /// </summary>
        private class DispatcherWaitUntilAction
        {
            public Func<bool> WaitHandler;
            public Action Success;
        }

        private static readonly ConcurrentQueue<Action> MainThreadQueue = new();
        private static readonly ConcurrentBag<Func<bool>> EditorTasksToUpdate = new();

        /// <summary>
        /// We pool all wait until actions in a list
        /// so as not to create a bunch of different
        /// coroutines which can be very memory and CPU
        /// intensive. This is just a much more efficient
        /// way.
        /// </summary>
        private static readonly List<DispatcherWaitUntilAction> WaitUntilActions = new();

        private static MetaverseDispatcher _instance;

        public static MetaverseDispatcher Instance
        {
            get
            {
                if (_instance == null)
                    _instance = MVUtils.FindObjectsOfTypeNonPrefabPooled<MetaverseDispatcher>(true).FirstOrDefault();
                if (_instance == null)
                    CreateInstance();
                return _instance;
            }
        }

        /// <summary>
        /// A helper property that determines if we should be using
        /// the UniTask threading system or not.
        /// </summary>
        public static bool UseUniTaskThreading
#if UNITY_EDITOR
        {
            get;
            private set;
        }
#else
        => true;
#endif

#if UNITY_EDITOR

        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorInit()
        {
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        [UnityEditor.InitializeOnEnterPlayMode]
        private static void PlayModeStateChanged() => UseUniTaskThreading = true;

        private static void OnEditorUpdate()
        {
            UseUniTaskThreading = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;

            Dequeue();

            UpdateEditorTasks();
        }

        private static void UpdateEditorTasks()
        {
            var count = EditorTasksToUpdate.Count;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    if (!EditorTasksToUpdate.TryTake(out var action))
                        continue;
                    if (action != null && !action())
                        EditorTasksToUpdate.Add(action);
                }
                catch (Exception e)
                {
                    MetaverseProgram.Logger.LogError(e);
                }
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            CreateInstance();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void FixedUpdate()
        {
            Dequeue();
        }

        private static void Dequeue()
        {
            var maximumIterationsPerFrame = 5;
            while (MainThreadQueue.Count > 0)
            {
                try
                {
                    if (MainThreadQueue.TryDequeue(out var item))
                    {
                        item?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    MetaverseProgram.Logger.LogError("Error in Dequeued Action: " + e);
                }
                
                if (Application.isPlaying && --maximumIterationsPerFrame <= 0)
                    break;
            }
        }

        private static void CreateInstance()
        {
#if UNITY_2023_1_OR_NEWER
            _instance = FindFirstObjectByType<MetaverseDispatcher>();
#else
            _instance = FindObjectOfType<MetaverseDispatcher>();
#endif
            if (_instance)
                return;

            _instance = new GameObject(nameof(MetaverseDispatcher)).AddComponent<MetaverseDispatcher>();
            _instance.gameObject.hideFlags = UseUniTaskThreading
                ? (HideFlags.HideInHierarchy | HideFlags.NotEditable)
                : HideFlags.HideAndDontSave;

            if (UseUniTaskThreading)
                DontDestroyOnLoad(_instance.gameObject);
        }

        private static IEnumerator WaitUntilEnumerator()
        {
            while (WaitUntilActions.Count > 0)
            {
                var maxIterationsPerFrame = 5;
                for (var i = WaitUntilActions.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var x = WaitUntilActions[i];
                        if (!x.WaitHandler()) continue;
                        x.Success?.Invoke();
                    }
                    catch(Exception e)
                    {
                        /* ignored */
                        Debug.LogException(e);
                    }
                    WaitUntilActions.RemoveAt(i);
                    
                    if (Application.isPlaying && --maxIterationsPerFrame <= 0)
                        break;
                }
                
                yield return null;
            }
        }

        private static IEnumerator AwaitTaskCoroutine(Task task, Action onSuccess, Action<object> onError)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsCanceled)
            {
                onError?.Invoke("Task was cancelled.");
                yield break;
            }

            if (task.IsFaulted) onError?.Invoke(task.Exception);
            else onSuccess?.Invoke();
        }

        private static IEnumerator AwaitEnumerator<T>(Task<T> task, Action<T> onSuccess, Action<object> onError)
        {
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsCanceled)
            {
                onError?.Invoke("Task was cancelled.");
                yield break;
            }

            if (task.IsFaulted) onError?.Invoke(task.Exception);
            else onSuccess?.Invoke(task.Result);
        }

        public static void Await<T>(Task<T> task, Action<T> onSuccess, Action<object> onError = null,
            CancellationToken? cancellationToken = null)
        {
            try
            {
                var cancel = cancellationToken.GetValueOrDefault();
                if (UseUniTaskThreading)
                {
                    MainThreadQueue.Enqueue(() => Instance.StartCoroutine(AwaitEnumerator(task, onSuccess, onError)));
                }
                else
                {
                    EditorTasksToUpdate.Add(() =>
                    {
                        if (!task.IsCompleted && !cancel.IsCancellationRequested) return false;
                        OnTaskCompleted(task, onSuccess, onError, cancel.IsCancellationRequested);
                        return true;
                    });
                }
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
        }

        public static void Await(Task task, Action onSuccess, Action<object> onError = null,
            CancellationToken? cancellationToken = null)
        {
            try
            {
                var cancel = cancellationToken.GetValueOrDefault();
                if (UseUniTaskThreading)
                {
                    MainThreadQueue.Enqueue(() =>
                        Instance.StartCoroutine(AwaitTaskCoroutine(task, onSuccess, onError)));
                }
                else
                {
                    EditorTasksToUpdate.Add(() =>
                    {
                        if (!task.IsCompleted && !cancel.IsCancellationRequested) return false;
                        OnTaskCompleted(task, onSuccess, onError, cancel.IsCancellationRequested);
                        return true;
                    });
                }
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
        }

        public static void WaitUntil(Func<bool> func, Action onSuccess)
        {
            if (UseUniTaskThreading)
            {
                WaitUntilActions.Add(new DispatcherWaitUntilAction
                {
                    WaitHandler = func,
                    Success = onSuccess
                });

                if (WaitUntilActions.Count == 1)
                {
                    Instance.StartCoroutine(WaitUntilEnumerator());
                }
            }
            else
            {
                EditorTasksToUpdate.Add(() =>
                {
                    if (!func()) return false;
                    onSuccess?.Invoke();
                    return true;
                });
            }
        }

        public static void WaitForSeconds(float seconds, Action onSuccess)
        {
            if (seconds <= 0)
            {
                onSuccess?.Invoke();
                return;
            }

            var endTime = Time.unscaledTime + seconds;
            WaitUntil(() => Time.unscaledTime > endTime, onSuccess);
        }

        public static void AtEndOfFrame(Action action)
        {
            if (UseUniTaskThreading)
                MainThreadQueue.Enqueue(action);
            else
                EditorTasksToUpdate.Add(() =>
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    return true;
                });
        }

        private static void OnTaskCompleted<T>(Task<T> task, Action<T> onSuccess, Action<object> onError,
            bool cancelled)
        {
            if (cancelled)
            {
                MainThreadQueue.Enqueue(() => onError?.Invoke("Task was cancelled."));
                return;
            }

            if (task.IsFaulted) MainThreadQueue.Enqueue(() => onError?.Invoke(task.Exception));
            else MainThreadQueue.Enqueue(() => onSuccess?.Invoke(task.Result));
        }

        private static void OnTaskCompleted(Task task, Action onSuccess, Action<object> onError, bool cancelled)
        {
            if (cancelled)
            {
                MainThreadQueue.Enqueue(() => onError?.Invoke("Task was cancelled."));
                return;
            }

            if (task.IsFaulted) MainThreadQueue.Enqueue(() => onError?.Invoke(task.Exception));
            else MainThreadQueue.Enqueue(() => onSuccess?.Invoke());
        }
    }
}