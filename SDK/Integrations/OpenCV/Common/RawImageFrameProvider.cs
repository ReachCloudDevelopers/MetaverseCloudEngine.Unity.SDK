#if (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV)

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using System;
using System.Collections;
using System.Threading; // Required for locking
using OpenCVForUnity.UnityUtils;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI; // Required for RawImage

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    #region ClonedCameraFrame Class (Implements ICameraFrame)

    /// <summary>
    /// An implementation of ICameraFrame that holds a CLONE of the Mat data
    /// provided to it. The consumer is responsible for calling the Dispose() method
    /// manually to release the cloned Mat.
    /// </summary>
    public class ClonedCameraFrame : ICameraFrame
    {
        private Mat _clonedMat; // Owns this cloned Mat instance
        private readonly float _fov;
        private readonly float _depthOffset;
        private readonly Vector2Int _size; // Store size from the cloned Mat

        // Constructor takes the CLONED Mat and metadata
        public ClonedCameraFrame(Mat matToOwn, float fov, float depthOffset)
        {
             if (matToOwn == null || matToOwn.IsDisposed)
             {
                 Debug.LogError("ClonedCameraFrame: Received null or disposed Mat during construction.");
                 _clonedMat = null;
                 _size = Vector2Int.zero;
             }
             else
             {
                 _clonedMat = matToOwn; // Takes ownership of the provided clone
                 _size = new Vector2Int(_clonedMat.cols(), _clonedMat.rows());
             }
            _fov = fov;
            _depthOffset = depthOffset;
        }

        /// <summary>
        /// Releases the internal cloned Mat resource. Must be called manually by the consumer.
        /// </summary>
        public void Dispose() // Not IDisposable.Dispose()
        {
            if (_clonedMat != null && !_clonedMat.IsDisposed)
            {
                _clonedMat.Dispose();
            }
            _clonedMat = null; // Prevent further access
            // Note: _size remains valid even after disposal for potential post-mortem info
        }

        // --- ICameraFrame Implementation ---

        public Mat GetMat()
        {
            // Return the internal Mat, check if valid
            if (_clonedMat == null || _clonedMat.IsDisposed)
            {
                // This might be expected if consumer calls GetMat after calling Dispose
                // Avoid logging error here, just return null.
                return null;
            }
            return _clonedMat;
        }

        public ReadOnlySpan<Color32> GetColors32()
        {
            if (_clonedMat == null || _clonedMat.IsDisposed) return Array.Empty<Color32>();

            TextureFormat format;
            int channels = _clonedMat.channels();
            if (channels == 1) format = TextureFormat.Alpha8;
            else if (channels == 3) format = TextureFormat.RGB24;
            else if (channels == 4) format = TextureFormat.RGBA32;
            else
            {
                Debug.LogError($"ClonedCameraFrame.GetColors32: Unsupported Mat channels: {channels}");
                return Array.Empty<Color32>();
            }

            Texture2D tempTex = null;
            try
            {
                // Use stored size for safety
                tempTex = new Texture2D(_size.x, _size.y, format, false);
                Utils.matToTexture2D(_clonedMat, tempTex);
                return tempTex.GetPixels32();
            }
            catch (Exception ex)
            {
                Debug.LogError($"ClonedCameraFrame.GetColors32 Error: {ex.Message}\n{ex.StackTrace}");
                return Array.Empty<Color32>();
            }
            finally
            {
                if (tempTex != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(tempTex);
                    else UnityEngine.Object.DestroyImmediate(tempTex);
                }
            }
        }

        public Vector2Int GetSize()
        {
             // Return stored size. It remains valid even after Dispose() is called.
            return _size;
        }

        public float GetFOV(ICameraFrame.FOVType type)
        {
            if (type == ICameraFrame.FOVType.Horizontal)
            {
                return _fov;
            }
            else
            {
                if (_size.y == 0) return 0;
                float aspect = (float)_size.x / _size.y;
                if (aspect <= 0 || _fov <= 0) return 0;
                float hFovRad = _fov * Mathf.Deg2Rad;
                float vFovRad = 2f * Mathf.Atan(Mathf.Tan(hFovRad / 2f) / aspect);
                return vFovRad * Mathf.Rad2Deg;
            }
        }

        public bool ProvidesDepthData()
        {
            return _depthOffset > 0;
        }

        public float SampleDepth(int sampleX, int sampleY)
        {
            if (!ProvidesDepthData()) return -1f;
            // Optional: Add bounds check using _size
            if (sampleX < 0 || sampleX >= _size.x || sampleY < 0 || sampleY >= _size.y) return -1f;
            return _depthOffset;
        }

        public bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point)
        {
            point = default;
            if (!ProvidesDepthData() || _size.x <= 0 || _size.y <= 0) return false;
            if (sampleX < 0 || sampleX >= _size.x || sampleY < 0 || sampleY >= _size.y) return false;

            float aspect = (float)_size.x / _size.y;
            float hFovRad = _fov * Mathf.Deg2Rad;
            if (hFovRad <= 0 || aspect <= 0) return false;

            float normX = (sampleX / (float)(_size.x - 1)) * 2f - 1f;
            float normY = (1f - (sampleY / (float)(_size.y - 1))) * 2f - 1f; // Invert Y
            float planeX = normX * Mathf.Tan(hFovRad / 2f);
            float planeY = normY * Mathf.Tan(hFovRad / 2f) / aspect;

            point.x = planeX * _depthOffset;
            point.y = planeY * _depthOffset;
            point.z = _depthOffset;
            return true;
        }
    }

    #endregion

    #region RawImageFrameProvider Class

    [HideMonoScript]
    [DeclareFoldoutGroup("RawImage Source Options")]
    [DeclareFoldoutGroup("Additional Metadata")]
    public class RawImageFrameProvider : TriInspectorMonoBehaviour, ICameraFrameProvider
    {
        #region Inspector Fields
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
                    _needsReinitialization = true;
                }
            }
        }

        [Tooltip("Size of the internal ring buffer for processed frames.")]
        [Min(2)]
        [SerializeField]
        [Group("RawImage Source Options")]
        private int bufferSize = 3;

        #endregion

        #region Events
        public event Action Initialized;
        public event Action Disposed;
        #endregion

        #region Internal Fields
        protected Texture sourceTexture;
        protected Texture2D intermediateTexture2D; // Main thread only
        protected Mat baseMat; // Main thread only
        protected Mat frameMat; // Main thread only (intermediate for processing)
        protected ColorFormat baseColorFormat = ColorFormat.RGBA;

        protected volatile bool hasInitDone = false;
        protected bool isInitializing = false;
        private bool _needsReinitialization = false;
        private bool _isStreaming = false;

        // --- Ring Buffer Fields ---
        private Mat[] _ringBufferMats; // Provider owns these Mats
        private int _writeIndex = 0;
        private int _readIndex = 0;
        private readonly object _bufferLock = new object();
        private long _producedFrameCount = 0;
        private long _consumedFrameCount = 0;

        protected int currentTextureWidth = 0;
        protected int currentTextureHeight = 0;

        public enum ColorFormat : int { GRAY = 0, RGB, BGR, RGBA, BGRA }

        public bool RequestedIsFrontFacing { get; set; } = false;
        #endregion

        #region Unity Lifecycle Methods

        protected virtual void OnValidate()
        {
            fieldOfView = Mathf.Max(0, fieldOfView);
            defaultDepthOffset = Mathf.Max(0, defaultDepthOffset);
            bufferSize = Mathf.Max(2, bufferSize);
        }

        private void Start()
        {
            if (initOnStart)
                Initialize();
        }

        private void OnDisable()
        {
            _isStreaming = false;
            _needsReinitialization = true; // Will reinit in next LateUpdate
        }

        protected virtual void OnDestroy()
        {
            Dispose();
        }

        protected virtual void LateUpdate()
        {
            if (_needsReinitialization && hasInitDone)
            {
                Debug.Log("RawImageFrameProvider: Reinitializing Mats due to configuration change.");
                ReleaseMatsInternal();
                if (!InitializeMatsInternal())
                {
                    Debug.LogError("RawImageFrameProvider: Failed to reinitialize Mats in LateUpdate.");
                    hasInitDone = false;
                }
                _needsReinitialization = false;
            }

            if (!hasInitDone || isInitializing)
            {
                _isStreaming = false;
                return;
            }

            if (!sourceRawImage || !sourceRawImage.isActiveAndEnabled)
            {
                _isStreaming = false;
                _needsReinitialization = true; // Will reinit in next LateUpdate
                return;
            }

            sourceTexture = sourceRawImage.texture;
            if (!sourceTexture)
            {
                _isStreaming = false;
                _needsReinitialization = true; // Will reinit in next LateUpdate
                return;
            }

            _isStreaming = true;

            bool resourcesInvalid = intermediateTexture2D == null || baseMat == null || frameMat == null || _ringBufferMats == null;
            bool dimensionsChanged = sourceTexture.width != currentTextureWidth || sourceTexture.height != currentTextureHeight;

            if (resourcesInvalid || dimensionsChanged)
            {
                 Debug.Log($"RawImageFrameProvider: Resources invalid ({resourcesInvalid}) or dimensions changed ({dimensionsChanged}). Reinitializing.");
                 ReleaseMatsInternal();
                 if (!InitializeMatsInternal())
                 {
                     Debug.LogError("RawImageFrameProvider: Failed to reinitialize Mats/Texture after change in LateUpdate.");
                     hasInitDone = false;
                     return;
                 }
            }

            if (baseMat == null || baseMat.IsDisposed ||
                frameMat == null || frameMat.IsDisposed ||
                intermediateTexture2D == null || _ringBufferMats == null)
            {
                 Debug.LogError("RawImageFrameProvider: Processing resources invalid after reinitialization check.");
                 return;
            }

            // --- Process Texture to Mat (Main Thread Work) ---
            Mat targetMatInBuffer = null;
            int nextWriteIndex = -1;

            lock (_bufferLock)
            {
                nextWriteIndex = _writeIndex;
                targetMatInBuffer = _ringBufferMats[nextWriteIndex];

                if (targetMatInBuffer == null || targetMatInBuffer.IsDisposed)
                {
                    Debug.LogError($"RawImageFrameProvider: Target Mat in ring buffer at index {nextWriteIndex} is invalid! Reinitializing.");
                    _needsReinitialization = true;
                    return;
                }

                try
                {
                    // Step 1: Texture -> Texture2D
                    Utils.textureToTexture2D(sourceTexture, intermediateTexture2D);
                    // Step 2: Texture2D -> baseMat (RGBA)
                    Utils.texture2DToMat(intermediateTexture2D, baseMat);

                    Mat matToCopy;
                    // Step 3: Color Conversion (if needed)
                    if (baseColorFormat != outputColorFormat)
                    {
                        int code = ColorConversionCodes(baseColorFormat, outputColorFormat);
                        if (code >= 0)
                        {
                            Imgproc.cvtColor(baseMat, frameMat, code);
                            matToCopy = frameMat;
                        }
                        else
                        {
                             if (frameMat != baseMat) baseMat.copyTo(frameMat);
                             matToCopy = frameMat;
                             Debug.LogWarning($"RawImageFrameProvider: Unsupported color conversion from {baseColorFormat} to {outputColorFormat}. Using base format.");
                        }
                    }
                    else
                    {
                        matToCopy = baseMat;
                    }

                    // Step 4: Apply flips
                    FlipMat(matToCopy, _flipVertical, _flipHorizontal);

                    // Step 5: Copy final result to the ring buffer Mat
                    matToCopy.copyTo(targetMatInBuffer);

                    // Step 6: Update Write Index and Count
                    _writeIndex = (_writeIndex + 1) % bufferSize;
                    _producedFrameCount++;

                }
                catch (Exception e) // Catch potential errors during processing
                {
                     Debug.LogError($"RawImageFrameProvider: Error during frame processing in LateUpdate: {e.Message}\n{e.StackTrace}");
                     // Optionally skip frame production or attempt recovery
                     _needsReinitialization = e is ArgumentException || e is CvException; // Trigger re-init for common issues
                }
            } // --- End Critical Section ---
        } // End LateUpdate

        #endregion

        #region Public API (ICameraFrameProvider Implementation)

        public virtual void Initialize()
        {
            if (!this) return;
            if (!isActiveAndEnabled) return;
            if (isInitializing || hasInitDone) return;

            isInitializing = true;
            hasInitDone = false;

            if (sourceRawImage == null)
            {
                Debug.LogError("RawImageFrameProvider: Source RawImage is not assigned.");
                isInitializing = false;
                return;
            }

            sourceTexture = sourceRawImage.texture;
            if (sourceTexture != null)
            {
                 if (!InitializeMatsInternal())
                 {
                     Debug.LogError("RawImageFrameProvider: Failed to initialize Mats during Initialize().");
                     isInitializing = false;
                     return;
                 }
            }
            else {
                 Debug.LogWarning("RawImageFrameProvider: Source RawImage has no texture at initialization. Will initialize Mats in LateUpdate.");
            }

            isInitializing = false;
            hasInitDone = true;
            _needsReinitialization = false;

            Debug.Log("RawImageFrameProvider: Initialized.");
            Initialized?.Invoke();
        }

        public virtual bool IsInitialized()
        {
            return hasInitDone;
        }

        public virtual bool IsInitializing()
        {
            return isInitializing;
        }

        public virtual bool IsStreaming()
        {
            return this && isActiveAndEnabled && hasInitDone && _isStreaming;
        }

        /// <summary>
        /// Dequeues the next available processed frame. (Thread-Safe)
        /// Returns an ICameraFrame containing a CLONE of the data.
        /// The caller is responsible for calling Dispose() on the returned object.
        /// </summary>
        public virtual ICameraFrame DequeueNextFrame()
        {
            if (!hasInitDone) return null;

            Mat clonedMat = null;
            bool gotFrame = false;

            lock (_bufferLock)
            {
                if (_consumedFrameCount < _producedFrameCount)
                {
                    if (_ringBufferMats == null || _ringBufferMats.Length != bufferSize)
                    {
                        Debug.LogError("RawImageFrameProvider: Ring buffer invalid in DequeueNextFrame.");
                        return null;
                    }

                    Mat sourceMat = _ringBufferMats[_readIndex];
                    if (sourceMat == null || sourceMat.IsDisposed)
                    {
                        Debug.LogError($"RawImageFrameProvider: Mat at read index {_readIndex} is invalid.");
                        _readIndex = (_readIndex + 1) % bufferSize;
                        _consumedFrameCount++;
                        return null;
                    }

                    try
                    {
                        // *** Clone the Mat data ***
                        clonedMat = sourceMat.clone();
                        gotFrame = true;

                        // Advance read index and count
                        _readIndex = (_readIndex + 1) % bufferSize;
                        _consumedFrameCount++;
                    }
                    catch (Exception ex)
                    {
                         Debug.LogError($"RawImageFrameProvider: Error cloning Mat in DequeueNextFrame: {ex.Message}");
                         // If clonedMat was successfully created before another error in the try block, it could leak.
                         // However, with current code, only clone() is likely to throw here.
                         // If clone() itself fails, clonedMat should not hold a valid Mat needing disposal.
                         // For added safety, one might check and dispose clonedMat if non-null here.
                         _readIndex = (_readIndex + 1) % bufferSize;
                         _consumedFrameCount++;
                         return null; // Return null as clone failed or another error occurred
                    }
                }
            }

            if (gotFrame && clonedMat != null)
            {
                // Create the frame wrapper OWNING the cloned Mat
                try
                {
                    return new ClonedCameraFrame(clonedMat, fieldOfView, defaultDepthOffset);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"RawImageFrameProvider: Exception during ClonedCameraFrame construction. Disposing the cloned Mat to prevent leak. Error: {ex.Message}");
                    if (clonedMat != null && !clonedMat.IsDisposed)
                        clonedMat.Dispose();
                    return null; // Indicate failure to provide a frame
                }
            }
            else
            {
                // Ensure that if clonedMat was somehow created but not used, it's disposed.
                // This case should ideally not be hit if gotFrame is false due to clone failure and clonedMat is null.
                if (clonedMat != null && !clonedMat.IsDisposed) {
                    Debug.LogWarning("RawImageFrameProvider: clonedMat existed but was not used to create a frame. Disposing.");
                    clonedMat.Dispose();
                }
                return null; // No new frame available or clone failed
            }
        }

        public virtual void Dispose()
        {
            _isStreaming = false;

            lock (_bufferLock)
            {
                if (hasInitDone || isInitializing)
                {
                    ReleaseResourcesInternal();

                    hasInitDone = false;
                    isInitializing = false;
                    sourceTexture = null;

                    Debug.Log("RawImageFrameProvider: Disposed.");
                    try { Disposed?.Invoke(); } catch (Exception e) { Debug.LogError($"Error invoking Disposed event: {e}"); }
                }
            }
        }

        #endregion

        #region Internal Helper Methods

        private bool InitializeMatsInternal()
        {
            if (sourceTexture == null)
            {
                Debug.LogWarning("RawImageTextureSource: Cannot initialize Mats, source texture is null.");
                return true; // Wait for LateUpdate
            }

            if (sourceRawImage == null || !sourceRawImage.isActiveAndEnabled)
            {
                Debug.LogWarning("RawImageTextureSource: Source RawImage is not active or assigned. Will initialize Mats in LateUpdate.");
                return true; // Wait for LateUpdate
            }

            int newWidth = sourceTexture.width;
            int newHeight = sourceTexture.height;

            if (newWidth <= 0 || newHeight <= 0)
            {
                Debug.LogError($"RawImageTextureSource: Cannot initialize Mats, texture dimensions invalid ({newWidth}x{newHeight}).");
                return false;
            }

            ReleaseMatsInternal(); // Ensure cleanup before recreating

            bool success = false;
            try
            {
                currentTextureWidth = newWidth;
                currentTextureHeight = newHeight;

                intermediateTexture2D = new Texture2D(currentTextureWidth, currentTextureHeight, TextureFormat.RGBA32, false);
                baseMat = new Mat(currentTextureHeight, currentTextureWidth, CvType.CV_8UC4);
                baseColorFormat = ColorFormat.RGBA;

                if (baseColorFormat == outputColorFormat) {
                    frameMat = baseMat; // Reuse
                } else {
                    frameMat = new Mat(currentTextureHeight, currentTextureWidth, CvType.CV_8UC(Channels(outputColorFormat)));
                }

                // --- Initialize Ring Buffer with Provider-Owned Mats ---
                _ringBufferMats = new Mat[bufferSize];
                int outputCvType = CvType.CV_8UC(Channels(outputColorFormat));

                for (int i = 0; i < bufferSize; i++) {
                    _ringBufferMats[i] = new Mat(currentTextureHeight, currentTextureWidth, outputCvType);
                }
                _writeIndex = 0;
                _readIndex = 0;
                _producedFrameCount = 0;
                _consumedFrameCount = 0;
                // -----------------------------

                success = true;
                Debug.Log($"RawImageFrameProvider: Mats and Ring Buffer Initialized ({currentTextureWidth}x{currentTextureHeight}, Format: {_outputColorFormat}, BufferSize: {bufferSize})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"RawImageTextureSource: Error creating resources: {ex.Message}\n{ex.StackTrace}");
                ReleaseMatsInternal(); // Cleanup on failure
                success = false;
            }
            return success;
        }

        private void ReleaseMatsInternal()
        {
            // Dispose intermediate processing Mats
            if (frameMat != null && frameMat != baseMat) frameMat.Dispose();
            frameMat = null;
            if (baseMat != null) baseMat.Dispose();
            baseMat = null;

            // Dispose intermediate Texture2D
            if (intermediateTexture2D != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(intermediateTexture2D);
                else UnityEngine.Object.DestroyImmediate(intermediateTexture2D);
                intermediateTexture2D = null;
            }

            // Dispose Mats OWNED BY THE PROVIDER in the ring buffer
            if (_ringBufferMats != null)
                 for (int i = 0; i < _ringBufferMats.Length; i++)
                     _ringBufferMats[i]?.Dispose();
            _ringBufferMats = null;

            currentTextureWidth = 0;
            currentTextureHeight = 0;
            _writeIndex = 0;
            _readIndex = 0;
            _producedFrameCount = 0;
            _consumedFrameCount = 0;
        }

        protected virtual void ReleaseResourcesInternal()
        {
            ReleaseMatsInternal();
        }

        /// <summary>
        /// Flips the mat. Adapated from WebCameraFrameProvider. (Main Thread)
        /// </summary>
        protected virtual void FlipMat(Mat mat, bool applyVerticalFlip, bool applyHorizontalFlip)
        {
            if (mat == null || mat.IsDisposed) return; // Safety check

            int flipCode = int.MinValue; // No flip initially

            if (applyVerticalFlip)
            {
                if (flipCode == int.MinValue) flipCode = 0;        // None -> Vertical
                else if (flipCode == 0) flipCode = int.MinValue;   // Vertical -> None
                else if (flipCode == 1) flipCode = -1;       // Horizontal -> Both
                else if (flipCode == -1) flipCode = 1;       // Both -> Horizontal
            }

            if (applyHorizontalFlip)
            {
                if (flipCode == int.MinValue) flipCode = 1;        // None -> Horizontal
                else if (flipCode == 0) flipCode = -1;       // Vertical -> Both
                else if (flipCode == 1) flipCode = int.MinValue;   // Horizontal -> None
                else if (flipCode == -1) flipCode = 0;       // Both -> Vertical
            }

            if (flipCode > int.MinValue)
            {
                 try {
                     Core.flip(mat, mat, flipCode);
                 } catch (CvException cvEx) {
                     Debug.LogError($"Error during Core.flip: {cvEx.Message}");
                 }
            }
        }

        /// <summary> Helper methods adapted from WebCameraFrameProvider (Main Thread) </summary>
        protected virtual int Channels(ColorFormat type)
        {
            switch (type)
            {
                case ColorFormat.GRAY: return 1;
                case ColorFormat.RGB: return 3;
                case ColorFormat.BGR: return 3;
                case ColorFormat.RGBA: return 4;
                case ColorFormat.BGRA: return 4;
                default:
                     Debug.LogWarning($"Unsupported ColorFormat '{type}' in Channels(). Defaulting to 4.");
                     return 4; // Default to RGBA
            }
        }

        /// <summary> Helper methods adapted from WebCameraFrameProvider (Main Thread) </summary>
        protected virtual int ColorConversionCodes(ColorFormat srcType, ColorFormat dstType)
        {
             if (srcType == dstType) return -1; // No conversion needed

             if (srcType == ColorFormat.GRAY)
             {
                 switch (dstType)
                 {
                     case ColorFormat.RGB: return Imgproc.COLOR_GRAY2RGB;
                     case ColorFormat.BGR: return Imgproc.COLOR_GRAY2BGR;
                     case ColorFormat.RGBA: return Imgproc.COLOR_GRAY2RGBA;
                     case ColorFormat.BGRA: return Imgproc.COLOR_GRAY2BGRA;
                 }
             }
             else if (srcType == ColorFormat.RGB)
             {
                 switch (dstType)
                 {
                     case ColorFormat.GRAY: return Imgproc.COLOR_RGB2GRAY;
                     case ColorFormat.BGR: return Imgproc.COLOR_RGB2BGR;
                     case ColorFormat.RGBA: return Imgproc.COLOR_RGB2RGBA;
                     case ColorFormat.BGRA: return Imgproc.COLOR_RGB2BGRA;
                 }
             }
             else if (srcType == ColorFormat.BGR)
             {
                 switch (dstType)
                 {
                     case ColorFormat.GRAY: return Imgproc.COLOR_BGR2GRAY;
                     case ColorFormat.RGB: return Imgproc.COLOR_BGR2RGB;
                     case ColorFormat.RGBA: return Imgproc.COLOR_BGR2RGBA;
                     case ColorFormat.BGRA: return Imgproc.COLOR_BGR2BGRA;
                 }
             }
             else if (srcType == ColorFormat.RGBA) // Common case from Texture2D
             {
                 switch (dstType)
                 {
                     case ColorFormat.GRAY: return Imgproc.COLOR_RGBA2GRAY;
                     case ColorFormat.RGB: return Imgproc.COLOR_RGBA2RGB;
                     case ColorFormat.BGR: return Imgproc.COLOR_RGBA2BGR;
                     case ColorFormat.BGRA: return Imgproc.COLOR_RGBA2BGRA;
                 }
             }
             else if (srcType == ColorFormat.BGRA)
             {
                 switch (dstType)
                 {
                     case ColorFormat.GRAY: return Imgproc.COLOR_BGRA2GRAY;
                     case ColorFormat.RGB: return Imgproc.COLOR_BGRA2RGB;
                     case ColorFormat.BGR: return Imgproc.COLOR_BGRA2BGR;
                     case ColorFormat.RGBA: return Imgproc.COLOR_BGRA2RGBA;
                 }
             }

             Debug.LogWarning($"Unsupported color conversion combination: {srcType} to {dstType}");
             return -1; // Indicate unsupported conversion
        }

        #endregion

    } // End Class RawImageFrameProvider

    #endregion
} // End Namespace

#endif // (METAVERSE_CLOUD_ENGINE_INTERNAL && METAVERSE_CLOUD_ENGINE_INITIALIZED || MV_OPENCV)