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
    /// Consumes the ground mask and evaluates a camera-derived trapezoid "runway"
    /// bounded horizontally by a single corridor width (meters) and vertically by
    /// the horizon row computed from camera FOV + pitch.
    /// Emits a boolean stop signal and a continuous danger score in [0,1].
    /// Draws gizmos to visualize the frame and trapezoid.
    /// </summary>
    [HideMonoScript]
    [RequireComponent(typeof(SegFormerGroundMask))]
    public sealed class SegFormerSafeZone : TriInspectorMonoBehaviour
    {
        [Header("Source")]
        [Required] public SegFormerGroundMask segformer;

        // ─────────────────────────────────────────────────────────────────────────────
        // NEW: Camera-based runway parameters
        // ─────────────────────────────────────────────────────────────────────────────
        [Header("Runway (camera-based)")]
        [Tooltip("Camera used to derive FOV. If null, Camera.main is used.")]
        public Camera sourceCamera;
        [Tooltip("Downward pitch of the camera in degrees. 0 = level, + = looking down.")]
        [Range(-45f, 89f)] public float pitchDegrees = 12f;
        [Tooltip("Corridor width on the ground (meters). Used at near & far to compute image bounds.")]
        [Min(0.05f)] public float corridorWidthMeters = 0.8f;

        [Header("Scoring")]
        [Tooltip("Pixel threshold to consider ground (0..1).")]
        [Range(0, 1)] public float groundThreshold = 0.5f;
        [Tooltip("Exponent for vertical weighting (1 = linear, >1 = stronger emphasis near bottom).")]
        [Range(0.5f, 8f)] public float weightPower = 2.0f;

        [Header("Temporal filter & hysteresis")]
        [Range(0f, 1f)] public float dangerEmaAlpha = 0.25f; // 0=no smoothing, 1=no memory
        [Range(0f, 1f)] public float stopOn        = 0.55f;
        [Range(0f, 1f)] public float releaseAt     = 0.35f;
        [Range(0f, 1f)] public float speedFloor    = 0.25f;

        [Header("Events")]
        public UnityEvent<bool>  onStop     = new();
        public UnityEvent<float> onDanger   = new();
        public UnityEvent<string> onDebugText = new();

        [Header("Outputs (read-only)")]
        [TriInspectorMVCE.ReadOnly] public float dangerScore;
        [TriInspectorMVCE.ReadOnly] public float filteredDanger;
        [TriInspectorMVCE.ReadOnly] public float speedScale = 1f;
        [TriInspectorMVCE.ReadOnly] public bool  stop;

        // Internal state
        private bool _readbackPending;
        private int  _texW = 1, _texH = 1;
        private bool _lastStop;
        private bool _stopLatched;
        private bool _filteredInitialized;

        // ─────────────────────────────────────────────────────────────────────────────
        // Obstacle Mapping (unchanged)
        // ─────────────────────────────────────────────────────────────────────────────
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
        [Tooltip("Minimum consecutive frames required before enabling an obstacle.")]
        [Min(1)] public int framesOn = 3;
        [Tooltip("Frames to wait before disabling an obstacle once the hole disappears.")]
        [Min(1)] public int framesOff = 6;

        private readonly System.Collections.Generic.List<NavMeshObstacle> _obstacles = new();
        private float[] _sampleHoleWidth;
        private float[] _sampleHoleOffset;
        private bool[]  _sampleHasHole;
        private int[]   _seen;
        private int[]   _missing;

        // ─────────────────────────────────────────────────────────────────────────────
        // Cached trapezoid (computed per frame)
        // ─────────────────────────────────────────────────────────────────────────────
        private float _baseL01, _baseR01, _apexL01, _apexR01, _apexY01; // 0..1 image space (x from left, y from bottom)
        private bool  _runwayValid;

        // Gizmos
        [Header("Gizmos")]
        public bool drawGizmos = true;
        public Vector2 previewFrameSize = new(512, 512);
        public float gizmoZ = 0f;
        public float gizmoScale = 1f;
        public Color frameOutlineColor = new(1f, 1f, 1f, 0.4f);
        public Color zoneOutlineColor  = new(1f, 0.55f, 0f, 0.95f);
        public Color zoneFillOk        = new(0f, 1f, 0f, 0.15f);
        public Color zoneFillStop      = new(1f, 0f, 0f, 0.18f);
        public Color midlineColor      = new(1f, 1f, 1f, 0.25f);

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

        private Camera GetCamera()
        {
            if (sourceCamera) return sourceCamera;
            if (Camera.main) return Camera.main;
            return null; // will fallback to a default FOV below
        }

        private void OnMaskUpdated(RenderTexture mask)
        {
            _texW = Mathf.Max(1, mask.width);
            _texH = Mathf.Max(1, mask.height);

            if (_readbackPending) return;
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
                _sampleHoleWidth  = new float[n];
                _sampleHoleOffset = new float[n];
                _sampleHasHole    = new bool[n];
            }
            else
            {
                Array.Clear(_sampleHoleWidth,  0, _sampleHoleWidth.Length);
                Array.Clear(_sampleHoleOffset, 0, _sampleHoleOffset.Length);
                Array.Clear(_sampleHasHole,    0, _sampleHasHole.Length);
            }

            if (_seen == null || _seen.Length != n)
            {
                _seen    = new int[n];
                _missing = new int[n];
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Compute camera-based trapezoid in image-normalized space
        // ─────────────────────────────────────────────────────────────────────────────
        private void ComputeRunwayFromCamera(int w, int h)
        {
            var cam = GetCamera();

            // Vertical FOV in radians (use camera if present, else a sensible default)
            float vFovRad = Mathf.Deg2Rad * ((cam ? cam.fieldOfView : 60f));
            float aspectMask = (h > 0) ? (w / (float)h) : (cam ? cam.aspect : 1f);
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspectMask);

            float pitchRad = Mathf.Deg2Rad * pitchDegrees;

            // Horizon row y in [0..1] from bottom: y = (tan(beta)/tan(vfov/2) + 1)/2 with beta = +pitch
            float yNdcHorizon = Mathf.Tan(pitchRad) / Mathf.Tan(vFovRad * 0.5f); // [-1..+1]
            _apexY01 = 0.5f * (1f + yNdcHorizon);
            _apexY01 = Mathf.Clamp01(_apexY01);

            // Horizontal half-width at near/far (NDC) for a corridor width (meters)
            // x_ndc = (X/Z) / tan(hfov/2). For half corridor width X = W/2.
            float halfNearNdc = ((corridorWidthMeters * 0.5f) / Mathf.Max(0.001f, nearDistance)) / Mathf.Tan(hFovRad * 0.5f);
            float halfFarNdc  = ((corridorWidthMeters * 0.5f) / Mathf.Max(0.001f, farDistance )) / Mathf.Tan(hFovRad * 0.5f);

            // Clamp NDC to valid view
            halfNearNdc = Mathf.Clamp(halfNearNdc, 0f, 0.999f);
            halfFarNdc  = Mathf.Clamp(halfFarNdc,  0f, 0.999f);

            // Convert NDC [-1..+1] to image x [0..1]
            _baseL01 = 0.5f - 0.5f * halfNearNdc;
            _baseR01 = 0.5f + 0.5f * halfNearNdc;
            _apexL01 = 0.5f - 0.5f * halfFarNdc;
            _apexR01 = 0.5f + 0.5f * halfFarNdc;

            // Ensure sane ordering & non-degenerate apex height
            if (_apexR01 < _apexL01) { var t = _apexL01; _apexL01 = _apexR01; _apexR01 = t; }
            if (_baseR01 < _baseL01) { var t = _baseL01; _baseL01 = _baseR01; _baseR01 = t; }

            // If the horizon is at/below the bottom due to extreme pitch/FOV, cap at small height
            if (_apexY01 < 0.02f) _apexY01 = 0.02f;

            _runwayValid = true;
        }

        private void EvaluateRunway(NativeArray<byte> data, int w, int h)
        {
            if (!data.IsCreated || data.Length <= 0) return;
            EnsureSampleArrays();
            ComputeRunwayFromCamera(w, h);
            if (!_runwayValid) return;

            byte thrByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(groundThreshold) * 255f);

            double wSum = 0.0;
            double dangerSum = 0.0;

            // Scan rows from bottom up to the computed apex (horizon) row.
            int maxY = Mathf.Min(h - 1, Mathf.RoundToInt(_apexY01 * (h - 1)));
            for (int y = 0; y <= maxY; y++)
            {
                float v = (float)y / (h - 1);                 // 0..1 bottom->top
                float t = Mathf.Clamp01(v / _apexY01);        // 0..1 along runway height

                float lx01 = Mathf.Lerp(_baseL01, _apexL01, t);
                float rx01 = Mathf.Lerp(_baseR01, _apexR01, t);

                int x0 = Mathf.Clamp(Mathf.RoundToInt(lx01 * (w - 1)), 0, w - 1);
                int x1 = Mathf.Clamp(Mathf.RoundToInt(rx01 * (w - 1)), 0, w - 1);
                if (x1 < x0) { var tmp = x0; x0 = x1; x1 = tmp; }

                // Weight more heavily near the bottom
                float weight = Mathf.Pow(1f - v, weightPower);

                // Track largest contiguous non-ground run (hole) in this row
                int bestRun = 0, bestRunStart = -1;
                int run = 0, runStart = -1;

                for (int x = x0; x <= x1; x++)
                {
                    int idx = y * w + x;     // R8: one byte per pixel
                    bool isGround = data[idx] >= thrByte;

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
                float halfWidthPx = (x1 - x0 + 1) * 0.5f;
                if (bestRun > 0 && halfWidthPx > 0.5f)
                {
                    float holeCenterX = bestRunStart + bestRun * 0.5f;
                    float midX = (x0 + x1) * 0.5f;
                    float widthRatio = Mathf.Clamp01(bestRun / (float)(x1 - x0 + 1));
                    float lateral = Mathf.Clamp((float)((holeCenterX - midX) / halfWidthPx), -1f, 1f);

                    int si = Mathf.Clamp(Mathf.RoundToInt(t * (samples - 1)), 0, samples - 1);
                    if (!_sampleHasHole[si] || widthRatio > _sampleHoleWidth[si])
                    {
                        _sampleHasHole[si]  = true;
                        _sampleHoleWidth[si] = widthRatio;
                        _sampleHoleOffset[si] = lateral;
                    }
                }
            }

            float danger = wSum > 0.0 ? (float)(dangerSum / wSum) : 0f;
            danger = Mathf.Clamp01(danger);

            // Temporal filter + hysteresis + speed limiting (unchanged)
            dangerScore = danger;
            float alpha = Mathf.Clamp01(dangerEmaAlpha);
            if (!_filteredInitialized || alpha <= 0f) { filteredDanger = danger; _filteredInitialized = true; }
            else                                       filteredDanger = Mathf.Lerp(filteredDanger, danger, alpha);
            filteredDanger = Mathf.Clamp01(filteredDanger);
            onDanger?.Invoke(filteredDanger);

            float stopOnClamped    = Mathf.Clamp01(stopOn);
            float releaseAtClamped = Mathf.Clamp01(releaseAt);
            if (releaseAtClamped > stopOnClamped) releaseAtClamped = stopOnClamped;

            if (_stopLatched) { if (filteredDanger <= releaseAtClamped) _stopLatched = false; }
            else              { if (filteredDanger >= stopOnClamped)    _stopLatched = true;  }

            stop = _stopLatched;
            if (stop != _lastStop) { _lastStop = stop; onStop?.Invoke(stop); }

            if (stop) speedScale = 0f;
            else
            {
                float floor = Mathf.Clamp01(speedFloor);
                float t = Mathf.Approximately(stopOnClamped, releaseAtClamped)
                         ? (filteredDanger >= stopOnClamped ? 1f : 0f)
                         : Mathf.Clamp01(Mathf.InverseLerp(releaseAtClamped, stopOnClamped, filteredDanger));
                speedScale = Mathf.Lerp(1f, floor, t);
            }

            onDebugText?.Invoke($"runway(downPitch={pitchDegrees:F1}°, w={corridorWidthMeters:F2}m) " +
                                $"apexY={_apexY01:F2} danger(raw)={danger:F3} ema={filteredDanger:F3} stop={stop} scale={speedScale:F2}");

            UpdateObstaclesFromSamples();
        }

        private void UpdateObstaclesFromSamples()
        {
            if (_sampleHoleWidth == null) return;
            if (!createObstacles) { DisableAllObstacles(); return; }
            EnsureObstaclePool();

            int framesOnClamped  = Mathf.Max(1, framesOn);
            int framesOffClamped = Mathf.Max(1, framesOff);

            for (int i = 0; i < samples; i++)
            {
                bool has = _sampleHasHole[i] && _sampleHoleWidth[i] >= minHoleWidthRatio;
                var obs = _obstacles[i];
                if (!obs) continue;

                if (has) { _seen[i] = Mathf.Min(_seen[i] + 1, framesOnClamped); _missing[i] = 0; }
                else     { _missing[i] = Mathf.Min(_missing[i] + 1, framesOffClamped); }

                bool wasActive = obs.gameObject.activeSelf;
                bool shouldBeActive = (_seen[i] >= framesOnClamped) || (wasActive && _missing[i] < framesOffClamped);
                if (!shouldBeActive) { if (wasActive) obs.gameObject.SetActive(false); continue; }
                if (has && _seen[i] == framesOnClamped) _missing[i] = 0;

                float t = samples == 1 ? 0f : i / (float)(samples - 1);
                float dist = Mathf.Lerp(nearDistance, farDistance, t);
                float halfW = Mathf.Lerp(baseHalfWidthMeters, apexHalfWidthMeters, t);
                float offset = Mathf.Clamp(_sampleHoleOffset[i], -1f, 1f) * halfW;
                float widthMeters = Mathf.Max(0.01f, _sampleHoleWidth[i] * (halfW * 2f));

                var local = new Vector3(offset, obstacleLocalY, dist);
                var world = transform.TransformPoint(local);
                obs.transform.position = world;
                obs.transform.rotation = transform.rotation;
                if (!obs.gameObject.activeSelf) obs.gameObject.SetActive(true);

                if (obstacleAsBox)
                {
                    obs.shape = NavMeshObstacleShape.Box;
                    obs.size = new Vector3(widthMeters, Mathf.Max(0.01f, obstacleHeight), widthMeters);
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
            int needed = Mathf.Max(1, samples);
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
            for (int i = needed; i < _obstacles.Count; i++) if (_obstacles[i]) _obstacles[i].gameObject.SetActive(false);

            if (_seen == null || _seen.Length != needed) { _seen = new int[needed]; _missing = new int[needed]; }
        }

        private void DisableAllObstacles()
        {
            if (_obstacles == null) return;
            for (int i = 0; i < _obstacles.Count; i++) if (_obstacles[i]) _obstacles[i].gameObject.SetActive(false);
        }

        private void OnValidate()
        {
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

            int fw = (_texW > 1 || _texH > 1) ? _texW : Mathf.RoundToInt(Mathf.Max(1f, previewFrameSize.x));
            int fh = (_texW > 1 || _texH > 1) ? _texH : Mathf.RoundToInt(Mathf.Max(1f, previewFrameSize.y));

            // Recompute trapezoid using the best known dimensions
            ComputeRunwayFromCamera(fw, fh);

            // Frame outline
            Gizmos.color = frameOutlineColor;
            DrawRectOutline(new Rect(0, 0, 1, 1), gizmoScale, gizmoScale, gizmoZ);

            // Trapezoid corners in 0..1 image space
            Vector2 A = new(_baseL01, 0f);
            Vector2 B = new(_baseR01, 0f);
            Vector2 C = new(_apexR01, _apexY01);
            Vector2 D = new(_apexL01, _apexY01);

            // Fill
            Gizmos.color = _lastStop ? zoneFillStop : zoneFillOk;
            DrawQuadFill(A, B, C, D, gizmoScale, gizmoScale, gizmoZ);

            // Outline
            Gizmos.color = zoneOutlineColor;
            DrawQuadOutline(A, B, C, D, gizmoScale, gizmoScale, gizmoZ);

            // Midline (bottom center → apex mid)
            Gizmos.color = midlineColor;
            var mid0 = ToWorld(new Vector2(0.5f, 0f), gizmoScale, gizmoScale, gizmoZ);
            var mid1 = ToWorld(new Vector2(0.5f, _apexY01), gizmoScale, gizmoScale, gizmoZ);
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

        private void DrawQuadOutline(Vector2 a01, Vector2 b01, Vector2 c01, Vector2 d01, float sx, float sy, float z)
        {
            var A = ToWorld(a01, sx, sy, z);
            var B = ToWorld(b01, sx, sy, z);
            var C = ToWorld(c01, sx, sy, z);
            var D = ToWorld(d01, sx, sy, z);
            Gizmos.DrawLine(A, B);
            Gizmos.DrawLine(B, C);
            Gizmos.DrawLine(C, D);
            Gizmos.DrawLine(D, A);
        }

        private void DrawQuadFill(Vector2 a01, Vector2 b01, Vector2 c01, Vector2 d01, float sx, float sy, float z)
        {
            // Draw a thin cube over the AABB as a simple fill (approximation)
            float minX = Mathf.Min(Mathf.Min(a01.x, b01.x), Mathf.Min(c01.x, d01.x));
            float maxX = Mathf.Max(Mathf.Max(a01.x, b01.x), Mathf.Max(c01.x, d01.x));
            float minY = Mathf.Min(Mathf.Min(a01.y, b01.y), Mathf.Min(c01.y, d01.y));
            float maxY = Mathf.Max(Mathf.Max(a01.y, b01.y), Mathf.Max(c01.y, d01.y));
            var center = new Vector3((minX + maxX) * 0.5f * sx, (minY + maxY) * 0.5f * sy, z);
            var size   = new Vector3(Mathf.Max(0f, maxX - minX) * sx, Mathf.Max(0f, maxY - minY) * sy, 0.0001f);
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
