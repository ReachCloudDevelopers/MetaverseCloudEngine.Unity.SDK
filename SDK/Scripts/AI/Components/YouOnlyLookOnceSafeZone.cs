#if MV_UNITY_AI_INFERENCE
using System.Collections.Generic;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    /// <summary>
    /// Consumes detections from <see cref="YouOnlyLookOnce"/> and emits:
    ///  - STOP/OK based on threshold + mode (supports zone_sum/object/zone/iou)
    ///  - Continuous nav value in [-1,1] every frame (−1=steer left, +1=steer right).
    /// Also renders gizmos for frame bounds and the safe zone.
    /// </summary>
    [HideMonoScript]
    [RequireComponent(typeof(YouOnlyLookOnce))]
    public sealed class YouOnlyLookOnceSafeZone : TriInspectorMonoBehaviour
    {
        [Header("Source")]
        [Tooltip("YOLO component that provides the frame detections.")]
        [Required]
        public YouOnlyLookOnce yolo;

        [Header("Danger Zone (normalized)")]
        [Tooltip("Normalized [0..1] rect in source texture space (origin bottom-left).")]
        public Rect zoneNorm = new(0.25f, 0.4f, 0.5f, 0.2f);

        [Header("Mode & Threshold")]
        public Mode mode = Mode.ZoneSum;
        [Range(0, 1)] public float threshold = 0.45f;

        [Header("Classes to care about")]
        [Tooltip("Leave empty to care about ALL labels.")]
        public List<string> careLabels = new()
        {
            "person",
            "car"
        };

        [Header("Navigation output")]
        public NavTransform navTransform = NavTransform.SignOneMinusAbs;

        [Header("Events")]
        public UnityEvent<bool> onStop = new();
        public UnityEvent<float> onAvoid = new();
        public UnityEvent<string> onDebugText = new();

        public enum Mode { Object, Zone, IoU, ZoneSum }
        public enum NavTransform { None, SignOneMinusAbs }

        private bool _lastStop;
        private float _lastMetric;
        private readonly List<YoloDetection> _buffer = new(64);
        private int _frameW = 1, _frameH = 1;

        // ─────────────────────────────────────────────────────────────────────────────
        // Gizmo settings
        // ─────────────────────────────────────────────────────────────────────────────
        [Header("Gizmos")]
        [Tooltip("Draw gizmos for frame boundaries and safe zone.")]
        public bool drawGizmos = true;

        [Tooltip("When not running (no frames yet), use this size to preview frame bounds.")]
        public Vector2 previewFrameSize = new(640, 640);

        [Tooltip("Place gizmos on this local Z plane (XY plane).")]
        public float gizmoZ = 0f;

        [Tooltip("Uniform scale applied to 0..1 normalized space.")]
        public float gizmoScale = 1f;

        [Tooltip("If true, draw a vertical line through the safe-zone center.")]
        public bool drawZoneMidline = true;

        [Space]
        public Color frameOutlineColor = new Color(1f, 1f, 1f, 0.4f);
        public Color zoneOutlineColor = new Color(1f, 0.55f, 0f, 0.95f);
        public Color zoneFillOk = new Color(0f, 1f, 0f, 0.15f);
        public Color zoneFillStop = new Color(1f, 0f, 0f, 0.18f);
        public Color midlineColor = new Color(1f, 1f, 1f, 0.25f);

        private void OnEnable()
        {
            if (yolo) yolo.DetectionsFrame += OnDetectionsFrame;
        }

        private void OnDisable()
        {
            if (yolo) yolo.DetectionsFrame -= OnDetectionsFrame;
        }

        private void OnDetectionsFrame(IReadOnlyList<YoloDetection> detections, int texW, int texH)
        {
            _frameW = Mathf.Max(1, texW);
            _frameH = Mathf.Max(1, texH);

            _buffer.Clear();
            if (detections is { Count: > 0 })
            {
                if (careLabels == null || careLabels.Count == 0)
                {
                    foreach (var t in detections) _buffer.Add(t);
                }
                else
                {
                    foreach (var d in detections)
                        if (careLabels.Contains(d.Label)) _buffer.Add(d);
                }
            }

            EvaluateAndEmit();
        }

        private void EvaluateAndEmit()
        {
            var zonePx = new Rect(
                zoneNorm.x * _frameW,
                zoneNorm.y * _frameH,
                Mathf.Max(0f, zoneNorm.width) * _frameW,
                Mathf.Max(0f, zoneNorm.height) * _frameH);

            var navRaw = ComputeNavRaw(zonePx, _buffer);
            var nav = ApplyNavTransform(navRaw);
            onAvoid?.Invoke(nav);

            bool stop;
            float metric;

            switch (mode)
            {
                case Mode.ZoneSum:
                    metric = ComputeZoneSum(zonePx, _buffer);
                    stop = metric >= threshold;
                    break;
                case Mode.Object:
                case Mode.Zone:
                case Mode.IoU:
                    metric = ComputeMaxRatio(zonePx, _buffer, mode);
                    stop = metric >= threshold;
                    break;
                default:
                    metric = 0f; stop = false; break;
            }

            if (stop != _lastStop || !Mathf.Approximately(metric, _lastMetric))
            {
                _lastStop = stop;
                _lastMetric = metric;
                onStop?.Invoke(stop);
            }

            onDebugText?.Invoke($"mode={mode} thr={threshold:F2} total={metric:F3} nav={nav:F2}");
        }

        private static float Area(Rect r) => Mathf.Max(0f, r.width) * Mathf.Max(0f, r.height);
        private static float IntersectArea(Rect a, Rect b)
        {
            var x1 = Mathf.Max(a.xMin, b.xMin);
            var y1 = Mathf.Max(a.yMin, b.yMin);
            var x2 = Mathf.Min(a.xMax, b.xMax);
            var y2 = Mathf.Min(a.yMax, b.yMax);
            return Mathf.Max(0f, x2 - x1) * Mathf.Max(0f, y2 - y1);
        }

        private static float ComputeRatio(Rect obj, Rect zone, Mode m)
        {
            var inter = IntersectArea(obj, zone);
            if (inter <= 0f) return 0f;
            var a = Area(obj);
            var z = Area(zone);
            float denom = m switch
            {
                Mode.Object => a,
                Mode.Zone => z,
                Mode.IoU => a + z - inter,
                _ => 1f
            };
            return denom <= 0f ? 0f : inter / denom;
        }

        private static float ComputeZoneSum(Rect zone, List<YoloDetection> dets)
        {
            if (dets == null || dets.Count == 0) return 0f;
            var z = Mathf.Max(1f, Area(zone));
            var sum = 0f;
            for (var i = 0; i < dets.Count; i++)
            {
                sum += IntersectArea(dets[i].Rect, zone) / z;
            }
            return Mathf.Min(1f, sum);
        }

        private static float ComputeMaxRatio(Rect zone, List<YoloDetection> dets, Mode m)
        {
            var maxR = 0f;
            if (dets == null || dets.Count == 0) return 0f;
            for (var i = 0; i < dets.Count; i++)
            {
                var r = ComputeRatio(dets[i].Rect, zone, m);
                if (r > maxR) maxR = r;
            }
            return maxR;
        }

        /// <summary>
        /// Raw left/right guidance based on mass split of overlaps.
        /// Positive means steer RIGHT (more mass on left); negative means steer LEFT.
        /// Range ~[-1,1].
        /// </summary>
        private static float ComputeNavRaw(Rect zone, List<YoloDetection> dets)
        {
            if (dets == null || dets.Count == 0) return 0f;
            var mid = zone.center.x;
            float leftA = 0f, rightA = 0f;
            for (var i = 0; i < dets.Count; i++)
            {
                var r = dets[i].Rect;
                var x1 = Mathf.Max(r.xMin, zone.xMin);
                var y1 = Mathf.Max(r.yMin, zone.yMin);
                var x2 = Mathf.Min(r.xMax, zone.xMax);
                var y2 = Mathf.Min(r.yMax, zone.yMax);
                if (x2 <= x1 || y2 <= y1) continue;
                var h = y2 - y1;
                var leftW = Mathf.Max(0f, Mathf.Min(x2, mid) - x1);
                var rightW = Mathf.Max(0f, x2 - Mathf.Max(x1, mid));
                leftA += leftW * h;
                rightA += rightW * h;
            }
            var denominator = leftA + rightA;
            if (denominator <= 0f) return 0f;
            var raw = Mathf.Clamp((leftA - rightA) / denominator, -1f, 1f); // + => steer right
            return raw;
        }

        private float ApplyNavTransform(float raw)
        {
            return navTransform switch
            {
                NavTransform.None => raw,
                NavTransform.SignOneMinusAbs => Mathf.Sign(raw) * (1f - Mathf.Abs(raw)),
                _ => raw
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Gizmos
        // ─────────────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Determine frame size (pixels) – use live frame if available, otherwise preview.
            var fw = (_frameW > 1 || _frameH > 1) ? _frameW : Mathf.RoundToInt(Mathf.Max(1f, previewFrameSize.x));
            var fh = (_frameW > 1 || _frameH > 1) ? _frameH : Mathf.RoundToInt(Mathf.Max(1f, previewFrameSize.y));

            // Frame in normalized [0..1] space so gizmoScale controls overall size.
            var frame01 = new Rect(0f, 0f, 1f, 1f);
            var zone01 = new Rect(
                Mathf.Clamp01(zoneNorm.x),
                Mathf.Clamp01(zoneNorm.y),
                Mathf.Clamp01(Mathf.Max(0f, zoneNorm.width)),
                Mathf.Clamp01(Mathf.Max(0f, zoneNorm.height))
            );

            // Draw frame outline
            Gizmos.color = frameOutlineColor;
            DrawRectOutline(frame01, gizmoScale, gizmoScale, gizmoZ);

            // Zone fill (green=OK, red=STOP)
            var fillCol = _lastStop ? zoneFillStop : zoneFillOk;
            Gizmos.color = fillCol;
            DrawRectFill(zone01, gizmoScale, gizmoScale, gizmoZ);

            // Zone outline
            Gizmos.color = zoneOutlineColor;
            DrawRectOutline(zone01, gizmoScale, gizmoScale, gizmoZ);

            // Optional vertical midline through zone center (helps visualize nav split)
            if (drawZoneMidline)
            {
                Gizmos.color = midlineColor;
                var cx = zone01.center.x;
                var y0 = ToWorld(new Vector2(cx, zone01.yMin), gizmoScale, gizmoScale, gizmoZ);
                var y1 = ToWorld(new Vector2(cx, zone01.yMax), gizmoScale, gizmoScale, gizmoZ);
                Gizmos.DrawLine(y0, y1);
            }

            // Optionally: you could also render text via Handles.Label, but that requires UnityEditor.
            // Keeping this runtime-safe: no UnityEditor dependency here.
        }

        private void DrawRectOutline(Rect r01, float sx, float sy, float z)
        {
            var p0 = ToWorld(new Vector2(r01.xMin, r01.yMin), sx, sy, z);
            var p1 = ToWorld(new Vector2(r01.xMax, r01.yMin), sx, sy, z);
            var p2 = ToWorld(new Vector2(r01.xMax, r01.yMax), sx, sy, z);
            var p3 = ToWorld(new Vector2(r01.xMin, r01.yMax), sx, sy, z);

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }

        private void DrawRectFill(Rect r01, float sx, float sy, float z)
        {
            // Draw a thin cube as a filled quad on XY plane
            var center01 = r01.center;
            var size01 = r01.size;

            var center = transform.TransformPoint(new Vector3(center01.x * sx, center01.y * sy, z));
            var size = new Vector3(Mathf.Max(0f, size01.x) * sx, Mathf.Max(0f, size01.y) * sy, 0.0001f);
            Gizmos.DrawCube(center, size);
        }

        private Vector3 ToWorld(Vector2 p01, float sx, float sy, float z)
        {
            return transform.TransformPoint(new Vector3(p01.x * sx, p01.y * sy, z));
        }
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class YouOnlyLookOnceSafeZone : InferenceEngineComponent {}
}
#endif
