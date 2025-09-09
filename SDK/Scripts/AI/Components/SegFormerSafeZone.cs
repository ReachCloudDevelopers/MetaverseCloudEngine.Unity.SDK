#if MV_UNITY_AI_INFERENCE
using System;
using TriInspectorMVCE;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.AI;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    /// <summary>
    /// Consumes the ground mask produced by SegFormerGroundMask and evaluates a
    /// triangular "runway" that projects from the bottom of the image toward an apex.
    /// The lower the blank (non-ground) pixels in the runway, the stronger the stop score.
    /// Emits a boolean stop signal and a continuous danger score in [0,1].
    /// Also draws gizmos to visualize the frame and runway.
    /// </summary>
    [HideMonoScript]
    [RequireComponent(typeof(SegFormerGroundMask))]
    public sealed class SegFormerSafeZone : TriInspectorMonoBehaviour
    {
        [Header("Source")]
        [Required]
        public SegFormerGroundMask segformer;

        [Header("Runway (normalized)")]
        [Tooltip("Bottom-left X of the runway base (0..1).")]
        [Range(0, 1)] public float baseLeftX = 0.05f;
        [Tooltip("Bottom-right X of the runway base (0..1).")]
        [Range(0, 1)] public float baseRightX = 0.95f;
        [Tooltip("Apex horizontal position (0..1, 0.5=center).")]
        [Range(0, 1)] public float apexX = 0.5f;
        [Tooltip("Apex vertical position (0..1 from bottom). Lower = short runway.")]
        [Range(0, 1)] public float apexY = 0.55f;

        [Header("Scoring")]
        [Tooltip("Pixel value threshold to consider ground (0..1).")]
        [Range(0, 1)] public float groundThreshold = 0.5f;
        [Tooltip("Exponent for vertical weighting (1 = linear, >1 = stronger emphasis near bottom).")]
        [Range(0.5f, 8f)] public float weightPower = 2.0f;
        [Tooltip("Stop when danger score >= threshold.")]
        [Range(0, 1)] public float stopThreshold = 0.35f;

        [Header("Events")]
        public UnityEvent<bool> onStop = new();
        public UnityEvent<float> onDanger = new();
        public UnityEvent<string> onDebugText = new();

        [Header("Outputs (read-only)")]
        [TriInspectorMVCE.ReadOnly] public float dangerScore;
        [TriInspectorMVCE.ReadOnly] public bool stop;

        // Internal state
        private bool _readbackPending;
        private int _texW = 1, _texH = 1;
        private bool _lastStop;

        [Header("Obstacle Mapping")]
        [Tooltip("Create NavMeshObstacle proxies from runway samples.")]
        public bool createObstacles = true;
        [Tooltip("If true, created obstacles will carve the NavMesh.")]
        public bool carveNavMesh = true;
        [Tooltip("Number of depth samples along the runway. One obstacle per sample.")]
        [Range(1, 64)] public int samples = 12;
        [Tooltip("Meters from origin at the runway base (near edge).")]
        public float nearDistance = 0.25f;
        [Tooltip("Meters from origin at the apex (far edge).")]
        public float farDistance = 3.0f;
        [Tooltip("Half width in meters at the runway base (near).")]
        public float baseHalfWidthMeters = 0.5f;
        [Tooltip("Half width in meters at the apex (far).")]
        public float apexHalfWidthMeters = 0.1f;
        [Tooltip("Minimum hole width ratio to spawn an obstacle.")]
        [Range(0f, 1f)] public float minHoleWidthRatio = 0.15f;
        [Tooltip("Local Y position to place obstacles.")]
        public float obstacleLocalY = 0f;
        [Tooltip("Y size of box obstacles.")]
        public float obstacleHeight = 0.5f;
        [Tooltip("Use Box shape (recommended). If false, uses Capsule.")]
        public bool obstacleAsBox = true;

        private readonly System.Collections.Generic.List<NavMeshObstacle> _obstacles = new();
        private float[] _sampleHoleWidth;   // 0..1 width ratio inside row
        private float[] _sampleHoleOffset;  // -1..1 lateral offset from midline
        private bool[] _sampleHasHole;

        // Gizmos
        [Header("Gizmos")]
        public bool drawGizmos = true;
        public Vector2 previewFrameSize = new(512, 512);
        public float gizmoZ = 0f;
        public float gizmoScale = 1f;
        public Color frameOutlineColor = new Color(1f, 1f, 1f, 0.4f);
        public Color zoneOutlineColor = new Color(1f, 0.55f, 0f, 0.95f);
        public Color zoneFillOk = new Color(0f, 1f, 0f, 0.15f);
        public Color zoneFillStop = new Color(1f, 0f, 0f, 0.18f);
        public Color midlineColor = new Color(1f, 1f, 1f, 0.25f);

        private void Reset()
        {
            segformer = GetComponent<SegFormerGroundMask>();
        }

        private void OnEnable()
        {
            if (!segformer) segformer = GetComponent<SegFormerGroundMask>();
            if (segformer) segformer.onMaskUpdated.AddListener(OnMaskUpdated);
        }

        private void OnDisable()
        {
            if (segformer) segformer.onMaskUpdated.RemoveListener(OnMaskUpdated);
        }

        private void OnMaskUpdated(RenderTexture mask)
        {
            _texW = Mathf.Max(1, mask.width);
            _texH = Mathf.Max(1, mask.height);

            if (_readbackPending) return; // avoid piling requests
            _readbackPending = true;
            AsyncGPUReadback.Request(mask, 0, request =>
            {
                _readbackPending = false;
                if (request.hasError) return;

                try
                {
                    var data = request.GetData<byte>();
                    EvaluateRunway(data, _texW, _texH);
                }
                catch (Exception) { /* ignore transient readback issues */ }
            });
        }

        private void EnsureSampleArrays()
        {
            var n = Mathf.Max(1, samples);
            if (_sampleHoleWidth == null || _sampleHoleWidth.Length != n)
            {
                _sampleHoleWidth = new float[n];
                _sampleHoleOffset = new float[n];
                _sampleHasHole = new bool[n];
            }
            else
            {
                Array.Clear(_sampleHoleWidth, 0, _sampleHoleWidth.Length);
                Array.Clear(_sampleHoleOffset, 0, _sampleHoleOffset.Length);
                Array.Clear(_sampleHasHole, 0, _sampleHasHole.Length);
            }
        }

        private void EvaluateRunway(NativeArray<byte> data, int w, int h)
        {
            if (!data.IsCreated || data.Length <= 0) return;
            EnsureSampleArrays();

            var baseL = Mathf.Clamp01(Mathf.Min(baseLeftX, baseRightX));
            var baseR = Mathf.Clamp01(Mathf.Max(baseLeftX, baseRightX));
            var ax = Mathf.Clamp01(apexX);
            var ay = Mathf.Clamp01(Mathf.Max(0.0001f, apexY)); // avoid divide-by-zero

            var thrByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(groundThreshold) * 255f);

            double wSum = 0.0;
            double dangerSum = 0.0;

            // Scan rows from bottom (y=0) up to apexY portion of the image.
            var maxY = Mathf.Min(h - 1, Mathf.RoundToInt(ay * (h - 1)));
            for (int y = 0; y <= maxY; y++)
            {
                var v = (float)y / (h - 1); // 0..1 bottom->top
                var t = v / ay;             // 0..1 along runway height
                var lx = Mathf.Lerp(baseL, ax, t);
                var rx = Mathf.Lerp(baseR, ax, t);

                var x0 = Mathf.Clamp(Mathf.RoundToInt(lx * (w - 1)), 0, w - 1);
                var x1 = Mathf.Clamp(Mathf.RoundToInt(rx * (w - 1)), 0, w - 1);
                if (x1 < x0) { var tmp = x0; x0 = x1; x1 = tmp; }

                // Weight more heavily near the bottom
                var weight = Mathf.Pow(1f - v, weightPower);

                // Track largest contiguous non-ground run (hole) in this row
                int bestRun = 0, bestRunStart = -1;
                int run = 0, runStart = -1;
                for (int x = x0; x <= x1; x++)
                {
                    int idx = y * w + x;     // R8 layout: one byte per pixel
                    var isGround = data[idx] >= thrByte;
                    if (!isGround)
                    {
                        dangerSum += weight;
                        if (run == 0) runStart = x;
                        run++;
                    }
                    else if (run > 0)
                    {
                        if (run > bestRun) { bestRun = run; bestRunStart = runStart; }
                        run = 0; runStart = -1;
                    }
                    wSum += weight;
                }
                if (run > 0 && run > bestRun) { bestRun = run; bestRunStart = runStart; }

                // Map this row to a sample bucket and record the widest hole
                var halfWidthPx = (x1 - x0 + 1) * 0.5f;
                if (bestRun > 0 && halfWidthPx > 0.5f)
                {
                    var holeCenterX = bestRunStart + bestRun * 0.5f;
                    var midX = (x0 + x1) * 0.5f;
                    var widthRatio = Mathf.Clamp01(bestRun / (float)(x1 - x0 + 1));
                    var lateral = Mathf.Clamp((float)((holeCenterX - midX) / halfWidthPx), -1f, 1f);

                    int si = Mathf.Clamp(Mathf.RoundToInt(t * (samples - 1)), 0, samples - 1);
                    if (!_sampleHasHole[si] || widthRatio > _sampleHoleWidth[si])
                    {
                        _sampleHasHole[si] = true;
                        _sampleHoleWidth[si] = widthRatio;
                        _sampleHoleOffset[si] = lateral;
                    }
                }
            }

            var danger = wSum > 0.0 ? (float)(dangerSum / wSum) : 0f;
            danger = Mathf.Clamp01(danger);

            dangerScore = danger;
            onDanger?.Invoke(danger);

            var doStop = danger >= stopThreshold;
            stop = doStop;

            if (doStop != _lastStop)
            {
                _lastStop = doStop;
                onStop?.Invoke(doStop);
            }

            onDebugText?.Invoke($"runway danger={danger:F3} thr={stopThreshold:F2}");

            UpdateObstaclesFromSamples();
        }

        private void UpdateObstaclesFromSamples()
        {
            if (_sampleHoleWidth == null) return;
            if (!createObstacles)
            {
                DisableAllObstacles();
                return;
            }
            EnsureObstaclePool();

            for (int i = 0; i < samples; i++)
            {
                var has = _sampleHasHole[i] && _sampleHoleWidth[i] >= minHoleWidthRatio;
                var obs = _obstacles[i];
                if (!has)
                {
                    if (obs) obs.gameObject.SetActive(false);
                    continue;
                }

                var t = samples == 1 ? 0f : i / (float)(samples - 1);
                var dist = Mathf.Lerp(nearDistance, farDistance, t);
                var halfW = Mathf.Lerp(baseHalfWidthMeters, apexHalfWidthMeters, t);
                var offset = Mathf.Clamp(_sampleHoleOffset[i], -1f, 1f) * halfW;
                var widthMeters = Mathf.Max(0.01f, _sampleHoleWidth[i] * (halfW * 2f));

                var local = new Vector3(offset, obstacleLocalY, dist);
                var world = transform.TransformPoint(local);
                obs.transform.position = world;
                obs.transform.rotation = transform.rotation; // align with forward
                obs.gameObject.SetActive(true);

                if (obstacleAsBox)
                {
                    obs.shape = NavMeshObstacleShape.Box;
                    var size = new Vector3(widthMeters, Mathf.Max(0.01f, obstacleHeight), widthMeters);
                    obs.size = size;
                    obs.center = Vector3.zero;
                }
                else
                {
                    obs.shape = NavMeshObstacleShape.Capsule;
                    obs.radius = widthMeters * 0.5f;
                    obs.height = Mathf.Max(0.01f, obstacleHeight);
                    obs.center = Vector3.zero;
                }

                obs.carving = carveNavMesh;
                obs.carveOnlyStationary = false;
            }
        }

        private void EnsureObstaclePool()
        {
            var needed = Mathf.Max(1, samples);
            while (_obstacles.Count < needed)
            {
                var go = new GameObject($"RunwayObstacle[{_obstacles.Count}]");
                go.transform.SetParent(transform, false);
                var obs = go.AddComponent<NavMeshObstacle>();
                obs.carving = carveNavMesh;
                obs.carveOnlyStationary = false;
                obs.gameObject.SetActive(false);
                _obstacles.Add(obs);
            }
            // If samples decreased, disable extras
            for (int i = needed; i < _obstacles.Count; i++)
            {
                if (_obstacles[i]) _obstacles[i].gameObject.SetActive(false);
            }
        }

        private void DisableAllObstacles()
        {
            if (_obstacles == null) return;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                if (_obstacles[i]) _obstacles[i].gameObject.SetActive(false);
            }
        }

        private void OnValidate()
        {
            // Keep existing obstacles in sync with flags when edited in Inspector
            if (_obstacles == null) return;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (!obs) continue;
                obs.carving = carveNavMesh;
                if (!createObstacles) obs.gameObject.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Gizmos
        // ─────────────────────────────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            var fw = (_texW > 1 || _texH > 1) ? _texW : Mathf.RoundToInt(Mathf.Max(1f, previewFrameSize.x));
            var fh = (_texW > 1 || _texH > 1) ? _texH : Mathf.RoundToInt(Mathf.Max(1f, previewFrameSize.y));
            _ = fw; _ = fh; // dimensions are only for conceptual preview; drawing uses 0..1 space

            var baseL = Mathf.Clamp01(Mathf.Min(baseLeftX, baseRightX));
            var baseR = Mathf.Clamp01(Mathf.Max(baseLeftX, baseRightX));
            var ax = Mathf.Clamp01(apexX);
            var ay = Mathf.Clamp01(apexY);

            var A = new Vector2(baseL, 0f);
            var B = new Vector2(baseR, 0f);
            var C = new Vector2(ax, ay);

            // Frame outline
            Gizmos.color = frameOutlineColor;
            DrawRectOutline(new Rect(0, 0, 1, 1), gizmoScale, gizmoScale, gizmoZ);

            // Fill the triangle (approximate with a thin cube over its AABB)
            Gizmos.color = _lastStop ? zoneFillStop : zoneFillOk;
            DrawTriangleFill(A, B, C, gizmoScale, gizmoScale, gizmoZ);

            // Outline
            Gizmos.color = zoneOutlineColor;
            DrawTriOutline(A, B, C, gizmoScale, gizmoScale, gizmoZ);

            // Midline for clarity
            Gizmos.color = midlineColor;
            var mid0 = ToWorld(new Vector2((A.x + B.x) * 0.5f, 0f), gizmoScale, gizmoScale, gizmoZ);
            var mid1 = ToWorld(C, gizmoScale, gizmoScale, gizmoZ);
            Gizmos.DrawLine(mid0, mid1);
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

        private void DrawTriOutline(Vector2 a01, Vector2 b01, Vector2 c01, float sx, float sy, float z)
        {
            var A = ToWorld(a01, sx, sy, z);
            var B = ToWorld(b01, sx, sy, z);
            var C = ToWorld(c01, sx, sy, z);
            Gizmos.DrawLine(A, B);
            Gizmos.DrawLine(B, C);
            Gizmos.DrawLine(C, A);
        }

        private void DrawTriangleFill(Vector2 a01, Vector2 b01, Vector2 c01, float sx, float sy, float z)
        {
            // Draw a thin cube over the triangle's AABB as a simple visual fill
            var minX = Mathf.Min(a01.x, Mathf.Min(b01.x, c01.x));
            var maxX = Mathf.Max(a01.x, Mathf.Max(b01.x, c01.x));
            var minY = Mathf.Min(a01.y, Mathf.Min(b01.y, c01.y));
            var maxY = Mathf.Max(a01.y, Mathf.Max(b01.y, c01.y));
            var center = new Vector3((minX + maxX) * 0.5f * sx, (minY + maxY) * 0.5f * sy, z);
            var size = new Vector3(Mathf.Max(0f, maxX - minX) * sx, Mathf.Max(0f, maxY - minY) * sy, 0.0001f);
            Gizmos.DrawCube(transform.TransformPoint(center), size);
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
    public class SegFormerSafeZone : InferenceEngineComponent {}
}
#endif
