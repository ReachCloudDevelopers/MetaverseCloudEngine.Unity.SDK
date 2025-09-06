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
        public int updatesPerSecond;

        [Tooltip("Intersection over Union (IoU) threshold for filtering detections.")]
        [Range(0, 1)] public float iouThreshold = 0.5f;

        [Tooltip("Score threshold for filtering detections.")]
        [Range(0, 1)] public float scoreThreshold = 0.5f;

        [PropertySpace(25, 25)]
        [Tooltip("List of label-event pairs. When a label is detected, the corresponding event will be invoked.")]
        public List<LabelEventPair> labelEvents = new();

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
        private WebCamTexture _webCamTex;
        private readonly Dictionary<string, LabelEventPair> _eventLookup = new();
        private readonly HashSet<string> _detectedLabels = new();
        private float _nextUpdateTime;

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
                var rect = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
                var det = new YoloDetection(label, rect, scores[i], classId); // <-- score included

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
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class YouOnlyLookOnce : InferenceEngineComponent {}
}
#endif
