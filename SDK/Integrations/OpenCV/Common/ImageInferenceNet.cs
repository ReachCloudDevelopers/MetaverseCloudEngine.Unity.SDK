#if !(PLATFORM_LUMIN && !UNITY_EDITOR) && (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV) 

#if !UNITY_WSA_10_0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using TriInspectorMVCE;
using UnityEngine.Events;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    /// <summary>
    /// A base class for image inference networks.
    /// </summary>
    [RequireComponent(typeof(ICameraFrameProvider))]
    public abstract class ImageInferenceNet : TriInspectorMonoBehaviour
    {
        protected interface IInferenceOutputData : IDisposable
        {
        }

        [Header("Image Inference Events")]
        [Tooltip("Invoked when the texture is created.")]
        public UnityEvent<Texture> onTextureCreated;

        /// <summary>
        /// The texture that we are performing inference on.
        /// </summary>
        public Texture2D Texture { get; private set; }

        /// <summary>
        /// Helps convert the texture to a matrix.
        /// </summary>
        protected ICameraFrameProvider TextureProvider;

        private readonly ConcurrentQueue<(IInferenceOutputData, Mat)> _outputDataQueue = new();

        private void Start()
        {
            TextureProvider = GetComponent<ICameraFrameProvider>();
            if (TextureProvider == null)
            {
                Debug.LogError("TextureToMatrix component not found.");
                enabled = false;
                return;
            }
            
            TextureProvider.Disposed += OnTexToMatDisposed;

            FetchResources();
        }

        protected void FetchResources()
        {
            MetaverseResourcesAPI.Fetch(
                GetRequiredAIModelDependencies().Select(n => (MetaverseResourcesAPI.CloudResourcePath.AIModels, name: n)).ToList(),
                "ComputerVision",
                filePaths =>
                {
                    if (filePaths.Length > 0)
                    {
                        Run(filePaths);
                    }
                    else
                    {
                        Debug.LogError("Failed to fetch model and classes files.");
                    }
                }
            );
        }

        protected virtual void OnDestroy()
        {
            TextureProvider.Dispose();
            Utils.setDebugMode(false);
        }

        protected virtual void LateUpdate()
        {
            if (_outputDataQueue.Count == 0) 
                return;
            if (_outputDataQueue.Count > 15)
            {
                while (_outputDataQueue.Count > 0)
                    if (_outputDataQueue.TryDequeue(out var d))
                    {
                        d.Item1.Dispose();
                        d.Item2.Dispose();
                    }
                return;
            }

            if (!_outputDataQueue.TryDequeue(out var data))
                return;

            try
            {
                if (data.Item2.cols() <= 0 || data.Item2.rows() <= 0)
                {
                    if (Texture && (Texture.width > 1 || Texture.height > 1))
                    {
                        Texture = Texture2D.whiteTexture;
                        onTextureCreated?.Invoke(Texture);
                    }
                    return;
                }
                
                if (!Texture || data.Item2.cols() != Texture.width || data.Item2.rows() != Texture.height)
                {
                    Texture = new Texture2D(data.Item2.cols(), data.Item2.rows(), TextureFormat.RGBA32, false);
                    onTextureCreated?.Invoke(Texture);
                }

                Utils.matToTexture2D(data.Item2, Texture, flip: true);
                OnMainThreadPostProcessInference(data.Item1);
            }
            finally
            {
                data.Item1.Dispose();
                data.Item2.Dispose();
            }
        }

        /// <summary>
        /// Use this method to specify the required AI model dependencies.
        /// </summary>
        /// <returns>The required AI model dependencies.</returns>
        protected abstract IEnumerable<string> GetRequiredAIModelDependencies();

        /// <summary>
        /// Called on the main thread after the inference has been performed.
        /// </summary>
        /// <param name="outputData"></param>
        protected virtual void OnMainThreadPostProcessInference(IInferenceOutputData outputData)
        {
        }

        /// <summary>
        /// Do the inference. Warning: This method is called on a separate thread.
        /// </summary>
        /// <param name="cameraFrame">The frame that is modified and will be visualized.</param>
        protected abstract (IInferenceOutputData, Mat) PerformInference(ICameraFrame cameraFrame);

        /// <summary>
        /// Called before the initialization.
        /// </summary>
        /// <param name="dependencies">The dependencies specified in <see cref="GetRequiredAIModelDependencies"/>.</param>
        /// <param name="error">The error to output if the method fails.</param>
        /// <returns>True if the initialization should proceed, false otherwise.</returns>
        protected abstract bool OnPreInitialize(string[] dependencies, out object error);

        /// <summary>
        /// Toggles the mobile camera.
        /// </summary>
        public void ToggleMobileCameraDirection()
        {
            if (!Application.isMobilePlatform)
                return;
            TextureProvider.RequestedIsFrontFacing = !TextureProvider.RequestedIsFrontFacing;
        }

        private void Run(string[] dependencies)
        {
            Utils.setDebugMode(true);

            if (!OnPreInitialize(dependencies, out var error))
            {
                MetaverseProgram.Logger.LogError(error);
                return;
            }

            if (!TextureProvider.IsInitialized() && !TextureProvider.IsInitializing())
            {
                TextureProvider.Initialize();
            }

            Task.Run(async () =>
            {
                while (!MetaverseProgram.IsQuitting)
                {
                    if (TextureProvider == null || !TextureProvider.IsStreaming())
                    {
                        await Task.Yield();
                        continue;
                    }

                    try
                    {
                        using var frame = TextureProvider.DequeueNextFrame();
                        if (frame is null)
                        {
                            await Task.Yield();
                            continue;
                        }

                        var outputData = PerformInference(frame);
                        _outputDataQueue.Enqueue(outputData);
                        await Task.Yield();
                    }
                    catch (ObjectDisposedException e)
                    {
                        // Camera was probably rotated or deactivated somehow.
                        MetaverseProgram.Logger.LogWarning(e);
                        await Task.Yield();
                    }
                    catch (Exception e)
                    {
                        MetaverseProgram.Logger.LogError(e);
                        throw;
                    }
                }
            }, destroyCancellationToken);
        }

        private void OnTexToMatDisposed()
        {
            if (Texture == null) return;
            Destroy(Texture);
            Texture = null;
        }
    }
}
#endif

#endif