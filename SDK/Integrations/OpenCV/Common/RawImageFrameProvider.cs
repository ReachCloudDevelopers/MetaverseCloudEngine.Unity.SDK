#if (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV)

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using System;
using System.Collections;
using OpenCVForUnity.UnityUtils;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI; // Required for RawImage

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    [HideMonoScript]
    [DeclareFoldoutGroup("RawImage Source Options")]
    [DeclareFoldoutGroup("Additional Metadata")]
    public class RawImageFrameProvider : TriInspectorMonoBehaviour, ICameraFrameProvider
    {
        [Tooltip("The RawImage component whose texture will be used as the source.")]
        [Required]
        [SerializeField]
        private RawImage sourceRawImage;

        [Tooltip("If true, initializes the source on start.")]
        [SerializeField]
        private bool initOnStart = true;

        [Group("Additional Metadata")]
        [Min(0)]
        [SerializeField]
        private float fieldOfView = 60f;

        [Tooltip("Since RawImage doesn't provide depth, depth can be simulated by this constant.")]
        [Min(0)]
        [SerializeField]
        [Group("Additional Metadata")]
        private float defaultDepthOffset = 1f;

        [Tooltip("Determines if flips vertically before returning the Mat.")]
        [SerializeField]
        [Group("RawImage Source Options")]
        protected bool _flipVertical = false;

        public virtual bool flipVertical
        {
            get { return _flipVertical; }
            set { _flipVertical = value; }
        }

        [Tooltip("Determines if flips horizontally before returning the Mat.")]
        [SerializeField]
        [Group("RawImage Source Options")]
        protected bool _flipHorizontal = false;

        public virtual bool flipHorizontal
        {
            get { return _flipHorizontal; }
            set { _flipHorizontal = value; }
        }

        [Tooltip("Select the output color format for the Mat.")]
        [SerializeField]
        [Group("RawImage Source Options")]
        protected ColorFormat _outputColorFormat = ColorFormat.RGBA;

        public virtual ColorFormat outputColorFormat
        {
            get { return _outputColorFormat; }
            set
            {
                if (_outputColorFormat != value)
                {
                    _outputColorFormat = value;
                    // Reinitialize Mat if format changes
                    if (hasInitDone)
                        InitializeMats();
                }
            }
        }

        /// <summary>
        /// UnityEvent that is triggered when this instance is initialized.
        /// </summary>
        public event Action Initialized;

        /// <summary>
        /// UnityEvent that is triggered when this instance is disposed.
        /// </summary>
        public event Action Disposed;

        /// <summary>
        /// The texture read from the RawImage.
        /// </summary>
        protected Texture sourceTexture;

        /// <summary>
        /// Intermediate Texture2D used for conversion.
        /// </summary>
        protected Texture2D intermediateTexture2D;

        /// <summary>
        /// The frame mat.
        /// </summary>
        protected Mat frameMat;

        /// <summary>
        /// The base mat (usually RGBA from the texture).
        /// </summary>
        protected Mat baseMat;

        // Note: Rotation is less common for RawImage, but kept for potential future use or consistency
        /// <summary>
        /// The rotated frame mat
        /// </summary>
        // protected Mat rotatedFrameMat;

        /// <summary>
        /// The base color format (usually RGBA when reading from Unity Textures).
        /// </summary>
        protected ColorFormat baseColorFormat = ColorFormat.RGBA;

        /// <summary>
        /// Indicates whether this instance has been initialized.
        /// </summary>
        protected bool hasInitDone = false;

        /// <summary>
        /// Indicates whether this instance is currently initializing (less relevant here, but kept for interface consistency).
        /// </summary>
        protected bool isInitializing = false;

        // Store texture dimensions to detect changes
        protected int currentTextureWidth = 0;
        protected int currentTextureHeight = 0;


        // Keep ColorFormat enum for consistency with WebCameraFrameProvider
        public enum ColorFormat : int
        {
            GRAY = 0,
            RGB,
            BGR,
            RGBA,
            BGRA,
        }

        // RequestedIsFrontFacing is part of ICameraFrameProvider, provide a default implementation
        public bool RequestedIsFrontFacing { get; set; } = false; // RawImage doesn't have a facing concept

        protected virtual void OnValidate()
        {
            fieldOfView = Mathf.Max(0, fieldOfView);
            defaultDepthOffset = Mathf.Max(0, defaultDepthOffset);
        }

        private void Start()
        {
            if (initOnStart)
            {
                Initialize();
            }
        }

        protected virtual void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// Initializes this instance. Reads the texture from the RawImage and prepares Mats.
        /// </summary>
        public virtual void Initialize()
        {
            if (hasInitDone)
            {
                ReleaseResources(); // Clean up previous state if any
                 if (Disposed != null)
                    Disposed.Invoke();
            }

            isInitializing = true;

            if (sourceRawImage == null)
            {
                Debug.LogError("RawImageFrameProvider: Source RawImage is not assigned.");
                isInitializing = false;
                return;
            }

            sourceTexture = sourceRawImage.texture;

            if (sourceTexture == null)
            {
                Debug.LogWarning("RawImageFrameProvider: Source RawImage does not have a texture assigned yet.");
                // We might be initialized, but cannot produce frames yet.
                // DequeueNextFrame will handle the null texture case.
                // Alternatively, you could implement a check in Update() or a delay.
                isInitializing = false;
                hasInitDone = true; // Consider it initialized, but possibly inactive
                 if (Initialized != null)
                    Initialized.Invoke();
                return;
            }

            if (!InitializeMats())
            {
                // Failed to initialize Mats (e.g., zero dimensions)
                isInitializing = false;
                return;
            }

            isInitializing = false;
            hasInitDone = true;

            if (Initialized != null)
                Initialized.Invoke();
        }

        /// <summary>
        /// Allocates or reallocates the OpenCV Mat objects and intermediate Texture2D
        /// based on the current texture dimensions and format.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        protected virtual bool InitializeMats()
        {
            if (sourceTexture == null)
            {
                Debug.LogWarning("RawImageTextureSource: Cannot initialize Mats, source texture is null.");
                ReleaseMats(); // Clean up existing resources if any
                return false;
            }

            int newWidth = sourceTexture.width;
            int newHeight = sourceTexture.height;

            if (newWidth <= 0 || newHeight <= 0)
            {
                 Debug.LogWarning($"RawImageTextureSource: Cannot initialize Mats, texture dimensions are invalid ({newWidth}x{newHeight}).");
                 ReleaseMats();
                 return false;
            }

            // Check if dimensions changed or if resources are missing
            bool needsRecreation = baseMat == null || intermediateTexture2D == null ||
                                  currentTextureWidth != newWidth || currentTextureHeight != newHeight;

            if (needsRecreation)
            {
                ReleaseMats(); // Release previous resources

                currentTextureWidth = newWidth;
                currentTextureHeight = newHeight;

                try
                {
                    // Create intermediate Texture2D (RGBA32 is generally compatible)
                    intermediateTexture2D = new Texture2D(currentTextureWidth, currentTextureHeight, TextureFormat.RGBA32, false);

                    // Create baseMat (Utils.texture2DToMat typically deals well with RGBA input)
                    baseMat = new Mat(currentTextureHeight, currentTextureWidth, CvType.CV_8UC4);
                    baseColorFormat = ColorFormat.RGBA; // Assuming RGBA from Texture2D

                    // Create frameMat based on output format
                    if (baseColorFormat == outputColorFormat)
                    {
                        frameMat = baseMat; // Reuse Mat if formats match
                    }
                    else
                    {
                        frameMat = new Mat(currentTextureHeight, currentTextureWidth, CvType.CV_8UC(Channels(outputColorFormat)));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"RawImageTextureSource: Error creating Mats or Texture2D: {ex.Message}");
                    ReleaseMats(); // Ensure cleanup on failure
                    return false;
                }
            }
            // If not needsRecreation, check if frameMat needs update due to format change
            else if (frameMat == null || (frameMat != baseMat && frameMat.channels() != Channels(outputColorFormat)))
            {
                if(frameMat != null && frameMat != baseMat) frameMat.Dispose(); // Dispose previous specific frameMat

                if (baseColorFormat == outputColorFormat)
                {
                    frameMat = baseMat;
                }
                else
                {
                     frameMat = new Mat(currentTextureHeight, currentTextureWidth, CvType.CV_8UC(Channels(outputColorFormat)));
                }
            }


            // Note: Rotation logic removed for simplicity, add back if needed
            // if (rotatedFrameMat != null) rotatedFrameMat.Dispose();
            // rotatedFrameMat = null; // Reset rotation mat

            return true;
        }

        /// <summary>
        /// Releases the OpenCV Mat resources and the intermediate Texture2D.
        /// </summary>
        protected virtual void ReleaseMats()
        {
            // If frameMat points to baseMat, only dispose baseMat
            if (frameMat != null && frameMat != baseMat)
            {
                frameMat.Dispose();
            }
            frameMat = null;

            if (baseMat != null)
            {
                baseMat.Dispose();
            }
            baseMat = null;

            // Dispose intermediate Texture2D
            if (intermediateTexture2D != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(intermediateTexture2D);
                else
                    UnityEngine.Object.DestroyImmediate(intermediateTexture2D);
                intermediateTexture2D = null;
            }

            // if (rotatedFrameMat != null) rotatedFrameMat.Dispose();
            // rotatedFrameMat = null;

             currentTextureWidth = 0;
             currentTextureHeight = 0;
        }

        /// <summary>
        /// Indicates whether this instance has been initialized.
        /// </summary>
        /// <returns><c>true</c>, if this instance has been initialized, <c>false</c> otherwise.</returns>
        public virtual bool IsInitialized()
        {
            return hasInitDone;
        }

        /// <summary>
        /// Indicates whether this instance is currently initializing.
        /// </summary>
        /// <returns><c>true</c>, if this instance is initializing, <c>false</c> otherwise.</returns>
        public virtual bool IsInitializing()
        {
            return isInitializing;
        }


        /// <summary>
        /// Indicates whether the source is ready to provide frames.
        /// Requires initialization and a valid texture.
        /// </summary>
        /// <returns><c>true</c>, if ready, <c>false</c> otherwise.</returns>
        public virtual bool IsStreaming()
        {
            // Considered "streaming" if initialized and has a valid texture assigned
            return hasInitDone && sourceRawImage != null && sourceRawImage.texture != null;
        }

        // --- ICameraFrame Implementation ---
        private readonly struct RawImageCameraFrame : ICameraFrame
        {
            private readonly Mat _mat; // The final processed Mat
            private readonly float _fov;
            private readonly float _depthOffset;

            public RawImageCameraFrame(Mat mat, float fov, float depthOffset)
            {
                _mat = mat; // This holds a reference, doesn't own it
                _fov = fov;
                _depthOffset = depthOffset;
            }

            public void Dispose()
            {
                // This frame doesn't own the Mat, so nothing to dispose here.
                // The RawImageFrameProvider manages the Mat lifecycle.
            }

            public Mat GetMat()
            {
                // Return the reference to the processed Mat
                return _mat;
            }

            public ReadOnlySpan<Color32> GetColors32()
            {
                 // This is potentially expensive: Mat -> Texture2D -> Color32[]
                 if (_mat == null || _mat.IsDisposed)
                    return Array.Empty<Color32>();

                // Create a temporary Texture2D to get pixels
                // Ensure the texture format matches the Mat channels
                TextureFormat format;
                if (_mat.channels() == 1) format = TextureFormat.Alpha8; // Or R8 if appropriate
                else if (_mat.channels() == 3) format = TextureFormat.RGB24;
                else if (_mat.channels() == 4) format = TextureFormat.RGBA32;
                else return Array.Empty<Color32>(); // Unsupported channel count

                Texture2D tempTex = new Texture2D(_mat.cols(), _mat.rows(), format, false);
                try
                {
                    Utils.matToTexture2D(_mat, tempTex);
                    // Note: matToTexture2D might handle color order (BGR vs RGB) depending on OpenCV build/settings.
                    // If colors look wrong, you might need an intermediate BGR<->RGB conversion Mat step.
                    return tempTex.GetPixels32(); // Gets a copy
                }
                finally
                {
                    // Destroy the temporary texture
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(tempTex);
                    else
                        UnityEngine.Object.DestroyImmediate(tempTex);
                }
            }


            public Vector2Int GetSize()
            {
                 if (_mat == null || _mat.IsDisposed)
                    return Vector2Int.zero;
                return new Vector2Int(_mat.cols(), _mat.rows());
            }

            public float GetFOV(ICameraFrame.FOVType type)
            {
                // RawImage doesn't intrinsically have FOV, use the configured value
                // Assuming horizontal FOV for simplicity, adjust if needed
                return _fov;
            }

            public bool ProvidesDepthData()
            {
                // Only provides depth if a default offset is set
                return _depthOffset > 0;
            }

            public float SampleDepth(int sampleX, int sampleY)
            {
                if (!ProvidesDepthData()) return -1;

                // Check bounds roughly (GetSize might be called separately)
                 var size = GetSize();
                 if (sampleX < 0 || sampleX >= size.x || sampleY < 0 || sampleY >= size.y)
                     return -1;

                // Return the constant depth offset
                return _depthOffset;
            }

            public bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point)
            {
                 point = default;
                if (!ProvidesDepthData()) return false;

                var size = GetSize();
                if (sampleX < 0 || sampleX >= size.x || sampleY < 0 || sampleY >= size.y)
                     return false;

                // Simplified perspective projection using FOV and constant depth
                // Assumes FOV is horizontal FOV
                float aspect = (float)size.x / size.y;
                float hFovRad = _fov * Mathf.Deg2Rad;
                // float vFovRad = 2f * Mathf.Atan(Mathf.Tan(hFovRad / 2f) / aspect); // Calculate vertical FOV if needed

                // Normalize coordinates (-1 to 1, with Y often inverted in image space)
                float normX = (sampleX / (float)(size.x - 1)) * 2f - 1f;
                float normY = (1f - (sampleY / (float)(size.y - 1))) * 2f - 1f; // Invert Y

                // Calculate position on the near plane (z=1) based on FOV
                float planeX = normX * Mathf.Tan(hFovRad / 2f);
                 float planeY = normY * Mathf.Tan(hFovRad / 2f) / aspect; // Use aspect ratio correct vertical position

                // Scale by depth
                point.x = planeX * _depthOffset;
                point.y = planeY * _depthOffset;
                point.z = _depthOffset;

                return true;
            }
        }
        // --- End ICameraFrame Implementation ---

        /// <summary>
        /// Gets the Mat of the current frame from the RawImage texture.
        /// The Mat object's type is determined by the outputColorFormat setting.
        /// Please do not dispose of the returned mat as it will be reused.
        /// </summary>
        /// <returns>An ICameraFrame containing the Mat of the current frame, or null if not ready.</returns>
        public virtual ICameraFrame DequeueNextFrame()
        {
            if (!hasInitDone || sourceRawImage == null)
                return null;

            // Update source texture reference in case it changed
            sourceTexture = sourceRawImage.texture;

            if (sourceTexture == null)
            {
                // If texture becomes null after initialization, release mats
                if(currentTextureWidth > 0 || currentTextureHeight > 0)
                {
                    Debug.LogWarning("RawImageTextureSource: Source texture became null. Releasing resources.");
                    ReleaseMats();
                }
                return null; // No texture to process
            }


            // Check if texture dimensions changed OR if essential resources are missing/invalid
            // and reinitialize Mats/Texture if needed
            if (intermediateTexture2D == null || baseMat == null || // Resources missing?
                sourceTexture.width != currentTextureWidth || sourceTexture.height != currentTextureHeight) // Dimensions changed?
            {
                Debug.Log($"RawImageTextureSource: Texture dimensions changed or resources invalid. " +
                          $"Current:({currentTextureWidth}x{currentTextureHeight}), " +
                          $"New:({sourceTexture.width}x{sourceTexture.height}). Reinitializing.");
                if (!InitializeMats())
                {
                    Debug.LogError("RawImageTextureSource: Failed to reinitialize Mats/Texture after change.");
                    return null; // Cannot proceed if resources aren't ready
                }
            }

             // Ensure Mats and intermediate texture are valid before proceeding (double check after potential InitializeMats call)
             if (baseMat == null || baseMat.IsDisposed ||
                 frameMat == null || frameMat.IsDisposed ||
                 intermediateTexture2D == null)
            {
                Debug.LogError("RawImageTextureSource: Mats or intermediate Texture2D are not initialized or have been disposed unexpectedly.");
                // Avoid infinite loop by not calling InitializeMats again here unless logic guarantees exit
                return null;
            }


            try
            {
                // === Step 1: Convert source Texture to intermediate Texture2D ===
                Utils.textureToTexture2D(sourceTexture, intermediateTexture2D);

                // === Step 2: Convert intermediate Texture2D to baseMat (usually RGBA) ===
                Utils.texture2DToMat(intermediateTexture2D, baseMat);

                // === Step 3: Perform color conversion if necessary (baseMat -> frameMat) ===
                if (baseColorFormat != outputColorFormat)
                {
                    int code = ColorConversionCodes(baseColorFormat, outputColorFormat);
                    if (code >= 0)
                    {
                        Imgproc.cvtColor(baseMat, frameMat, code);
                    }
                    else
                    {
                        // If no direct conversion, copy base to frame if they are different objects
                         if(frameMat != baseMat) baseMat.copyTo(frameMat);
                         Debug.LogWarning($"RawImageTextureSource: Unsupported color conversion from {baseColorFormat} to {outputColorFormat}. Using base format.");
                    }
                }
                // If formats are the same, frameMat already references baseMat (or was copied if conversion failed)
                // Now 'frameMat' holds the image in the correct format

                // === Step 4: Apply flips ===
                FlipMat(frameMat, _flipVertical, _flipHorizontal);

                // === Step 5: Return the frame wrapper ===
                // Note: Rotation logic removed for RawImage simplicity
                // if (rotatedFrameMat != null) { ... Core.rotate ... return new RawImageCameraFrame(rotatedFrameMat, ...); }

                return new RawImageCameraFrame(frameMat, fieldOfView, defaultDepthOffset);

            }
            catch (ArgumentException argEx) // Catch potential errors from Utils methods (e.g., size mismatch)
            {
                 Debug.LogError($"RawImageTextureSource: Argument Error processing texture to Mat: {argEx.Message}\n{argEx.StackTrace}");
                 // This might indicate a need to reinitialize if sizes diverged unexpectedly
                 // Consider calling InitializeMats() here cautiously or just returning null.
                 return null;
            }
            catch (Exception e) // Catch general errors
            {
                Debug.LogError($"RawImageTextureSource: General Error processing texture to Mat: {e}");
                return null;
            }
        }

        /// <summary>
        /// Flips the mat. Adapated from WebCameraFrameProvider.
        /// </summary>
        /// <param name="mat">Mat to flip.</param>
        /// <param name="applyVerticalFlip">Whether to flip vertically.</param>
        /// <param name="applyHorizontalFlip">Whether to flip horizontally.</param>
        protected virtual void FlipMat(Mat mat, bool applyVerticalFlip, bool applyHorizontalFlip)
        {
            // Texture coordinates often start top-left, OpenCV starts top-left.
            // Direct Texture -> Mat conversion might or might not need an initial flip depending on source.
            // Let's assume Utils.textureToMat provides a consistent orientation (usually requires no initial flip).
            int flipCode = int.MinValue; // int.MinValue means no flip initially

            if (applyVerticalFlip)
            {
                if (flipCode == int.MinValue) flipCode = 0; // Vertical
                else if (flipCode == 0) flipCode = int.MinValue; // Vertical -> None
                else if (flipCode == 1) flipCode = -1; // Horizontal -> Both
                else if (flipCode == -1) flipCode = 1; // Both -> Horizontal
            }

            if (applyHorizontalFlip)
            {
                if (flipCode == int.MinValue) flipCode = 1; // Horizontal
                else if (flipCode == 0) flipCode = -1; // Vertical -> Both
                else if (flipCode == 1) flipCode = int.MinValue; // Horizontal -> None
                else if (flipCode == -1) flipCode = 0; // Both -> Vertical
            }

            if (flipCode > int.MinValue)
            {
                Core.flip(mat, mat, flipCode);
            }
        }

        // Helper methods adapted from WebCameraFrameProvider
        protected virtual int Channels(ColorFormat type)
        {
            switch (type)
            {
                case ColorFormat.GRAY: return 1;
                case ColorFormat.RGB: return 3;
                case ColorFormat.BGR: return 3;
                case ColorFormat.RGBA: return 4;
                case ColorFormat.BGRA: return 4;
                default: return 4; // Default to RGBA
            }
        }

        protected virtual int ColorConversionCodes(ColorFormat srcType, ColorFormat dstType)
        {
            if (srcType == dstType) return -1; // No conversion needed

            if (srcType == ColorFormat.GRAY)
            {
                if (dstType == ColorFormat.RGB) return Imgproc.COLOR_GRAY2RGB;
                if (dstType == ColorFormat.BGR) return Imgproc.COLOR_GRAY2BGR;
                if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_GRAY2RGBA;
                if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_GRAY2BGRA;
            }
            else if (srcType == ColorFormat.RGB)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_RGB2GRAY;
                if (dstType == ColorFormat.BGR) return Imgproc.COLOR_RGB2BGR;
                if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_RGB2RGBA;
                if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_RGB2BGRA;
            }
            else if (srcType == ColorFormat.BGR)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_BGR2GRAY;
                if (dstType == ColorFormat.RGB) return Imgproc.COLOR_BGR2RGB;
                if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_BGR2RGBA;
                if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_BGR2BGRA;
            }
            else if (srcType == ColorFormat.RGBA)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_RGBA2GRAY;
                if (dstType == ColorFormat.RGB) return Imgproc.COLOR_RGBA2RGB;
                if (dstType == ColorFormat.BGR) return Imgproc.COLOR_RGBA2BGR;
                if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_RGBA2BGRA;
            }
            else if (srcType == ColorFormat.BGRA)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_BGRA2GRAY;
                if (dstType == ColorFormat.RGB) return Imgproc.COLOR_BGRA2RGB;
                if (dstType == ColorFormat.BGR) return Imgproc.COLOR_BGRA2BGR;
                if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_BGRA2RGBA;
            }
            return -1; // Conversion not found
        }


        /// <summary>
        /// To release the resources.
        /// </summary>
        protected virtual void ReleaseResources()
        {
            hasInitDone = false;
            isInitializing = false;
            sourceTexture = null; // Release reference

            ReleaseMats(); // Dispose Mat objects
        }

        /// <summary>
        /// Releases all resource used by the RawImageFrameProvider object.
        /// </summary>
        public virtual void Dispose()
        {
             if (hasInitDone || isInitializing) // Check if resources might exist
             {
                ReleaseResources();

                if (Disposed != null)
                    Disposed.Invoke();
             }
        }
    }
}

#endif // (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV)