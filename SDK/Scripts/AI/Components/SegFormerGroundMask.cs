#if MV_UNITY_AI_INFERENCE
using TriInspectorMVCE;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections.Generic;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    public sealed class SegFormerGroundMask : InferenceEngineComponent
    {
        [Header("Model")]
        [Tooltip("Fused ONNX exported by export_segformer_b0_groundmask_onnx.py")]
        public ModelAsset modelAsset;
        [Tooltip("Optional: Local file path to the ONNX/Sentis model. If not set, a default cloud model will be fetched when needed.")]
        public string modelLocalPath;

        [Header("Input Settings")]
        public enum InputMethod { Texture, RawImage, WebCamTexture }
        [Tooltip("Input source for SegFormer model inference.")]
        public InputMethod inputMethod = InputMethod.WebCamTexture;

        [ShowIf(nameof(inputMethod), InputMethod.Texture)]
        [Tooltip("Texture source for model input when using Texture mode.")]
        public Texture inputTexture;

        [ShowIf(nameof(inputMethod), InputMethod.RawImage)]
        [Tooltip("RawImage source for model input when using RawImage mode.")]
        public RawImage inputRawImage;

        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("If WebCamTexture input is selected, this name will be used to find the webcam. Leave empty for default.")]
        public string webCamName;
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("Requested webcam width in pixels (WebCamTexture mode).")]
        public int webCamWidth = 640;
        [ShowIf(nameof(inputMethod), InputMethod.WebCamTexture)]
        [Tooltip("Requested webcam height in pixels (WebCamTexture mode).")]
        public int webCamHeight = 480;

        [Header("Output")]
        public int outputWidth  = 512;
        public int outputHeight = 512;
        public UnityEvent<RenderTexture> onMaskUpdated = new();

        [Header("Run Loop")]
        public bool runInUpdate = true;
        [Range(0, 60)] public int updatesPerSecond = 10;

        private Worker _worker;
        private RenderTexture _scratchRT;           // model input (512x512)
        private RenderTexture _maskRT;              // final mask (R8)
        private float _nextUpdateTime;
        private WebCamTexture _webCamTex;

        private const int InputHW = 512;            // matches exporter

        private void Awake()
        {
            // If neither embedded model nor local path is available, fetch from cloud, else run immediately.
            if (!modelAsset && (string.IsNullOrEmpty(modelLocalPath) || !File.Exists(modelLocalPath)))
            {
                FetchResources();
            }
            else
            {
                Run();
            }
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
            if (_scratchRT) Destroy(_scratchRT);
            if (_maskRT) Destroy(_maskRT);
            TryStopWebcam();
        }

        private void Update()
        {
            if (!runInUpdate) return;
            var t = Time.unscaledTime;
            if (updatesPerSecond != 0 && t <= _nextUpdateTime) return;
            Evaluate();
            if (updatesPerSecond > 0) _nextUpdateTime = t + 1f / Mathf.Max(1, updatesPerSecond);
        }

        public void Evaluate()
        {
            // Lazily start webcam in WebCamTexture mode
            if (inputMethod == InputMethod.WebCamTexture && _webCamTex == null)
                TryStartWebcam();

            if (_worker == null) return;

            var src = AcquireSourceTexture(out var invertX, out var invertY);
            if (!src || (_webCamTex && !_webCamTex.isPlaying)) return;

            if (_maskRT == null || _maskRT.width != outputWidth || _maskRT.height != outputHeight)
            {
                if (_maskRT) Destroy(_maskRT);
                _maskRT = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.R8)
                { filterMode = FilterMode.Point };
            }

            // Ensure scratch RT exists
            if (_scratchRT == null)
            {
                _scratchRT = new RenderTexture(InputHW, InputHW, 0, RenderTextureFormat.ARGB32)
                { filterMode = FilterMode.Bilinear };
            }

            // Resize + flip source → 512x512 on GPU
            Graphics.Blit(
                src,
                _scratchRT,
                scale: new Vector2(invertX ? -1 : 1, invertY ? -1 : 1),
                offset: Vector2.zero);

            // Upload to device tensor (NCHW) on GPU
            using var input = new Tensor<float>(new TensorShape(1, 3, InputHW, InputHW));
            TextureConverter.ToTensor(_scratchRT, input);     // stays on device

            // Run fused graph (normalization+argmax+upsample inside model)
            _worker.Schedule(input);

            // Get output tensor and render directly to RT (no Readback)
            var mask = _worker.PeekOutput("ground_mask") as Tensor<float>;
            if (mask != null)
            {
                TextureConverter.RenderToTexture(mask, _maskRT);
                onMaskUpdated?.Invoke(_maskRT);
            }
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

        private void FetchResources()
        {
            MetaverseResourcesAPI.Fetch(
                GetExternalDependencies(),
                "ComputerVision",
                filePaths =>
                {
                    if (!this) return;
                    if (filePaths != null && filePaths.Length > 0)
                    {
                        Run(filePaths);
                    }
                    else
                    {
                        MetaverseProgram.Logger.LogError("Failed to fetch SegFormer model resources.");
                    }
                });
        }

        private List<(MetaverseResourcesAPI.CloudResourcePath, string)> GetExternalDependencies()
        {
            var deps = new List<(MetaverseResourcesAPI.CloudResourcePath, string)>();
            // If neither embedded model nor local file exists, fetch default cloud model.
            if (!modelAsset && (string.IsNullOrEmpty(modelLocalPath) || !File.Exists(modelLocalPath)))
            {
                // Default cloud filename (adjust on server to match).
                deps.Add((MetaverseResourcesAPI.CloudResourcePath.AIModels, "segformer.b0.groundmask.onnx"));
            }
            return deps;
        }

        private void Run(string[] fetchedPaths = null)
        {
            // Select backend similar to YOLO component to support WebGL
            var backend = Application.platform == RuntimePlatform.WebGLPlayer ? BackendType.CPU : BackendType.GPUCompute;

            Model model = null;
            if (fetchedPaths != null && fetchedPaths.Length > 0)
            {
                foreach (var p in fetchedPaths)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    if (p.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".sentis", StringComparison.OrdinalIgnoreCase))
                    {
                        model = ModelLoader.Load(p);
                        break;
                    }
                }
            }
            else if (modelAsset)
            {
                model = ModelLoader.Load(modelAsset);
            }
            else if (!string.IsNullOrEmpty(modelLocalPath) && File.Exists(modelLocalPath))
            {
                model = ModelLoader.Load(modelLocalPath);
            }

            if (model == null)
            {
                MetaverseProgram.Logger.LogError("SegFormer model is missing or not found.");
                enabled = false;
                return;
            }

            _worker = new Worker(model, backend);

            if (_scratchRT == null)
            {
                _scratchRT = new RenderTexture(InputHW, InputHW, 0, RenderTextureFormat.ARGB32)
                { filterMode = FilterMode.Bilinear };
            }

            if (_maskRT == null)
            {
                _maskRT = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.R8)
                { filterMode = FilterMode.Point };
            }

            if (inputMethod == InputMethod.WebCamTexture)
            {
                TryStartWebcam();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Webcam helpers
        // ─────────────────────────────────────────────────────────────────────────────
        public bool TryStartWebcam()
        {
            try
            {
                if (_webCamTex != null && _webCamTex.isPlaying) return true;

                string devName = webCamName;
                if (string.IsNullOrEmpty(devName))
                {
                    var devices = WebCamTexture.devices;
                    if (devices != null && devices.Length > 0)
                    {
                        devName = devices[0].name;
                    }
                }

                if (string.IsNullOrEmpty(devName))
                {
                    Debug.LogWarning("No webcam devices found.");
                    return false;
                }

                int reqW = Mathf.Max(16, webCamWidth);
                int reqH = Mathf.Max(16, webCamHeight);
                _webCamTex = string.IsNullOrEmpty(devName) ? new WebCamTexture() : new WebCamTexture(devName);
                _webCamTex.requestedWidth = reqW;
                _webCamTex.requestedHeight = reqH;
                _webCamTex.Play();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to start webcam: {e.Message}");
                return false;
            }
        }

        private void TryStopWebcam()
        {
            try
            {
                if (_webCamTex != null)
                {
                    if (_webCamTex.isPlaying) _webCamTex.Stop();
                    Destroy(_webCamTex);
                    _webCamTex = null;
                }
            }
            catch { /* ignore */ }
        }
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class SegFormerGroundMask : InferenceEngineComponent {}
}
#endif
