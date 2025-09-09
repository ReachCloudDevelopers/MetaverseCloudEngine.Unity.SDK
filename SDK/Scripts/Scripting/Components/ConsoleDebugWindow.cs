using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Scripting.Components
{
    /// <summary>
    /// Always-available debug console that can be opened via a long-press gesture:
    /// - Desktop: hold the tilde/backquote (`) key for 5 seconds (window must be focused)
    /// - Mobile: hold bottom-right corner for 10 seconds
    ///
    /// Captures logs starting from the first time it is opened, then continuously thereafter.
    /// Provides filtering by log type and Regex search. Uses OnGUI for rendering.
    /// </summary>
    public sealed class ConsoleDebugWindow : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Bootstrapping
        // ─────────────────────────────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Bootstrap()
        {
            var go = new GameObject("[ConsoleDebugWindow]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _ = go.AddComponent<ConsoleDebugWindow>();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────────────────────────────────────
        private bool _visible;
        private bool _capturing; // becomes true on first open

        private float _keyHoldStart = -1f;
        private float _touchHoldStart = -1f;

        private Rect _windowRect;
        private Vector2 _scroll;
        private string _regex = string.Empty;
        private bool _regexError;
        private Regex _compiledRegex;

        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;

        private const float DesktopHoldSeconds = 5f;
        private const float MobileHoldSeconds = 10f;
        private const int MaxEntries = 5000;

        private readonly List<LogEntry> _entries = new(MaxEntries);

        // ─────────────────────────────────────────────────────────────────────────────
        // Dynamic UI Scaling (replicates CanvasScaler: Scale With Screen Size)
        // ─────────────────────────────────────────────────────────────────────────────
        [Header("UI Scaling")]
        [SerializeField]
        private Vector2 _referenceResolution = new(1920, 1080);

        // 0 = match width, 1 = match height
        [Range(0f, 1f)]
        [SerializeField]
        private float _matchWidthOrHeight = 0.5f;

        // User-adjustable multiplier via +/- keys
        [SerializeField] private float _userScale = 1f;
        [SerializeField] private float _minUserScale = 0.5f;
        [SerializeField] private float _maxUserScale = 3.0f;
        [SerializeField] private float _userScaleStep = 0.1f;

        // Track screen size to rescale/move window when resolution changes
        private int _lastScreenW;
        private int _lastScreenH;

        private struct LogEntry
        {
            public DateTime Time;
            public string Message;
            public string Stack;
            public LogType Type;
        }

        private string LogFilePath => Path.Combine(Application.persistentDataPath, "Player.log");

        // ─────────────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Default window size based on platform/orientation
            var w = Mathf.Clamp(Screen.width * (IsMobile() ? 0.9f : 0.6f), 420f, Screen.width - 20f);
            var h = Mathf.Clamp(Screen.height * (IsMobile() ? 0.6f : 0.5f), 280f, Screen.height - 20f);
            _windowRect = new Rect(10, 10, w, h);

            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
        }

        private void OnEnable()
        {
            Application.focusChanged += OnAppFocusChanged;
        }

        private void OnDisable()
        {
            Application.focusChanged -= OnAppFocusChanged;
            if (_capturing)
                Application.logMessageReceived -= OnLog;
        }

        private void OnAppFocusChanged(bool focused)
        {
            // Reset holds if focus lost
            if (!focused)
            {
                _keyHoldStart = -1f;
                _touchHoldStart = -1f;
            }
        }

        private void Update()
        {
            TryOpenGesture();

            // Handle +/- scale input when visible
            if (_visible)
            {
                // Support main keyboard and keypad variations
                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    _userScale = Mathf.Clamp(_userScale + _userScaleStep, _minUserScale, _maxUserScale);
                }
                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Underscore) || Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    _userScale = Mathf.Clamp(_userScale - _userScaleStep, _minUserScale, _maxUserScale);
                }
            }

            // React to resolution changes (e.g., maximize/minimize Game view)
            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                OnScreenSizeChanged(_lastScreenW, _lastScreenH, Screen.width, Screen.height);
                _lastScreenW = Screen.width;
                _lastScreenH = Screen.height;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Input gestures for opening
        // ─────────────────────────────────────────────────────────────────────────────
        private void TryOpenGesture()
        {
            if (!Application.isFocused) { _keyHoldStart = -1f; _touchHoldStart = -1f; return; }

            if (!IsMobile())
            {
                // Desktop: tilde/backquote key
                if (Input.GetKey(KeyCode.BackQuote))
                {
                    if (_keyHoldStart < 0f) _keyHoldStart = Time.unscaledTime;
                    if (Time.unscaledTime - _keyHoldStart >= DesktopHoldSeconds)
                    {
                        ToggleVisible();
                        _keyHoldStart = -1f; // prevent repeat
                    }
                }
                else
                {
                    _keyHoldStart = -1f;
                }
            }
            else
            {
                // Mobile: hold bottom-right corner
                var held = IsBottomRightHeld();
                if (held)
                {
                    if (_touchHoldStart < 0f) _touchHoldStart = Time.unscaledTime;
                    if (Time.unscaledTime - _touchHoldStart >= MobileHoldSeconds)
                    {
                        ToggleVisible();
                        _touchHoldStart = -1f; // prevent repeat
                    }
                }
                else
                {
                    _touchHoldStart = -1f;
                }
            }
        }

        private bool IsBottomRightHeld()
        {
            if (Input.touchCount <= 0) return false;
            var rect = new Rect(Screen.width * 0.75f, 0, Screen.width * 0.25f, Screen.height * 0.25f);
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.touches[i];
                if (t.phase == TouchPhase.Canceled || t.phase == TouchPhase.Ended) continue;
                if (rect.Contains(t.position)) return true;
            }
            return false;
        }

        private void ToggleVisible()
        {
            _visible = !_visible;
            if (_visible && !_capturing)
            {
                Application.logMessageReceived += OnLog;
                _capturing = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Logging
        // ─────────────────────────────────────────────────────────────────────────────
        private void OnLog(string condition, string stacktrace, LogType type)
        {
            if (!_capturing) return;
            var e = new LogEntry
            {
                Time = DateTime.Now,
                Message = condition ?? string.Empty,
                Stack = stacktrace ?? string.Empty,
                Type = type
            };
            if (_entries.Count >= MaxEntries) _entries.RemoveAt(0);
            _entries.Add(e);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // OnGUI
        // ─────────────────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_visible) return;

            // Compute dynamic scale similar to Canvas Scaler (Scale With Screen Size)
            var scale = ComputeScaleWithScreenSize() * Mathf.Max(0.01f, _userScale);

            // Apply global scale while keeping window dimensions in screen space
            // by inversely scaling the window rect used for layout.
            var prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var scaledInRect = new Rect(
                _windowRect.x / scale,
                _windowRect.y / scale,
                _windowRect.width / scale,
                _windowRect.height / scale);

            var outRect = GUI.Window(GetInstanceID(), scaledInRect, DrawWindow, "Console Debug");

            // Convert back to screen space
            _windowRect = new Rect(outRect.x * scale, outRect.y * scale, outRect.width * scale, outRect.height * scale);

            GUI.matrix = prev;
        }

        private float ComputeScaleWithScreenSize()
        {
            var refW = Mathf.Max(1f, _referenceResolution.x);
            var refH = Mathf.Max(1f, _referenceResolution.y);
            var wRatio = Screen.width / refW;
            var hRatio = Screen.height / refH;
            // Match-Width-Or-Height style scaling without logs
            var scale = Mathf.Pow(wRatio, 1f - _matchWidthOrHeight) * Mathf.Pow(hRatio, _matchWidthOrHeight);
            // Light platform bump for small mobile screens (optional, mild)
            if (IsMobile())
            {
                // Avoid making tiny phones unreadable when reference is large
                scale = Mathf.Max(scale, 0.75f);
            }
            return scale;
        }

        private void OnScreenSizeChanged(int oldW, int oldH, int newW, int newH)
        {
            if (oldW <= 0 || oldH <= 0) return;

            // Preserve relative position and size as percentages
            float xPct = _windowRect.x / oldW;
            float yPct = _windowRect.y / oldH;
            float wPct = _windowRect.width / oldW;
            float hPct = _windowRect.height / oldH;

            float minW = 420f;
            float minH = 280f;

            _windowRect.x = Mathf.Round(xPct * newW);
            _windowRect.y = Mathf.Round(yPct * newH);
            _windowRect.width = Mathf.Clamp(Mathf.Round(wPct * newW), minW, newW - 20f);
            _windowRect.height = Mathf.Clamp(Mathf.Round(hPct * newH), minH, newH - 20f);

            ClampWindowToScreen(newW, newH);
        }

        private void ClampWindowToScreen(int sw, int sh)
        {
            float maxX = Mathf.Max(0f, sw - _windowRect.width);
            float maxY = Mathf.Max(0f, sh - _windowRect.height);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, maxY);
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            // Filters row
            GUILayout.BeginHorizontal();
            var all = GUILayout.Toggle(_showInfo && _showWarning && _showError, "All", GUILayout.Width(60));
            if (all && !(_showInfo && _showWarning && _showError))
            {
                _showInfo = _showWarning = _showError = true;
            }
            if (!all && _showInfo && _showWarning && _showError)
            {
                // do nothing; explicit toggles below take over
            }
            _showInfo = GUILayout.Toggle(_showInfo, "Info", GUILayout.Width(60));
            _showWarning = GUILayout.Toggle(_showWarning, "Warning", GUILayout.Width(80));
            _showError = GUILayout.Toggle(_showError, "Error", GUILayout.Width(70));

            GUILayout.FlexibleSpace();

            // Current scale indicator and hint
            GUILayout.Label($"Scale: {(int)(_userScale * 100f)}%  (+/-)", GUILayout.Width(150));

            GUILayout.Label("Regex:", GUILayout.Width(50));
            var newRegex = GUILayout.TextField(_regex, GUILayout.MinWidth(120));
            if (!string.Equals(newRegex, _regex, StringComparison.Ordinal))
            {
                _regex = newRegex;
                TryCompileRegex();
            }

#if !UNITY_WEBGL
            if (GUILayout.Button("Open Log File", GUILayout.Width(140)))
            {
                TryOpenLogFile();
            }
#else
            GUI.enabled = false;
            GUILayout.Button("Open Log File", GUILayout.Width(140));
            GUI.enabled = true;
#endif

            if (GUILayout.Button(_visible ? "Close" : "Open", GUILayout.Width(70)))
            {
                ToggleVisible();
            }

            GUILayout.EndHorizontal();

            if (_regexError)
            {
                var c = GUI.color; GUI.color = Color.red;
                GUILayout.Label("Invalid regex pattern");
                GUI.color = c;
            }

            // Log list
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!PassesTypeFilter(e.Type)) continue;
                if (_compiledRegex != null && !_compiledRegex.IsMatch(e.Message)) continue;

                DrawEntry(e);
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void DrawEntry(LogEntry e)
        {
            Color col = e.Type switch
            {
                LogType.Warning => new Color(1f, 0.75f, 0.2f, 1f),
                LogType.Error => Color.red,
                LogType.Assert => Color.red,
                LogType.Exception => Color.red,
                _ => Color.white
            };

            var prev = GUI.color; GUI.color = col;
            GUILayout.Label($"[{e.Time:HH:mm:ss}] {e.Type}: {e.Message}");
            GUI.color = prev;

            if (!string.IsNullOrEmpty(e.Stack) && e.Type != LogType.Log)
            {
                var c2 = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.8f);
                GUILayout.Label(e.Stack);
                GUI.color = c2;
            }
        }

        private bool PassesTypeFilter(LogType t)
        {
            return t switch
            {
                LogType.Log => _showInfo,
                LogType.Warning => _showWarning,
                LogType.Assert => _showError,
                LogType.Error => _showError,
                LogType.Exception => _showError,
                _ => true
            };
        }

        private void TryCompileRegex()
        {
            _regexError = false;
            _compiledRegex = null;
            if (string.IsNullOrEmpty(_regex)) return;
            try
            {
                _compiledRegex = new Regex(_regex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch
            {
                _regexError = true;
            }
        }

        private void TryOpenLogFile()
        {
            try
            {
                Application.OpenURL("file://" + LogFilePath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to open log file: {ex.Message}");
            }
        }

        private static bool IsMobile()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return false;
#endif
        }
    }
}
