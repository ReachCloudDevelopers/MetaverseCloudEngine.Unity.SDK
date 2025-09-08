#if MV_UNITY_AI_INFERENCE
using TriInspectorMVCE;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    [HideMonoScript]
    public sealed class SegFormerGroundMask : InferenceEngineComponent
    {
        [Header("Model")]
        [Tooltip("Fused ONNX exported by export_segformer_b0_groundmask_onnx.py")]
        public ModelAsset modelAsset;

        [Header("Source")]
        public Texture sourceTexture;
        public RawImage sourceRawImage;

        [Header("Output")]
        public int outputWidth  = 512;
        public int outputHeight = 512;
        public UnityEvent<RenderTexture> onMaskUpdated;

        [Header("Run Loop")]
        public bool runInUpdate = true;
        [Range(0, 60)] public int updatesPerSecond = 30;

        private Worker _worker;
        private RenderTexture _scratchRT;           // model input (512x512)
        private RenderTexture _maskRT;              // final mask (R8)
        private float _nextUpdateTime;

        private const int InputHW = 512;            // matches exporter

        void Awake()
        {
            if (!modelAsset) { Debug.LogError("ModelAsset missing."); enabled = false; return; }

            var model = ModelLoader.Load(modelAsset);
            _worker = new Worker(model, BackendType.GPUCompute);

            _scratchRT = new RenderTexture(InputHW, InputHW, 0, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Bilinear };

            _maskRT = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.R8)
            { filterMode = FilterMode.Point };
        }

        void OnDestroy()
        {
            _worker?.Dispose();
            if (_scratchRT) Destroy(_scratchRT);
            if (_maskRT) Destroy(_maskRT);
        }

        void Update()
        {
            if (!runInUpdate) return;
            var t = Time.unscaledTime;
            if (updatesPerSecond != 0 && t <= _nextUpdateTime) return;
            Evaluate();
            if (updatesPerSecond > 0) _nextUpdateTime = t + 1f / Mathf.Max(1, updatesPerSecond);
        }

        public void Evaluate()
        {
            var src = sourceTexture ? sourceTexture : (sourceRawImage ? sourceRawImage.texture : null);
            if (!src || _worker == null) return;

            if (_maskRT.width != outputWidth || _maskRT.height != outputHeight)
            {
                if (_maskRT) Destroy(_maskRT);
                _maskRT = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.R8)
                { filterMode = FilterMode.Point };
            }

            // Resize source → 512x512 on GPU
            Graphics.Blit(src, _scratchRT);

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
    }
}
#else
namespace MetaverseCloudEngine.Unity.AI.Components
{
    public class SegFormerGroundMask : InferenceEngineComponent {}
}
#endif