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

namespace MetaverseCloudEngine.Unity.AI.Components
{
    /// <summary>
    /// YOLO runtime that emits UnityEvents when a configured label is detected.
    /// </summary>
    [HideMonoScript]
    [HelpURL("https://huggingface.co/unity/inference-engine-yolo")]
    public sealed class YouOnlyLookOnce : InferenceEngineComponent
    {
        [InfoBox("This component supports version 8-12 of the YOLO model. Please click the documentation link for model downloads and usage details.")]
        [Header("Embedded Resources (optional)")]
        public ModelAsset modelAsset;
        public string modelLocalPath;
        public TextAsset classesAsset;
        public string classesLocalPath;

        /// <summary>
        /// Input method for the YOLO model.
        /// </summary>
        [Header("Input Settings")]
        [Tooltip("Input method for the YOLO model. Choose between Texture, RawImage, or WebCamTexture.")]
        public InputMethod inputMethod = InputMethod.WebCamTexture;
        /// <summary>
        /// The texture to use as input for the YOLO model.
        /// </summary>
        [Tooltip("If Texture input method is selected, this texture will be used as input for the YOLO model.")]
        [ShowIf(nameof(inputMethod), InputMethod.Texture)]
        public Texture inputTexture;
        /// <summary>
        /// The RawImage to use as input for the YOLO model.
        /// </summary>
        [Tooltip("If RawImage input method is selected, this RawImage will be used as input for the YOLO model.")]
        [ShowIf(nameof(inputMethod), InputMethod.RawImage)]
        public RawImage inputRawImage;
        /// <summary>
        /// The name of the webcam to use as input for the YOLO model.
        /// </summary>
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("If WebCamTexture input method is selected, this name will be used to find the webcam. Leave empty to use the default webcam.")]
        public string webCamName;
        /// <summary>
        /// The width and height of the webcam texture to use as input for the YOLO model.
        /// </summary>
        [Tooltip("If WebCamTexture input method is selected, this width and height will be used for the webcam texture. Default is 640x480.")]
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        public int webCamWidth = 640;
        /// <summary>
        /// The height of the webcam texture to use as input for the YOLO model.
        /// </summary>
        [Tooltip("If WebCamTexture input method is selected, this height will be used for the webcam texture. Default is 480.")]
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        public int webCamHeight = 480;
        
        /// <summary>
        /// Whether to run the YOLO model in the Update loop.
        /// </summary>
        [Header("Detection Settings")]
        [Tooltip("Whether to run the YOLO model in the Update loop. If false, you must call Infer() manually.")]
        public bool runInUpdate = true;
        /// <summary>
        /// How often to perform inference on the YOLO model.
        /// </summary>
        [Tooltip("The rate at which the update runs in times per second. Set to 0 to run every frame.")]
        [Range(0, 30)]
        public int updatesPerSecond;
        /// <summary>
        /// Intersection over Union (IoU) for filtering detections.
        /// </summary>
        [Tooltip("Intersection over Union (IoU) threshold for filtering detections.")]
        [Range(0, 1)] public float iouThreshold = 0.5f;
        /// <summary>
        /// A score threshold for filtering detections.
        /// </summary>
        [Tooltip("Score threshold for filtering detections.")]
        [Range(0, 1)] public float scoreThreshold = 0.5f;

        /// <summary>
        /// List of label-event pairs.
        /// </summary>
        [PropertySpace(25, 25)]
        [Tooltip("List of label-event pairs. When a label is detected, the corresponding event will be invoked.")]
        public List<LabelEventPair> labelEvents = new();

        /// <summary>
        /// Input method for the YOLO model.
        /// </summary>
        public enum InputMethod
        {
            /// <summary>
            /// Use a Texture as input for the YOLO model.
            /// </summary>
            Texture,
            /// <summary>
            /// Use a RawImage as input for the YOLO model.
            /// </summary>
            RawImage,
            /// <summary>
            /// Use a WebCamTexture as input for the YOLO model.
            /// </summary>
            WebCamTexture
        }

        /// <summary>
        /// UnityEvent that is invoked when a label is detected.
        /// </summary>
        [Serializable]
        public class RectEvent : UnityEvent<Rect> { }

        /// <summary>
        /// Pair of label and the event to invoke when that label is detected.
        /// </summary>
        [Serializable]
        public class LabelEventPair
        {
            [Tooltip("Label to detect. Must match the labels in the classes file.")]
            public string label = "";
            [Tooltip("Event to invoke when the label is detected.")]
            public RectEvent onDetected = new();
            [Tooltip("Event to invoke when the label was originally detected but is no longer detected after the most recent inference.")]
            public UnityEvent onDetectionLost = new();

            /// <summary>
            /// A flag applied by the <see cref="YouOnlyLookOnce"/> code
            /// that allows the system to know whether this detection was
            /// already made.
            /// </summary>
            [NonSerialized]
            internal bool WasDetected;
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
            foreach (var p in labelEvents
                         .Where(p => !string.IsNullOrEmpty(p.label)))
                _eventLookup[p.label.Trim()] = p;
            _scratchRT = new RenderTexture(ModelInputWidth, ModelInputHeight, 0);
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
            if (_scratchRT)
                Destroy(_scratchRT);
        }

        private void Update()
        {
            if (!runInUpdate)
                return;
            var currentUnscaledTime = Time.unscaledTime;
            if (updatesPerSecond != 0 && currentUnscaledTime <= _nextUpdateTime)
                return;
            Infer();
            if (updatesPerSecond > 0)
                _nextUpdateTime = currentUnscaledTime + 1f / updatesPerSecond;
        }

        public void Infer()
        {
            if (_worker == null) return;

            var source = AcquireSourceTexture();
            if (!source || (_webCamTex && !_webCamTex.isPlaying)) return;

            Graphics.Blit(source, _scratchRT);

            using var input = new Tensor<float>(new TensorShape(1, 3, ModelInputHeight, ModelInputWidth));
            TextureConverter.ToTensor(_scratchRT, input);
            _worker.Schedule(input);

            using var boxes = (_worker.PeekOutput("output_0") as Tensor<float>)?.ReadbackAndClone();
            using var ids = (_worker.PeekOutput("output_1") as Tensor<int>)?.ReadbackAndClone();
            if (boxes == null || ids == null) return;

            DispatchEvents(boxes, ids, source.width, source.height);
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
                    if (filePaths.Length > 0)
                        Run(filePaths);
                    else
                        MetaverseProgram.Logger.LogError("Failed to fetch model resources.");
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
            if (fetchedPaths != null && fetchedPaths.TryGetFirstOrDefault(x => x.EndsWith(".names"), out var classesPath) &&
                !string.IsNullOrEmpty(classesPath))
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

            var backend = Application.platform == RuntimePlatform.WebGLPlayer
                ? BackendType.CPU
                : BackendType.GPUCompute;
            Model model = null;
            if (fetchedPaths != null && fetchedPaths.TryGetFirstOrDefault(
                    x => x.EndsWith(".onnx") || x.EndsWith(".sentis"), out var modelPath) &&
                !string.IsNullOrEmpty(modelPath))
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
                new[] { 1, 0, 1, 0, 0, 1, 0, 1, -0.5f, 0, 0.5f, 0, 0, -0.5f, 0, 0.5f });

            var graph = new FunctionalGraph();
            var input = graph.AddInputs(model);
            var pred = Functional.Forward(model, input)[0];

            var boxes = pred[0, 0..4, ..].Transpose(0, 1);
            var scoresAll = pred[0, 4.., ..];

            var scores = Functional.ReduceMax(scoresAll, 0);
            var classIds = Functional.ArgMax(scoresAll);
            var xyxy = Functional.MatMul(boxes, Functional.Constant(_centersToCorners));

            var keep = Functional.NMS(xyxy, scores, iouThreshold, scoreThreshold);
            var outBoxes = boxes.IndexSelect(0, keep);
            var outIds = classIds.IndexSelect(0, keep);

            _worker = new Worker(graph.Compile(outBoxes, outIds), backend);
        }

        private Texture AcquireSourceTexture()
        {
            return inputMethod switch
            {
                InputMethod.Texture => inputTexture,
                InputMethod.RawImage => inputRawImage ? inputRawImage.texture : null,
                InputMethod.WebCamTexture => _webCamTex,
                _ => null
            };
        }

        private void DispatchEvents(Tensor<float> boxes, Tensor<int> ids, int texW, int texH)
        {
            if (_detectedLabels.Count > 0)
                _detectedLabels.Clear();
            
            var count = boxes.shape[0];
            for (var i = 0; i < count; i++)
            {
                var id = ids[i];
                if (id < 0 || id >= _labels.Length)
                    continue;
                var label = _labels[id];
                if (!_eventLookup.TryGetValue(label, out var evt) || evt == null)
                    continue;
                var cx = boxes[i, 0] * texW;
                var cy = boxes[i, 1] * texH;
                var w = boxes[i, 2] * texW;
                var h = boxes[i, 3] * texH;
                var rect = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
                try
                {
                    evt.onDetected.Invoke(rect);
                    evt.WasDetected = true;
                    _detectedLabels.Add(label);
                }
                catch (Exception ex) { MetaverseProgram.Logger.LogError(ex); }
            }

            if (_detectedLabels.Count <= 0) return;
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
    public class YouOnlyLookOnce : InferenceEngineComponent
    {
    }
}
#endif