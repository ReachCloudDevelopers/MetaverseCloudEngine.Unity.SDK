using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(RectTransform))]
    public class RectTransformSafeZone : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Canvas _canvas;

        private static readonly List<RectTransformSafeZone> Helpers = new();
        private static bool _screenChangeVarsInitialized;
        private static ScreenOrientation _lastOrientation = ScreenOrientation.LandscapeLeft;
        private static Vector2 _lastResolution = Vector2.zero;
        private static Rect _lastSafeArea = Rect.zero;

        private void Awake()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
            {
                enabled = false;
                return;
            }
            
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            InitializeScreenChangedVars();
        }

        private void OnEnable()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            if (!Helpers.Contains(this))
                Helpers.Add(this);

            ApplySafeArea();
        }

        private void OnDisable()
        {
            if (MetaverseProgram.IsQuitting) return;

            if (Helpers != null && Helpers.Contains(this))
                Helpers.Remove(this);
        }

        private void LateUpdate()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            if (Helpers == null || Helpers.Count == 0 || Helpers[0] != this)
                return;

            if (UnityEngine.Device.Screen.orientation != _lastOrientation)
                OrientationChanged();

            const float pixelDiff = 0.0001f;
            if (Math.Abs(UnityEngine.Device.Screen.width - _lastResolution.x) > pixelDiff || 
                Math.Abs(UnityEngine.Device.Screen.height - _lastResolution.y) > pixelDiff)
                ResolutionChanged();

            if (UnityEngine.Device.Screen.safeArea != _lastSafeArea)
                SafeAreaChanged();
        }

        private static void InitializeScreenChangedVars()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            if (_screenChangeVarsInitialized) return;
            _lastOrientation = UnityEngine.Device.Screen.orientation;
            _lastResolution.x = UnityEngine.Device.Screen.width;
            _lastResolution.y = UnityEngine.Device.Screen.height;
            _lastSafeArea = UnityEngine.Device.Screen.safeArea;
            _screenChangeVarsInitialized = true;
        }

        private void ApplySafeArea()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            if (_rectTransform == null || !gameObject.activeInHierarchy) return;
            if (!UnityEngine.Device.Application.isMobilePlatform) return;
            StartCoroutine(ApplySafeAreaNextFrameRoutine(UnityEngine.Device.Screen.safeArea));
        }

        private IEnumerator ApplySafeAreaNextFrameRoutine(Rect safeArea)
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                yield break;

            yield return null;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            Rect pixelRect = _canvas.pixelRect;
            anchorMin.x /= pixelRect.width;
            anchorMin.y /= pixelRect.height;
            anchorMax.x /= pixelRect.width;
            anchorMax.y /= pixelRect.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }

        private static void OrientationChanged()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            _lastOrientation = UnityEngine.Device.Screen.orientation;
            _lastResolution.x = UnityEngine.Device.Screen.width;
            _lastResolution.y = UnityEngine.Device.Screen.height;
        }

        private static void ResolutionChanged()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            _lastResolution.x = UnityEngine.Device.Screen.width;
            _lastResolution.y = UnityEngine.Device.Screen.height;
        }

        private static void SafeAreaChanged()
        {
            if (!UnityEngine.Device.Application.isMobilePlatform)
                return;

            _lastSafeArea = UnityEngine.Device.Screen.safeArea;

            for (int i = 0; i < Helpers.Count; i++)
                Helpers[i].ApplySafeArea();
        }
    }
}
