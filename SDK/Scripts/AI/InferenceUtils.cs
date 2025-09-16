#if MV_UNITY_AI_INFERENCE
using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.AI
{
    public enum InferenceBackendPreference
    {
        Auto,
        CPU,
        GPU
    }

    public static class InferenceUtils
    {
        private const string LogPrefix = "[InferenceUtils]";

        public static BackendType ChooseBestBackend(
            Model model,
            int minimumGpuMemoryMB,
            Func<Tensor<float>> createTestInput,
            out string reason,
            InferenceBackendPreference preference = InferenceBackendPreference.Auto,
            Action<string> logInfo = null,
            Action<string> logWarning = null)
        {
            logInfo    ??= Debug.Log;
            logWarning ??= Debug.LogWarning;

            LogGpuInfo(logInfo);

            reason = null;
            if (model == null)
            {
                reason = "Model is null.";
                logWarning?.Invoke($"{LogPrefix} Model is null. Defaulting to CPU backend.");
                return BackendType.CPU;
            }

            if (preference == InferenceBackendPreference.CPU)
            {
                reason = "Backend preference forces CPU.";
                logInfo?.Invoke($"{LogPrefix} Backend preference set to CPU. Skipping GPU capability tests.");
                return BackendType.CPU;
            }

            if (!SupportsGpuForInference(minimumGpuMemoryMB, out var supportMessage))
            {
                reason = supportMessage;
                logWarning?.Invoke($"{LogPrefix} GPU support check failed: {supportMessage}");
                return BackendType.CPU;
            }

            logInfo?.Invoke($"{LogPrefix} GPU support check passed.");

            var prefLabel = preference == InferenceBackendPreference.GPU ? " (forced)" : string.Empty;
            if (createTestInput == null)
                logInfo?.Invoke($"{LogPrefix} No test tensor provided; validating via worker instantiation only.");

            logInfo?.Invoke($"{LogPrefix} Running GPU backend validation{prefLabel}...");

            if (!TryTestGpuWorker(model, createTestInput, out var testMessage))
            {
                reason = testMessage;
                logWarning?.Invoke($"{LogPrefix} GPU backend validation failed: {testMessage}");
                return BackendType.CPU;
            }

            logInfo?.Invoke($"{LogPrefix} GPU backend validation succeeded.");
            return BackendType.GPUCompute;
        }

        public static BackendType ChooseBestBackend(Model model)
        {
            return ChooseBestBackend(model, 0, null, out _);
        }

        public static BackendType ChooseBestBackend(Model model, int minimumGpuMemoryMB, out string reason)
        {
            return ChooseBestBackend(model, minimumGpuMemoryMB, null, out reason);
        }

        public static bool SupportsGpuForInference(int minimumGpuMemoryMB, out string message)
        {
            message = null;

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                message = "GPU inference is not supported on WebGL.";
                return false;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                message = "Compute shaders are not supported on this device.";
                return false;
            }

            if (minimumGpuMemoryMB > 0)
            {
                var memoryMb = SystemInfo.graphicsMemorySize;
                if (memoryMb > 0 && memoryMb < minimumGpuMemoryMB)
                {
                    message = $"Detected only {memoryMb}MB of graphics memory. Minimum required is {minimumGpuMemoryMB}MB.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryTestGpuWorker(
            Model model,
            Func<Tensor<float>> createTestInput,
            out string message)
        {
            message = null;

            try
            {
                using var worker = new Worker(model, BackendType.GPUCompute);
                if (createTestInput != null)
                {
                    var tensor = createTestInput();
                    if (tensor == null)
                    {
                        message = "GPU backend validation failed: test input factory returned null.";
                        return false;
                    }

                    using (tensor)
                    {
                        worker.Schedule(tensor);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static void LogGpuInfo(Action<string> logInfo)
        {
            if (logInfo == null) return;

            var gpuInfo = $"GPU: {SystemInfo.graphicsDeviceName} | Vendor: {SystemInfo.graphicsDeviceVendor} | Type: {SystemInfo.graphicsDeviceType} | Memory: {SystemInfo.graphicsMemorySize}MB";
            logInfo.Invoke($"{LogPrefix} {gpuInfo}");
        }
    }
}
#endif
