#if MV_UNITY_AI_INFERENCE
using System;
using System.Collections.Generic;
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
        public TextAsset classesAsset;

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
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        public string webCamName;
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        public int webCamWidth = 640;
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        public int webCamHeight = 480;
        
        /// <summary>
        /// Intersection over Union (IoU) and score thresholds for filtering detections.
        /// </summary>
        [Header("Detection Settings")]
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
            Texture,
            RawImage,
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
        }

        private const int ModelInputWidth = 640;
        private const int ModelInputHeight = 640;

        private Worker _worker;
        private Tensor<float> _centersToCorners;
        private string[] _labels;
        private RenderTexture _scratchRT;
        private WebCamTexture _webCamTex;
        private readonly Dictionary<string, RectEvent> _eventLookup = new();

        private void Awake()
        {
            foreach (var p in labelEvents
                         .Where(p => !string.IsNullOrEmpty(p.label) && p.onDetected != null))
                _eventLookup[p.label.Trim()] = p.onDetected;
            _scratchRT = new RenderTexture(ModelInputWidth, ModelInputHeight, 0);
            if (!modelAsset || !classesAsset) FetchResources();
            else Run();
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
            _centersToCorners?.Dispose();
            if (_webCamTex != null)
            {
                _webCamTex.Stop();
                Destroy(_webCamTex);
            }
            Destroy(_scratchRT);
        }

        private void Update()
        {
            if (_worker == null) return;

            var source = AcquireSourceTexture();
            if (!source) return;

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
                GetFallbackDependencies().Select(n => (MetaverseResourcesAPI.CloudResourcePath.AIModels, name: n)).ToList(),
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

        private IEnumerable<string> GetFallbackDependencies()
        {
            var deps = new List<string>();
            if (!modelAsset)
                deps.Add("yolo.v8.classification.onnx");
            if (!classesAsset)
                deps.Add("yolo.v8.coco.names");
            return deps;
        }

        private void Run(string[] fetchedPaths = null)
        {
            string classesText;
            if (fetchedPaths.TryGetFirstOrDefault(x => x.EndsWith(".names"), out var classesPath) &&
                !string.IsNullOrEmpty(classesPath))
                classesText = System.IO.File.ReadAllText(classesPath);
            else if (classesAsset)
                classesText = classesAsset.text;
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
            var model = fetchedPaths.TryGetFirstOrDefault(x => x.EndsWith(".onnx"), out var modelPath) && 
                        !string.IsNullOrEmpty(modelPath)
                ? ModelLoader.Load(modelPath)
                : ModelLoader.Load(modelAsset);
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
            var count = boxes.shape[0];
            for (var i = 0; i < count; i++)
            {
                var id = ids[i];
                if (id < 0 || id >= _labels.Length) continue;

                var label = _labels[id];
                if (!_eventLookup.TryGetValue(label, out var evt) || evt == null) continue;

                var cx = boxes[i, 0] * texW;
                var cy = boxes[i, 1] * texH;
                var w = boxes[i, 2] * texW;
                var h = boxes[i, 3] * texH;

                var rect = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
                try { evt.Invoke(rect); }
                catch (Exception ex) { MetaverseProgram.Logger.LogError(ex); }
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