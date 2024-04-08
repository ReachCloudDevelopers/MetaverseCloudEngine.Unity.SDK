using System;
using System.Collections;
using System.Linq;
using MetaverseCloudEngine.Unity.Attributes;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.UI.Components
{
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(Window))]
    [UnityEditor.CanEditMultipleObjects]
    public class WindowEditor : UnityEditor.Editor
    {
        private Window _window;

        private void OnEnable()
        {
            _window = (Window) target;
            if (!_window.IsOpen)
                _window.IsOpen = _window.IsOpen;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_window.IsOpen ? GUILayout.Button("Close") : GUILayout.Button("Open"))
            {
                UnityEditor.Undo.RecordObject(_window.gameObject, "Open / Close window");
                _window.IsOpen = !_window.IsOpen;
                UnityEditor.EditorUtility.SetDirty(_window);
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "isOpen"); 

            serializedObject.ApplyModifiedProperties();
        }
    }

#endif

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [HierarchyIcon("winbtn_win_max_a@2x")]
    public class Window : MonoBehaviour
    {
        /// <summary>
        /// This class is specifically to prevent this window from performing
        /// state changes multiple times within the same call stack.
        /// </summary>
        private class WindowChangeScope : IDisposable
        {
            private readonly Window _window;

            public WindowChangeScope(Window window)
            {
                _window = window;
            }

            public bool TryEnter()
            {
                if (_window._isChanging)
                    return false;
                _window._isChanging = true;
                return true;
            }

            public void Dispose()
            {
                _window._isChanging = false;
            }
        }

        public enum WindowTransition
        {
            None,
            FadeInOut
        }

        [SerializeField] private bool isOpen;
        [SerializeField] private string id;
        [SerializeField] private bool changeGameObjectActive = true;
        [SerializeField] private int priority;
        [SerializeField] private WindowGroup windowGroup;

        [Header("Animation")]
        [SerializeField] private WindowTransition transition = WindowTransition.FadeInOut;

        [Header("Behavior")]
        [SerializeField] private bool autoOpenParentWindow = true;
        [SerializeField] private bool autoOpenWhenParentOpens = true;
        [SerializeField] private bool autoCloseWhenParentCloses = true;

        [Header("Events")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;
        public UnityEvent<bool> onChange;

        private bool _didStart;
        private bool _isChanging;
        private CanvasGroup _canvasGroup;
        private IEnumerator _animationEnumerator;
        private bool _disabling;

        public CanvasGroup CanvasGroup {
            get {
                if (_canvasGroup == null)
                    _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        public string ID => id;

        public bool IsOpen {
            get => isOpen;
            set {
                if (isOpen == value) return;
                SetOpen(value);
            }
        }

        public bool IsClosed {
            get => !isOpen;
            set {
                if (isOpen != value)
                    return;
                SetOpen(!value);
            }
        }

        private void Start()
        {
            if (!_didStart)
            {
                InvokeWindowEvents(isOpen);
                ToggleGameObjectActivationState();
                _didStart = true;
            }
        }

        private void OnDisable()
        {
            _disabling = true;
            try { FinishAnimationImmediately(); }
            finally { _disabling = false; }
        }

        private void Reset() => windowGroup = GetComponentInParent<WindowGroup>();

        public void SetOpen(bool value)
        {
            if (value) Open();
            else Close();
        }

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            using var scope = new WindowChangeScope(this);
            if (!scope.TryEnter())
                return;

            OpenParentHierachy();
            OpenChildHierarchy();
            CloseOtherWindowGroupWindows();
            ChangeWindowState(true);
        }

        public void Close()
        {
            using var scope = new WindowChangeScope(this);
            if (!scope.TryEnter())
                return;

            if (IsTheOnlyWindowGroupWindow()) return;
            CloseChildHierarchy();
            OpenBestWindowGroupWindowBecauseWeClosed();
            ChangeWindowState(false);
        }

        private void OpenParentHierachy()
        {
            if (!autoOpenParentWindow)
                return;

            if (!transform.parent)
                return;

            var parentWindow = transform.parent.GetComponentInParent<Window>(true);
            if (parentWindow)
                parentWindow.OpenFromChildWindow();
        }

        private void OpenFromChildWindow()
        {
            using var scope = new WindowChangeScope(this);
            if (!scope.TryEnter())
                return;

            OpenParentHierachy();
            CloseOtherWindowGroupWindows();
            ChangeWindowState(true);
        }

        private void OpenBestWindowGroupWindowBecauseWeClosed()
        {
            if (!windowGroup || windowGroup.allowNoOpenWindows)
                return;

            Window windowGroupWindow = windowGroup.gameObject
                .GetComponentsInChildrenOrdered<Window>()
                .Where(x => x != this && !x.isOpen && x.windowGroup == windowGroup)
                .OrderByDescending(x => x.priority)
                .FirstOrDefault();

            if (windowGroupWindow == null)
                return;

            windowGroupWindow.Open();
        }

        private void OpenChildHierarchy()
        {
            System.Collections.Generic.IEnumerable<IGrouping<WindowGroup, Window>> children = gameObject
                .GetTopLevelComponentsInChildrenOrdered<Window>()
                .GroupBy(x => x.windowGroup);

            if (windowGroup)
                children = children.Where(x => x.Key != windowGroup); // Make sure we're not opening someone in OUR group.

            foreach (var group in children)
            {
                if (group.Key == null)
                {
                    // Open all children if there's no group associated
                    // to them.
                    foreach (var window in group)
                        window.OpenFromParentWindow();
                    continue;
                }

                Window bestChild = group.OrderByDescending(x => x.priority).FirstOrDefault(x => x.autoOpenWhenParentOpens);
                if (bestChild != null)
                    bestChild.OpenFromParentWindow();
            }
        }

        private void OpenFromParentWindow()
        {
            if (!autoOpenWhenParentOpens)
                return;

            using var scope = new WindowChangeScope(this);
            if (!scope.TryEnter())
                return;

            OpenChildHierarchy();
            CloseOtherWindowGroupWindows();
            ChangeWindowState(true);
        }

        private void CloseOtherWindowGroupWindows()
        {
            if (!windowGroup)
                return;

            var windowGroupWindow = windowGroup.gameObject
                .GetComponentsInChildrenOrdered<Window>()
                .Where(x => x.windowGroup == windowGroup && x != this && x.isOpen && !x.transform.IsChildOf(transform))
                .FirstOrDefault();

            if (windowGroupWindow != null)
                windowGroupWindow.CloseBecauseAnotherGroupedWindowOpened();
        }

        private void CloseChildHierarchy()
        {
            System.Collections.Generic.IEnumerable<Window> childWindows = gameObject.GetTopLevelComponentsInChildrenOrdered<Window>().Where(x => x != this && x.autoCloseWhenParentCloses);
            foreach (var childWindow in childWindows)
                childWindow.CloseBecauseParentWindowClosed();
        }

        private void CloseBecauseAnotherGroupedWindowOpened()
        {
            using var scope = new WindowChangeScope(this);
            if (!scope.TryEnter())
                return;

            CloseChildHierarchy();
            ChangeWindowState(false);
        }

        private void CloseBecauseParentWindowClosed()
        {
            using var scope = new WindowChangeScope(this);
            if (!scope.TryEnter())
                return;

            CloseChildHierarchy();
            ChangeWindowState(false);
        }

        private bool IsTheOnlyWindowGroupWindow()
        {
            if (windowGroup != null && !windowGroup.allowNoOpenWindows)
            {
                System.Collections.Generic.IEnumerable<Window> otherWindows = windowGroup.gameObject
                    .GetComponentsInChildrenOrdered<Window>()
                    .Where(x => x.windowGroup == windowGroup && x != this);

                if (!otherWindows.Any())
                    return true;
            }

            return false;
        }

        private void ChangeWindowState(bool value)
        {
            if (isOpen == value)
                return;

            isOpen = value;
            if (value)
                ToggleGameObjectActivationState();

            if (CanvasGroup.interactable != value)
                CanvasGroup.interactable = value;
            if (CanvasGroup.blocksRaycasts != value)
                CanvasGroup.blocksRaycasts = value;

            StartAnimation();
            InvokeWindowEvents(value);
        }

        private void InvokeWindowEvents(bool value)
        {
            if (value) onOpened?.Invoke();
            else onClosed?.Invoke();
            onChange?.Invoke(value);
        }

        private void StartAnimation()
        {
            StopAnimation();

            if (!Application.isPlaying || !enabled || !gameObject.activeInHierarchy || transition == WindowTransition.None || Time.timeScale < 0.01f)
            {
                var targetAlpha = isOpen ? 1 : 0;
                if (Math.Abs(CanvasGroup.alpha - targetAlpha) > 0.0001f)
                    CanvasGroup.alpha = targetAlpha;
                ToggleGameObjectActivationState();
                if (Application.isPlaying)
                    FinishAnimationImmediately();
                return;
            }

            switch (transition)
            {
                case WindowTransition.FadeInOut:
                    _animationEnumerator = FadeAnimation();
                    StartCoroutine(_animationEnumerator);
                    break;
            }
        }

        private void StopAnimation()
        {
            if (_animationEnumerator != null)
            {
                StopCoroutine(_animationEnumerator);
                _animationEnumerator = null;
            }
        }

        private IEnumerator FadeAnimation()
        {
            const float fadeDuration = 0.15f;

            if (isOpen)
                yield return new WaitForSeconds(fadeDuration);

            var startAlpha = CanvasGroup.alpha;
            var endAlpha = isOpen ? 1 : 0;

            var startScale = Vector3.one * (isOpen ? 0.95f : 1f);
            var endScale = Vector3.one * (isOpen ? 1f : 1.05f);

            var endTime = MVUtils.CachedTime + fadeDuration;
            while (MVUtils.CachedTime <= endTime)
            {
                var t = 1f - ((endTime - MVUtils.CachedTime) / fadeDuration);
                var targetAlpha = Mathf.Lerp(startAlpha, endAlpha, t);
                if (Math.Abs(CanvasGroup.alpha - targetAlpha) > 0.0001f)
                    CanvasGroup.alpha = targetAlpha;
                if (transform.localScale != endScale)
                    transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            CompleteAnimation();
        }

        private void CompleteAnimation()
        {
            var endAlpha = GetTargetAlpha();
            var endScale = GetTargetScale();
            if (Math.Abs(CanvasGroup.alpha - endAlpha) > 0.0001f)
                CanvasGroup.alpha = endAlpha;
            if (transform.localScale != endScale)
                transform.localScale = endScale;
            ToggleGameObjectActivationState();
        }

        private void ToggleGameObjectActivationState()
        {
            if (!changeGameObjectActive)
                return;

            if (_disabling)
                // Prevent Unity exception that's thrown
                // when trying to deactivate a game object
                // that's already being deactivated.
                return;

            if (gameObject.activeSelf != isOpen)
                gameObject.SetActive(isOpen);
        }

        private int GetTargetAlpha() => isOpen ? 1 : 0;

        private Vector3 GetTargetScale() => Vector3.one * (isOpen ? 1f : 1.05f);

        private void FinishAnimationImmediately()
        {
            CompleteAnimation();
            if (_animationEnumerator != null)
            {
                StopCoroutine(_animationEnumerator);
                _animationEnumerator = null;
            }
        }
    }
}