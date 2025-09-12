#if MV_UNITY_AI_INFERENCE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TriInspectorMVCE;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.ObjectModel;
using UnityEngine.Rendering;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    public struct YoloDetection
    {
        public string Label;
        public Rect Rect;
        public float Score;
        public int   ClassId;

        public YoloDetection(string label, Rect rect, float score, int classId)
        {
            Label   = label;
            Rect    = rect;
            Score   = score;
            ClassId = classId;
        }
    }

    /// <summary>
    /// YOLO runtime that emits UnityEvents when a configured label is detected.
    /// </summary>
    [HideMonoScript]
    [HelpURL("https://huggingface.co/unity/inference-engine-yolo")]
    public sealed class YouOnlyLookOnce : InferenceEngineComponent
    {
        /// <summary>
        /// Event invoked every time a frame is processed, providing the list of detections and the width and height of the source texture.
        /// </summary>
        public event Action<IReadOnlyList<YoloDetection>, int, int> DetectionsFrame;

        /// <summary>
        /// Optional hook to post-process/adjust each detection (e.g., score calibration, label mapping, bbox tweaks).
        /// Return the (possibly) modified detection. If null, the original detection is used.
        /// </summary>
        public Func<YoloDetection, YoloDetection> PostProcessDetection;

        [InfoBox("This component supports version 8-12 of the YOLO model. Please click the documentation link for model downloads and usage details.")]
        [Header("Embedded Resources (optional)")]
        public ModelAsset modelAsset;
        public string modelLocalPath;
        public TextAsset classesAsset;
        public string classesLocalPath;

        [Header("Input Settings")]
        [Tooltip("Input method for the YOLO model. Choose between Texture, RawImage, or WebCamTexture.")]
        public InputMethod inputMethod = InputMethod.WebCamTexture;

        [ShowIf(nameof(inputMethod), InputMethod.Texture)]
        [Tooltip("If Texture input method is selected, this texture will be used as input for the YOLO model.")]
        public Texture inputTexture;

        [ShowIf(nameof(inputMethod), InputMethod.RawImage)]
        [Tooltip("If RawImage input method is selected, this RawImage will be used as input for the YOLO model.")]
        public RawImage inputRawImage;

        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("If WebCamTexture input method is selected, this name will be used to find the webcam. Leave empty to use the default webcam.")]
        public string webCamName;

        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("If WebCamTexture input method is selected, this width and height will be used for the webcam texture. Default is 640x480.")]
        public int webCamWidth = 640;

        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("If WebCamTexture input method is selected, this height will be used for the webcam texture. Default is 480.")]
        public int webCamHeight = 480;

        [Header("Detection Settings")]
        [Tooltip("Whether to run the YOLO model in the Update loop. If false, you must call Infer() manually.")]
        public bool runInUpdate = true;

        [Tooltip("The rate at which the update runs in times per second. Set to 0 to run every frame.")]
        [Range(0, 30)]
        public int updatesPerSecond = 5;

        [Tooltip("Intersection over Union (IoU) threshold for filtering detections.")]
        [Range(0, 1)] public float iouThreshold = 0.5f;

        [Tooltip("Score threshold for filtering detections.")]
        [Range(0, 1)] public float scoreThreshold = 0.5f;

        [PropertySpace(25, 25)]
        [Tooltip("List of label-event pairs. When a label is detected, the corresponding event will be invoked.")]
        public List<LabelEventPair> labelEvents = new();

        [Header("Overlay Output")]
        [Tooltip("Overlay RenderTexture with 2D boxes + labels over transparent background.")]
        public UnityEvent<RenderTexture> onOverlayUpdated = new();
        [Tooltip("Box line thickness in pixels.")]
        [Range(1, 12)] public int overlayLineThickness = 2;
        [Tooltip("Box color for overlay.")]
        public Color overlayBoxColor = new Color(0.1f, 1f, 0.1f, 1f);
        [Tooltip("Text color for overlay labels.")]
        public Color overlayTextColor = Color.white;
        [Tooltip("Optional font used to draw label text. If null, text is omitted.")]
        public Font overlayFont;
        [Tooltip("Font size for overlay label text.")]
        [Range(8, 48)] public int overlayFontSize = 16;
        [Tooltip("Render overlay text with pixel-snapped coordinates and point-filtered textures to avoid waviness.")]
        public bool overlayCrispText = true;
        [Tooltip("Snap only the baseline to integer pixels (keeps AA while preventing vertical wobble).")]
        public bool overlaySnapBaseline = true;
        [Tooltip("Invert X for overlay drawing only (flips detection rect horizontally without altering the source image).")]
        public bool overlayInvertX = false;
        [Tooltip("Invert Y for overlay drawing only (flips detection rect vertically without altering the source image).")]
        public bool overlayInvertY = false;

        public enum InputMethod { Texture, RawImage, WebCamTexture }

        [Serializable] public class RectEvent : UnityEvent<Rect> { }

        [Serializable]
        public class LabelEventPair
        {
            [Tooltip("Label to detect. Must match the labels in the classes file.")]
            public string label = "";
            [Range(0, 1)]
            [Tooltip("Minimum score threshold [0..1] for this label to trigger events. This is in addition to the global score threshold.")]
            public float scoreFilter = 0.5f;
            [Tooltip("Event to invoke when the detection has started.")]
            public UnityEvent onDetectionBegan = new();
            [Tooltip("Event to invoke when the label is detected.")]
            public RectEvent onDetected = new();
            [Tooltip("Event to invoke when the label was originally detected but is no longer detected after the most recent inference.")]
            public UnityEvent onDetectionLost = new();

            [NonSerialized] internal bool WasDetected;
        }

        private const int ModelInputWidth = 640;
        private const int ModelInputHeight = 640;

        private Worker _worker;
        private Tensor<float> _centersToCorners;
        private string[] _labels;
        private RenderTexture _scratchRT;
        private RenderTexture _overlayRT;
        private WebCamTexture _webCamTex;
        private readonly Dictionary<string, LabelEventPair> _eventLookup = new();
        private readonly HashSet<string> _detectedLabels = new();
        private float _nextUpdateTime;
        private static Material _lineMaterial;

        private void Awake()
        {
            foreach (var p in labelEvents.Where(p => !string.IsNullOrEmpty(p.label)))
                _eventLookup[p.label.Trim()] = p;
            if (!modelAsset || !classesAsset) FetchResources();
            else Run();
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
            _centersToCorners?.Dispose();
            if (_webCamTex)
            {
                _webCamTex.Stop();
                Destroy(_webCamTex);
            }
            if (_scratchRT) Destroy(_scratchRT);
            if (_overlayRT) Destroy(_overlayRT);
        }

        private void Update()
        {
            if (!runInUpdate) return;
            var t = Time.unscaledTime;
            if (updatesPerSecond != 0 && t <= _nextUpdateTime) return;
            Infer();
            if (updatesPerSecond > 0) _nextUpdateTime = t + 1f / updatesPerSecond;
        }

        public void Infer()
        {
            if (_worker == null) return;

            var source = AcquireSourceTexture(out var invertX, out var invertY);
            if (!source || (_webCamTex && !_webCamTex.isPlaying)) return;

            if (!_scratchRT)
                _scratchRT = new RenderTexture(ModelInputWidth, ModelInputHeight, 0);

            Graphics.Blit(
                source, 
                _scratchRT, 
                scale: new Vector2(invertX ? -1 : 1, invertY ? -1 : 1), 
                offset: Vector2.zero);

            using var input = new Tensor<float>(new TensorShape(1, 3, ModelInputHeight, ModelInputWidth));
            TextureConverter.ToTensor(_scratchRT, input);
            _worker.Schedule(input);

            using var boxes  = (_worker.PeekOutput("output_0") as Tensor<float>)?.ReadbackAndClone();
            using var ids    = (_worker.PeekOutput("output_1") as Tensor<int>)?.ReadbackAndClone();
            using var scores = (_worker.PeekOutput("output_2") as Tensor<float>)?.ReadbackAndClone();

            if (boxes == null || ids == null || scores == null) return;

            DispatchEvents(boxes, ids, scores, source.width, source.height);
            RenderOverlay(boxes, ids, scores, source.width, source.height, invertX, invertY);
        }

        private void FetchResources()
        {
            MetaverseResourcesAPI.Fetch(
                GetExternalDependencies()
                    .Select(n => (MetaverseResourcesAPI.CloudResourcePath.AIModels, name: n))
                    .ToList(),
                "ComputerVision",
                filePaths =>
                {
                    if (!this) return;
                    if (filePaths.Length > 0) Run(filePaths);
                    else MetaverseProgram.Logger.LogError("Failed to fetch model resources.");
                });
        }

        private IEnumerable<string> GetExternalDependencies()
        {
            var deps = new List<string>();
            if (!modelAsset && !File.Exists(modelLocalPath))
                deps.Add("yolo.v8.classification.onnx");
            if (!classesAsset && !File.Exists(classesLocalPath))
                deps.Add("yolo.v8.coco.names");
            return deps;
        }

        private void Run(string[] fetchedPaths = null)
        {
            string classesText;
            if (fetchedPaths != null && fetchedPaths.TryGetFirstOrDefault(x => x.EndsWith(".names"), out var classesPath) && !string.IsNullOrEmpty(classesPath))
                classesText = File.ReadAllText(classesPath);
            else if (classesAsset)
                classesText = classesAsset.text;
            else if (!string.IsNullOrEmpty(classesLocalPath) && File.Exists(classesLocalPath))
                classesText = File.ReadAllText(classesLocalPath);
            else
            {
                MetaverseProgram.Logger.LogError("Classes file is missing or not found.");
                return;
            }

            if (string.IsNullOrEmpty(classesText))
            {
                MetaverseProgram.Logger.LogError("Classes file is empty or not found.");
                return;
            }

            _labels = classesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var backend = Application.platform == RuntimePlatform.WebGLPlayer ? BackendType.CPU : BackendType.GPUCompute;
            Model model = null;
            if (fetchedPaths != null && fetchedPaths.TryGetFirstOrDefault(x => x.EndsWith(".onnx") || x.EndsWith(".sentis"), out var modelPath) && !string.IsNullOrEmpty(modelPath))
                model = ModelLoader.Load(modelPath);
            else if (modelAsset)
                model = ModelLoader.Load(modelAsset);
            else if (!string.IsNullOrEmpty(modelLocalPath) && File.Exists(modelLocalPath))
                model = ModelLoader.Load(modelLocalPath);

            if (model == null)
            {
                MetaverseProgram.Logger.LogError("Model is missing or not found.");
                return;
            }

            BuildWorker(model, backend);

            if (inputMethod != InputMethod.WebCamTexture) return;
            _webCamTex = new WebCamTexture(webCamName, webCamWidth, webCamHeight);
            _webCamTex.Play();
        }

        private void BuildWorker(Model model, BackendType backend)
        {
            _centersToCorners = new Tensor<float>(new TensorShape(4, 4),
                new[] {
                    1, 0, 1, 0,
                    0, 1, 0, 1,
                   -0.5f, 0, 0.5f, 0,
                    0, -0.5f, 0, 0.5f
                });

            var graph = new FunctionalGraph();
            var input = graph.AddInputs(model);
            var pred  = Functional.Forward(model, input)[0];

            // pred: [B=1, C, N] with C=(4 + numClasses), N=numBoxes
            var boxes     = pred[0, 0..4, ..].Transpose(0, 1); // [N,4] cx,cy,w,h
            var scoresAll = pred[0, 4.., ..];                  // [numClasses, N]

            var scores   = Functional.ReduceMax(scoresAll, 0); // [N]
            var classIds = Functional.ArgMax(scoresAll);       // [N]
            var xyxy     = Functional.MatMul(boxes, Functional.Constant(_centersToCorners)); // [N,4]

            var keep     = Functional.NMS(xyxy, scores, iouThreshold, scoreThreshold);

            var outBoxes  = boxes.IndexSelect(0, keep);   // [K,4]
            var outIds    = classIds.IndexSelect(0, keep); // [K]
            var outScores = scores.IndexSelect(0, keep);   // [K]

            // Compile three outputs so we can read scores later.
            _worker = new Worker(graph.Compile(outBoxes, outIds, outScores), backend);
        }

        private Texture AcquireSourceTexture(out bool invertX, out bool invertY)
        {
            invertX = false;
            invertY = false;
            switch (inputMethod)
            {
                case InputMethod.Texture:
                    return inputTexture;
                case InputMethod.RawImage:
                    if (inputRawImage)
                    {
                        if (inputRawImage.transform.localScale.y < 0) invertY = true;
                        if (inputRawImage.transform.localScale.x < 0) invertX = true;
                        return inputRawImage.texture;
                    }
                    return null;
                case InputMethod.WebCamTexture:
                    return _webCamTex;
                default:
                    return null;
            }
        }

        private void DispatchEvents(Tensor<float> boxes, Tensor<int> ids, Tensor<float> scores, int texW, int texH)
        {
            if (_detectedLabels.Count > 0) _detectedLabels.Clear();

            var frame = new List<YoloDetection>(Mathf.Max(16, boxes.shape[0]));
            var count = boxes.shape[0];
            for (var i = 0; i < count; i++)
            {
                var classId = ids[i];
                if (classId < 0 || classId >= _labels.Length) continue;

                var label = _labels[classId];
                if (!_eventLookup.TryGetValue(label, out var evt) || evt == null)
                    continue;
                
                if (scores[i] < evt.scoreFilter)
                    continue;

                var cx = boxes[i, 0];
                var cy = boxes[i, 1];
                var w  = boxes[i, 2];
                var h  = boxes[i, 3];

                var yMin = ModelInputHeight - (cy + h * 0.5f);
                var xMin = cx - w * 0.5f;

                var rect = new Rect(xMin, yMin, w, h);
                var det  = new YoloDetection(label, rect, scores[i], classId);

                if (PostProcessDetection != null)
                {
                    try { det = PostProcessDetection(det); }
                    catch (Exception ex) { MetaverseProgram.Logger.LogError(ex); }
                }

                frame.Add(det);

                try
                {
                    if (_detectedLabels.Add(label))
                    {
                        if (!evt.WasDetected)
                        {
                            evt.onDetectionBegan?.Invoke();
                            evt.WasDetected = true;
                        }
                    }
                    evt.onDetected.Invoke(det.Rect);
                }
                catch (Exception ex) { MetaverseProgram.Logger.LogError(ex); }
            }

            DetectionsFrame?.Invoke(new ReadOnlyCollection<YoloDetection>(frame), texW, texH);

            // Handle "lost" events
            for (var i = labelEvents.Count - 1; i >= 0; i--)
            {
                var l = labelEvents[i];
                if (!l.WasDetected || _detectedLabels.Contains(l.label)) continue;
                l.WasDetected = false;
                l.onDetectionLost?.Invoke();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Overlay Rendering
        // ─────────────────────────────────────────────────────────────────────────────
        private void EnsureLineMaterial()
        {
            if (_lineMaterial != null) return;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader)
            {
                MetaverseProgram.Logger.LogError("Missing shader Hidden/Internal-Colored for overlay rendering.");
                return;
            }
            _lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        private void RenderOverlay(Tensor<float> boxes, Tensor<int> ids, Tensor<float> scores, int texW, int texH, bool invertX, bool invertY)
        {
            EnsureLineMaterial();
            if (_lineMaterial == null) return;

            // Ensure overlay texture matches source size
            if (_overlayRT == null || _overlayRT.width != texW || _overlayRT.height != texH)
            {
                if (_overlayRT) Destroy(_overlayRT);
                _overlayRT = new RenderTexture(texW, texH, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = overlayCrispText ? FilterMode.Point : FilterMode.Bilinear
                };
                _overlayRT.Create();
            }

            // Clear to transparent and set pixel matrix
            var prevActive = RenderTexture.active;
            RenderTexture.active = _overlayRT;
            _overlayRT.filterMode = overlayCrispText ? FilterMode.Point : FilterMode.Bilinear;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, texW, texH, 0);

            // Draw boxes
            _lineMaterial.SetPass(0);
            var thickness = Mathf.Max(1, overlayLineThickness);
            var scaleX = texW / (float)ModelInputWidth;
            var scaleY = texH / (float)ModelInputHeight;

            var count = boxes.shape[0];
            for (var i = 0; i < count; i++)
            {
                var classId = ids[i];
                if (classId < 0 || classId >= _labels.Length) continue;
                var label = _labels[classId];

                // Apply either per-label filter (if configured) or global threshold
                if (_eventLookup.TryGetValue(label, out var evt) && evt != null)
                {
                    if (scores[i] < Mathf.Max(0.0001f, evt.scoreFilter)) continue;
                }
                else
                {
                    continue;
                }

                var cx = boxes[i, 0];
                var cy = boxes[i, 1];
                var w  = boxes[i, 2];
                var h  = boxes[i, 3];

                var yMin = ModelInputHeight - (cy + h * 0.5f);
                var xMin = cx - w * 0.5f;

                // Scale to source texture dimensions
                var x = xMin * scaleX;
                var y = yMin * scaleY;
                var rw = w * scaleX;
                var rh = h * scaleY;

                // Apply same mirror used when blitting into the model input
                if (invertX)
                    x = texW - (x + rw);
                if (invertY)
                    y = texH - (y + rh);

                // Apply user-controlled overlay inversions (do not touch source texture)
                if (overlayInvertX)
                    x = texW - (x + rw);
                if (overlayInvertY)
                    y = texH - (y + rh);

                // Ensure the untextured colored material is bound before drawing the box.
                // DrawText() below binds the font's textured material, so we must rebind per detection.
                _lineMaterial.SetPass(0);
                DrawRectOutline(new Rect(x, y, rw, rh), thickness, overlayBoxColor);

                // Draw label text if a font is available
                if (overlayFont && !string.IsNullOrEmpty(label))
                {
                    DrawText(label, (int)x + 2, (int)(y + 2), overlayTextColor, overlayFontSize);
                }
            }

            GL.PopMatrix();
            RenderTexture.active = prevActive;

            onOverlayUpdated?.Invoke(_overlayRT);
        }

        private static void Quad(float x0, float y0, float x1, float y1, Color c)
        {
            GL.Begin(GL.QUADS);
            GL.Color(c);
            GL.Vertex3(x0, y0, 0);
            GL.Vertex3(x1, y0, 0);
            GL.Vertex3(x1, y1, 0);
            GL.Vertex3(x0, y1, 0);
            GL.End();
        }

        private void DrawRectOutline(Rect r, int t, Color c)
        {
            var x0 = r.xMin;
            var y0 = r.yMin;
            var x1 = r.xMax;
            var y1 = r.yMax;
            // top
            Quad(x0, y0, x1, y0 + t, c);
            // bottom
            Quad(x0, y1 - t, x1, y1, c);
            // left
            Quad(x0, y0, x0 + t, y1, c);
            // right
            Quad(x1 - t, y0, x1, y1, c);
        }

        private void DrawText(string text, int x, int y, Color color, int size)
        {
            var font = overlayFont;
            if (!font) return;

            var mat = font.material;
            if (!mat) return;
            mat.color = Color.white; // use glyph color, we tint via GL.Color
            mat.SetPass(0);

            if (mat.mainTexture)
            {
                if (overlayCrispText)
                {
                    mat.mainTexture.filterMode = FilterMode.Point; // avoid shimmering/wavy sampling
                    mat.mainTexture.anisoLevel = 0;
                }
                else
                {
                    mat.mainTexture.filterMode = FilterMode.Bilinear; // smoother edges with stable baseline
                }
            }

            font.RequestCharactersInTexture(text, size, FontStyle.Normal);

            // Compute a consistent baseline using the font ascender.
            // TextGenerator does this internally, but we're drawing via GL.
            var ascender = font.ascent; // normalized in some Unity versions; multiply by size to get pixels
            float baseline = y + ascender * size;
            if (overlaySnapBaseline) baseline = Mathf.Round(baseline);

            int cursorX = x;
            for (int i = 0; i < text.Length; i++)
            {
                if (!font.GetCharacterInfo(text[i], out var ch, size, FontStyle.Normal))
                    continue;

                // Use min/max relative to baseline to avoid per-glyph rounding drift.
                float vx0 = cursorX + ch.minX;
                float vx1 = cursorX + ch.maxX;
                float vy0 = baseline + ch.minY;
                float vy1 = baseline + ch.maxY;

                if (overlayCrispText)
                {
                    vx0 = Mathf.Round(vx0); vx1 = Mathf.Round(vx1);
                    vy0 = Mathf.Round(vy0); vy1 = Mathf.Round(vy1);
                }

                GL.Begin(GL.QUADS);
                GL.Color(color);
                GL.TexCoord2(ch.uvBottomLeft.x, ch.uvBottomLeft.y); GL.Vertex3(vx0, vy0, 0);
                GL.TexCoord2(ch.uvBottomRight.x, ch.uvBottomRight.y); GL.Vertex3(vx1, vy0, 0);
                GL.TexCoord2(ch.uvTopRight.x, ch.uvTopRight.y); GL.Vertex3(vx1, vy1, 0);
                GL.TexCoord2(ch.uvTopLeft.x, ch.uvTopLeft.y); GL.Vertex3(vx0, vy1, 0);
                GL.End();

                cursorX += ch.advance;
            }
        }
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class YouOnlyLookOnce : InferenceEngineComponent {}
}
#endif
