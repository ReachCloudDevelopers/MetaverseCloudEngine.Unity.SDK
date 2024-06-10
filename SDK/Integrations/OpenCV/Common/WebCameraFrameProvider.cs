#if (METAVERSE_CLOUD_ENGINE_INTERNAL || MV_OPENCV)

#if !OPENCV_DONT_USE_WEBCAMTEXTURE_API

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using System;
using System.Collections;
using OpenCVForUnity.UnityUtils;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace MetaverseCloudEngine.Unity.OpenCV.Common
{
    [HideMonoScript]
    [DeclareFoldoutGroup("Webcam Texture Creation Options")]
    [DeclareFoldoutGroup("Additional Metadata")]
    public class WebCameraFrameProvider : TriInspectorMonoBehaviour, ICameraFrameProvider
    {
        [Tooltip("If true, initializes the webcam on start.")] [SerializeField]
        private bool initOnStart;

        [Group("Additional Metadata")]
        [Min(0)] [SerializeField] private float fieldOfView = 60;

        [Tooltip("Set this to false if you will be supplying the WebCamTexture via the 'SetWebCamTexture' method.")]
        [SerializeField]
        [Group("Webcam Texture Creation Options")]
        private bool createWebCamTexture = true;
        
        /// <summary>
        /// Set the name of the camera device to use. (or device index number)
        /// </summary>
        [Group("Webcam Texture Creation Options")]
        [SerializeField, FormerlySerializedAs("requestedDeviceName"),
         TooltipAttribute("Set the name of the device to use. (or device index number)")]
        protected string _requestedDeviceName = null;

        public virtual string requestedDeviceName
        {
            get { return _requestedDeviceName; }
            set
            {
                if (_requestedDeviceName != value)
                {
                    _requestedDeviceName = value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        /// <summary>
        /// Set the width of camera.
        /// </summary>
        [Group("Webcam Texture Creation Options")]
        [SerializeField, FormerlySerializedAs("requestedWidth"), TooltipAttribute("Set the width of camera.")]
        protected int _requestedWidth = 640;

        public virtual int requestedWidth
        {
            get { return _requestedWidth; }
            set
            {
                int _value = (int)Mathf.Clamp(value, 0f, float.MaxValue);
                if (_requestedWidth != _value)
                {
                    _requestedWidth = _value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        /// <summary>
        /// Set the height of camera.
        /// </summary>
        [SerializeField, FormerlySerializedAs("requestedHeight"), TooltipAttribute("Set the height of camera.")]
        [Group("Webcam Texture Creation Options")]
        protected int _requestedHeight = 480;

        public virtual int requestedHeight
        {
            get { return _requestedHeight; }
            set
            {
                int _value = (int)Mathf.Clamp(value, 0f, float.MaxValue);
                if (_requestedHeight != _value)
                {
                    _requestedHeight = _value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        /// <summary>
        /// Set whether to use the front facing camera.
        /// </summary>
        [SerializeField, FormerlySerializedAs("requestedIsFrontFacing"),
         TooltipAttribute("Set whether to use the front facing camera.")]
        [Group("Webcam Texture Creation Options")]
        protected bool _requestedIsFrontFacing = false;

        public virtual bool RequestedIsFrontFacing
        {
            get { return _requestedIsFrontFacing; }
            set
            {
                if (_requestedIsFrontFacing != value)
                {
                    _requestedIsFrontFacing = value;
                    if (hasInitDone)
                        Initialize(_requestedIsFrontFacing, requestedFPS, rotate90Degree);
                }
            }
        }

        /// <summary>
        /// Set the frame rate of camera.
        /// </summary>
        [SerializeField, FormerlySerializedAs("requestedFPS"), TooltipAttribute("Set the frame rate of camera.")]
        [Group("Webcam Texture Creation Options")]
        protected float _requestedFPS = 30f;

        public virtual float requestedFPS
        {
            get { return _requestedFPS; }
            set
            {
                float _value = Mathf.Clamp(value, -1f, float.MaxValue);
                if (_requestedFPS != _value)
                {
                    _requestedFPS = _value;
                    if (hasInitDone)
                    {
                        bool isPlaying = IsStreaming();
                        Stop();
                        if (webCamTexture is WebCamTexture t)
                            t.requestedFPS = _requestedFPS;
                        if (isPlaying)
                            Play();
                    }
                }
            }
        }

        /// <summary>
        /// Sets whether to rotate camera frame 90 degrees. (clockwise)
        /// </summary>
        [SerializeField, FormerlySerializedAs("rotate90Degree"),
         TooltipAttribute("Sets whether to rotate camera frame 90 degrees. (clockwise)")]
        [Group("Webcam Texture Creation Options")]
        protected bool _rotate90Degree = false;

        public virtual bool rotate90Degree
        {
            get { return _rotate90Degree; }
            set
            {
                if (_rotate90Degree != value)
                {
                    _rotate90Degree = value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        /// <summary>
        /// Determines if flips vertically.
        /// </summary>
        [SerializeField, FormerlySerializedAs("flipVertical"), TooltipAttribute("Determines if flips vertically.")]
        [Group("Webcam Texture Creation Options")]
        protected bool _flipVertical = false;

        public virtual bool flipVertical
        {
            get { return _flipVertical; }
            set { _flipVertical = value; }
        }

        /// <summary>
        /// Determines if flips horizontal.
        /// </summary>
        [SerializeField, FormerlySerializedAs("flipHorizontal"), TooltipAttribute("Determines if flips horizontal.")]
        [Group("Webcam Texture Creation Options")]
        protected bool _flipHorizontal = false;

        public virtual bool flipHorizontal
        {
            get { return _flipHorizontal; }
            set { _flipHorizontal = value; }
        }

        /// <summary>
        /// Select the output color format.
        /// </summary>
        [SerializeField, FormerlySerializedAs("outputColorFormat"), TooltipAttribute("Select the output color format.")]
        [Group("Webcam Texture Creation Options")]
        protected ColorFormat _outputColorFormat = ColorFormat.RGBA;

        public virtual ColorFormat outputColorFormat
        {
            get { return _outputColorFormat; }
            set
            {
                if (_outputColorFormat != value)
                {
                    _outputColorFormat = value;
                    if (hasInitDone)
                        Initialize();
                }
            }
        }

        /// <summary>
        /// The number of frames before the initialization process times out.
        /// </summary>
        [SerializeField, FormerlySerializedAs("timeoutFrameCount"),
         TooltipAttribute("The number of frames before the initialization process times out.")]
        [Group("Webcam Texture Creation Options")]
        protected int _timeoutFrameCount = 1500;

        public virtual int timeoutFrameCount
        {
            get { return _timeoutFrameCount; }
            set { _timeoutFrameCount = (int)Mathf.Clamp(value, 0f, float.MaxValue); }
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
        /// UnityEvent that is triggered when the webcam texture is started.
        /// </summary>
        public UnityEvent<Texture> onFrameReceived;

        /// <summary>
        /// UnityEvent that is triggered when this instance is error Occurred.
        /// </summary>
        public ErrorUnityEvent onErrorOccurred;

        /// <summary>
        /// The active WebcamTexture.
        /// </summary>
        protected Texture webCamTexture;

        /// <summary>
        /// The active WebcamDevice.
        /// </summary>
        protected WebCamDevice webCamDevice;

        /// <summary>
        /// The frame mat.
        /// </summary>
        protected Mat frameMat;

        /// <summary>
        /// The base mat.
        /// </summary>
        protected Mat baseMat;

        /// <summary>
        /// The rotated frame mat
        /// </summary>
        protected Mat rotatedFrameMat;

        /// <summary>
        /// The buffer colors.
        /// </summary>
        protected Color32[] colors;

        /// <summary>
        /// The base color format.
        /// </summary>
        protected ColorFormat baseColorFormat = ColorFormat.RGBA;

        /// <summary>
        /// Indicates whether this instance is waiting for initialization to complete.
        /// </summary>
        protected bool isInitWaiting = false;

        /// <summary>
        /// Indicates whether this instance has been initialized.
        /// </summary>
        protected bool hasInitDone = false;

        /// <summary>
        /// The initialization coroutine.
        /// </summary>
        protected IEnumerator initCoroutine;

        /// <summary>
        /// The orientation of the screen.
        /// </summary>
        protected ScreenOrientation screenOrientation;

        /// <summary>
        /// The width of the screen.
        /// </summary>
        protected int screenWidth;

        /// <summary>
        /// The height of the screen.
        /// </summary>
        protected int screenHeight;

        public enum ColorFormat : int
        {
            GRAY = 0,
            RGB,
            BGR,
            RGBA,
            BGRA,
        }

        public enum ErrorCode : int
        {
            UNKNOWN = 0,
            CAMERA_DEVICE_NOT_EXIST,
            CAMERA_PERMISSION_DENIED,
            TIMEOUT,
        }

        [Serializable]
        public class ErrorUnityEvent : UnityEvent<ErrorCode>
        {
        }

        protected virtual void OnValidate()
        {
            _requestedWidth = (int)Mathf.Clamp(_requestedWidth, 0f, float.MaxValue);
            _requestedHeight = (int)Mathf.Clamp(_requestedHeight, 0f, float.MaxValue);
            _requestedFPS = Mathf.Clamp(_requestedFPS, -1f, float.MaxValue);
            _timeoutFrameCount = (int)Mathf.Clamp(_timeoutFrameCount, 0f, float.MaxValue);
        }

#if !UNITY_EDITOR && !UNITY_ANDROID
        protected bool isScreenSizeChangeWaiting = false;
#endif

        private void Start()
        {
            if (initOnStart)
            {
                Initialize();
            }
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            if (hasInitDone)
            {
                // Catch the orientation change of the screen and correct the mat image to the correct direction.
                if (screenOrientation != Screen.orientation)
                {
#if !UNITY_EDITOR && !UNITY_ANDROID
                    // Wait one frame until the Screen.width/Screen.height property changes.
                    if (!isScreenSizeChangeWaiting)
                    {
                        isScreenSizeChangeWaiting = true;
                        return;
                    }
                    isScreenSizeChangeWaiting = false;
#endif

                    if (Disposed != null)
                        Disposed.Invoke();

                    if (frameMat != null)
                    {
                        frameMat.Dispose();
                        frameMat = null;
                    }

                    if (baseMat != null)
                    {
                        baseMat.Dispose();
                        baseMat = null;
                    }

                    if (rotatedFrameMat != null)
                    {
                        rotatedFrameMat.Dispose();
                        rotatedFrameMat = null;
                    }

                    baseMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4,
                        new Scalar(0, 0, 0, 255));

                    if (baseColorFormat == outputColorFormat)
                    {
                        frameMat = baseMat;
                    }
                    else
                    {
                        frameMat = new Mat(baseMat.rows(), baseMat.cols(), CvType.CV_8UC(Channels(outputColorFormat)),
                            new Scalar(0, 0, 0, 255));
                    }

                    screenOrientation = Screen.orientation;
                    screenWidth = Screen.width;
                    screenHeight = Screen.height;

                    bool isRotatedFrame = false;
#if !UNITY_EDITOR && !(UNITY_STANDALONE || UNITY_WEBGL)
                    if (screenOrientation == ScreenOrientation.Portrait || screenOrientation == ScreenOrientation.PortraitUpsideDown)
                    {
                        if (!rotate90Degree)
                            isRotatedFrame = true;
                    }
                    else if (rotate90Degree)
                    {
                        isRotatedFrame = true;
                    }
#else
                    if (rotate90Degree)
                        isRotatedFrame = true;
#endif
                    if (isRotatedFrame)
                        rotatedFrameMat = new Mat(frameMat.cols(), frameMat.rows(),
                            CvType.CV_8UC(Channels(outputColorFormat)), new Scalar(0, 0, 0, 255));

                    if (Initialized != null)
                        Initialized.Invoke();
                }
            }
        }

        protected virtual IEnumerator OnApplicationFocus(bool hasFocus)
        {
#if ((UNITY_IOS || UNITY_WEBGL) && UNITY_2018_1_OR_NEWER) || (UNITY_ANDROID && UNITY_2018_3_OR_NEWER)
            yield return null;

            if (isUserRequestingPermission && hasFocus)
                isUserRequestingPermission = false;
#endif
            yield break;
        }

        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        protected virtual void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public virtual void Initialize()
        {
            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }

            if (onErrorOccurred == null)
                onErrorOccurred = new ErrorUnityEvent();

            initCoroutine = _Initialize();
            StartCoroutine(initCoroutine);
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="requestedWidth">Requested width.</param>
        /// <param name="requestedHeight">Requested height.</param>
        public virtual void Initialize(int requestedWidth, int requestedHeight)
        {
            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }

            _requestedWidth = requestedWidth;
            _requestedHeight = requestedHeight;
            if (onErrorOccurred == null)
                onErrorOccurred = new ErrorUnityEvent();

            initCoroutine = _Initialize();
            StartCoroutine(initCoroutine);
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="requestedIsFrontFacing">If set to <c>true</c> requested to using the front camera.</param>
        /// <param name="requestedFPS">Requested FPS.</param>
        /// <param name="rotate90Degree">If set to <c>true</c> requested to rotate camera frame 90 degrees. (clockwise)</param>
        public virtual void Initialize(bool requestedIsFrontFacing, float requestedFPS = 30f,
            bool rotate90Degree = false)
        {
            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }

            _requestedDeviceName = null;
            _requestedIsFrontFacing = requestedIsFrontFacing;
            _requestedFPS = requestedFPS;
            _rotate90Degree = rotate90Degree;
            if (onErrorOccurred == null)
                onErrorOccurred = new ErrorUnityEvent();

            initCoroutine = _Initialize();
            StartCoroutine(initCoroutine);
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <param name="deviceName">Device name.</param>
        /// <param name="requestedWidth">Requested width.</param>
        /// <param name="requestedHeight">Requested height.</param>
        /// <param name="requestedIsFrontFacing">If set to <c>true</c> requested to using the front camera.</param>
        /// <param name="requestedFPS">Requested FPS.</param>
        /// <param name="rotate90Degree">If set to <c>true</c> requested to rotate camera frame 90 degrees. (clockwise)</param>
        public virtual void Initialize(string deviceName, int requestedWidth, int requestedHeight,
            bool requestedIsFrontFacing = false, float requestedFPS = 30f, bool rotate90Degree = false)
        {
            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }

            _requestedDeviceName = deviceName;
            _requestedWidth = requestedWidth;
            _requestedHeight = requestedHeight;
            _requestedIsFrontFacing = requestedIsFrontFacing;
            _requestedFPS = requestedFPS;
            _rotate90Degree = rotate90Degree;
            if (onErrorOccurred == null)
                onErrorOccurred = new ErrorUnityEvent();

            initCoroutine = _Initialize();
            StartCoroutine(initCoroutine);
        }

        /// <summary>
        /// Initializes this instance by coroutine.
        /// </summary>
        protected virtual IEnumerator _Initialize()
        {
            if (hasInitDone)
            {
                ReleaseResources();

                if (Disposed != null)
                    Disposed.Invoke();
            }

            isInitWaiting = true;

#if (UNITY_IOS || UNITY_WEBGL || UNITY_ANDROID) && !UNITY_EDITOR
            // Checks camera permission state.
            IEnumerator coroutine = hasUserAuthorizedCameraPermission();
            yield return coroutine;

            if (!(bool)coroutine.Current)
            {
                isInitWaiting = false;
                initCoroutine = null;

                if (onErrorOccurred != null)
                    onErrorOccurred.Invoke(ErrorCode.CAMERA_PERMISSION_DENIED);

                yield break;
            }
#endif

            if (createWebCamTexture)
            {
                float requestedFPS = this.requestedFPS;

                // Creates the camera
                var devices = WebCamTexture.devices;
                if (!string.IsNullOrEmpty(requestedDeviceName))
                {
                    if (int.TryParse(requestedDeviceName, out var requestedDeviceIndex))
                    {
                        if (requestedDeviceIndex >= 0 && requestedDeviceIndex < devices.Length)
                        {
                            webCamDevice = devices[requestedDeviceIndex];

                            if (Application.platform == RuntimePlatform.Android && webCamDevice.isFrontFacing == true)
                                requestedFPS = 15f;

                            webCamTexture = requestedFPS < 0
                                ? new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight)
                                : new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight,
                                    (int)requestedFPS);
                            
                            onFrameReceived?.Invoke(webCamTexture);
                        }
                    }
                    else
                    {
                        for (int cameraIndex = 0; cameraIndex < devices.Length; cameraIndex++)
                        {
                            if (devices[cameraIndex].name != requestedDeviceName) 
                                continue;
                            
                            webCamDevice = devices[cameraIndex];

                            if (Application.platform == RuntimePlatform.Android &&
                                webCamDevice.isFrontFacing == true)
                                requestedFPS = 15f;

                            webCamTexture = requestedFPS < 0
                                ? new WebCamTexture(webCamDevice.name, requestedWidth,
                                    requestedHeight)
                                : new WebCamTexture(webCamDevice.name, requestedWidth,
                                    requestedHeight, (int)requestedFPS);

                            onFrameReceived?.Invoke(webCamTexture);

                            break;
                        }
                    }

                    if (webCamTexture == null)
                        Debug.Log("Cannot find camera device " + requestedDeviceName + ".");
                }

                if (webCamTexture == null)
                {
                    // Checks how many and which cameras are available on the device
                    for (int cameraIndex = 0; cameraIndex < devices.Length; cameraIndex++)
                    {
#if UNITY_2018_3_OR_NEWER
                        if (devices[cameraIndex].kind != WebCamKind.ColorAndDepth &&
                            devices[cameraIndex].isFrontFacing == RequestedIsFrontFacing)
#else
                    if (devices[cameraIndex].isFrontFacing == requestedIsFrontFacing)
#endif
                        {
                            webCamDevice = devices[cameraIndex];

                            if (Application.platform == RuntimePlatform.Android && webCamDevice.isFrontFacing == true)
                                requestedFPS = 15f;

                            webCamTexture = requestedFPS < 0
                                ? new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight)
                                : new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight,
                                    (int)requestedFPS);

                            onFrameReceived?.Invoke(webCamTexture);
                            break;
                        }
                    }
                }

                if (webCamTexture == null)
                {
                    if (devices.Length > 0)
                    {
                        webCamDevice = devices[0];

                        if (Application.platform == RuntimePlatform.Android && webCamDevice.isFrontFacing == true)
                            requestedFPS = 15f;

                        webCamTexture = requestedFPS < 0
                            ? new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight)
                            : new WebCamTexture(webCamDevice.name, requestedWidth, requestedHeight, (int)requestedFPS);

                        onFrameReceived?.Invoke(webCamTexture);
                    }
                    else
                    {
                        isInitWaiting = false;
                        initCoroutine = null;
                        onErrorOccurred?.Invoke(ErrorCode.CAMERA_DEVICE_NOT_EXIST);
                        yield break;
                    }
                }

                // Starts the camera
                ((WebCamTexture)webCamTexture).Play();
            }

            if (webCamTexture == null)
            {
                isInitWaiting = false;
                initCoroutine = null;

                if (onErrorOccurred != null)
                    onErrorOccurred.Invoke(ErrorCode.CAMERA_DEVICE_NOT_EXIST);

                yield break;
            }

            int initFrameCount = 0;
            bool isTimeout = false;

            while (true)
            {
                if (initFrameCount > timeoutFrameCount)
                {
                    isTimeout = true;
                    break;
                }

                if (webCamTexture && webCamTexture is WebCamTexture { didUpdateThisFrame: true } or not WebCamTexture)
                {
                    if (webCamTexture is WebCamTexture t)
                    {
                        Debug.Log("WebCamTextureToMatHelper:: " + "devicename:" + t.deviceName + " name:" +
                                  webCamTexture.name + " width:" + webCamTexture.width + " height:" +
                                  webCamTexture.height +
                                  " fps:" + t.requestedFPS
                                  + " videoRotationAngle:" + t.videoRotationAngle +
                                  " videoVerticallyMirrored:" + t.videoVerticallyMirrored + " isFrongFacing:" +
                                  webCamDevice.isFrontFacing);
                    }

                    if (colors == null || colors.Length != webCamTexture.width * webCamTexture.height)
                        colors = new Color32[webCamTexture.width * webCamTexture.height];

                    baseMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);

                    if (baseColorFormat == outputColorFormat)
                    {
                        frameMat = baseMat;
                    }
                    else
                    {
                        frameMat = new Mat(baseMat.rows(), baseMat.cols(), CvType.CV_8UC(Channels(outputColorFormat)),
                            new Scalar(0, 0, 0, 255));
                    }

                    screenOrientation = Screen.orientation;
                    screenWidth = Screen.width;
                    screenHeight = Screen.height;

                    bool isRotatedFrame = false;
#if !UNITY_EDITOR && !(UNITY_STANDALONE || UNITY_WEBGL)
                    if (screenOrientation == ScreenOrientation.Portrait || screenOrientation == ScreenOrientation.PortraitUpsideDown)
                    {
                        if (!rotate90Degree)
                            isRotatedFrame = true;
                    }
                    else if (rotate90Degree)
                    {
                        isRotatedFrame = true;
                    }
#else
                    if (rotate90Degree)
                        isRotatedFrame = true;
#endif
                    if (isRotatedFrame)
                        rotatedFrameMat = new Mat(frameMat.cols(), frameMat.rows(),
                            CvType.CV_8UC(Channels(outputColorFormat)), new Scalar(0, 0, 0, 255));

                    isInitWaiting = false;
                    hasInitDone = true;
                    initCoroutine = null;

                    if (Initialized != null)
                        Initialized.Invoke();

                    break;
                }

                initFrameCount++;
                yield return null;
            }

            if (isTimeout)
            {
                if (createWebCamTexture)
                {
                    ((WebCamTexture)webCamTexture).Stop();
                    webCamTexture = null;
                }

                isInitWaiting = false;
                initCoroutine = null;

                if (onErrorOccurred != null)
                    onErrorOccurred.Invoke(ErrorCode.TIMEOUT);
            }
        }

        /// <summary>
        /// Checks camera permission state by coroutine.
        /// </summary>
        protected virtual IEnumerator hasUserAuthorizedCameraPermission()
        {
#if (UNITY_IOS || UNITY_WEBGL) && UNITY_2018_1_OR_NEWER
            UserAuthorization mode = UserAuthorization.WebCam;
            if (!Application.HasUserAuthorization(mode))
            {
                yield return RequestUserAuthorization(mode);
            }
            yield return Application.HasUserAuthorization(mode);
#elif UNITY_ANDROID && UNITY_2018_3_OR_NEWER
            string permission = UnityEngine.Android.Permission.Camera;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                yield return RequestUserPermission(permission);
            }

            yield return UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission);
#else
            yield return true;
#endif
        }

#if ((UNITY_IOS || UNITY_WEBGL) && UNITY_2018_1_OR_NEWER) || (UNITY_ANDROID && UNITY_2018_3_OR_NEWER)
        protected bool isUserRequestingPermission;
#endif

#if (UNITY_IOS || UNITY_WEBGL) && UNITY_2018_1_OR_NEWER
        protected virtual IEnumerator RequestUserAuthorization(UserAuthorization mode)
        {
            isUserRequestingPermission = true;
            yield return Application.RequestUserAuthorization(mode);

            float timeElapsed = 0;
            while (isUserRequestingPermission)
            {
                if (timeElapsed > 0.25f)
                {
                    isUserRequestingPermission = false;
                    yield break;
                }
                timeElapsed += Time.deltaTime;

                yield return null;
            }
            yield break;
        }
#elif UNITY_ANDROID && UNITY_2018_3_OR_NEWER
        protected virtual IEnumerator RequestUserPermission(string permission)
        {
            isUserRequestingPermission = true;
            UnityEngine.Android.Permission.RequestUserPermission(permission);

            float timeElapsed = 0;
            while (isUserRequestingPermission)
            {
                if (timeElapsed > 0.25f)
                {
                    isUserRequestingPermission = false;
                    yield break;
                }

                timeElapsed += Time.deltaTime;

                yield return null;
            }

            yield break;
        }
#endif

        /// <summary>
        /// Indicates whether this instance has been initialized.
        /// </summary>
        /// <returns><c>true</c>, if this instance has been initialized, <c>false</c> otherwise.</returns>
        public virtual bool IsInitialized()
        {
            return hasInitDone;
        }

        public virtual bool IsInitializing()
        {
            return isInitWaiting;
        }

        /// <summary>
        /// Starts the camera.
        /// </summary>
        public virtual void Play()
        {
            if (hasInitDone && createWebCamTexture)
                ((WebCamTexture)webCamTexture).Play();
            else if (!createWebCamTexture)
                Debug.LogWarning(
                    "WebCamTextureToMatHelper::Play() is not supported when autoFetchWebCamTexture is false.");
        }

        /// <summary>
        /// Pauses the active camera.
        /// </summary>
        public virtual void Pause()
        {
            if (hasInitDone && createWebCamTexture)
                ((WebCamTexture)webCamTexture).Pause();
        }

        /// <summary>
        /// Stops the active camera.
        /// </summary>
        public virtual void Stop()
        {
            if (hasInitDone && createWebCamTexture)
                ((WebCamTexture)webCamTexture).Stop();
            else if (!createWebCamTexture)
                Debug.LogWarning(
                    "WebCamTextureToMatHelper::Stop() is not supported when autoFetchWebCamTexture is false.");
        }

        public void SetWebCamTexture(Texture tex)
        {
            this.webCamTexture = tex;
            Initialize();
        }

        /// <summary>
        /// Indicates whether the active camera is currently playing.
        /// </summary>
        /// <returns><c>true</c>, if the active camera is playing, <c>false</c> otherwise.</returns>
        public virtual bool IsStreaming()
        {
            if (!hasInitDone)
                return false;
            if (webCamTexture is WebCamTexture t)
                return t.isPlaying;
            return true;
        }

        private readonly struct SimpleCameraFrameMat : ICameraFrame
        {
            private readonly Mat _m;
            private readonly float _fov;

            public SimpleCameraFrameMat(Mat m, float fov)
            {
                _m = m;
                _fov = fov;
            }

            public void Dispose()
            {
            }

            public Mat GetMat()
            {
                return _m;
            }

            public ReadOnlySpan<Color32> GetColors32()
            {
                if (_m is null || _m.IsDisposed)
                    return Array.Empty<Color32>();
                var tex = new Texture2D(_m.cols(), _m.rows(), TextureFormat.RGB24, false);
                try
                {
                    Utils.matToTexture2D(_m, tex);
                    return tex.GetPixels32();
                }
                finally
                {
                    Destroy(tex);
                }
            }

            public Vector2Int GetSize()
            {
                return new Vector2Int(_m.cols(), _m.rows());
            }

            public float GetFOV(ICameraFrame.FOVType type)
            {
                return _fov;
            }

            public bool ProvidesDepthData()
            {
                return false;
            }

            public float SampleDepth(int sampleX, int sampleY)
            {
                return -1;
            }

            public bool TryGetCameraRelativePoint(int sampleX, int sampleY, out Vector3 point)
            {
                point = default;
                return false;
            }
        }

        /// <summary>
        /// Gets the mat of the current frame.
        /// The Mat object's type is 'CV_8UC4' or 'CV_8UC3' or 'CV_8UC1' (ColorFormat is determined by the outputColorFormat setting).
        /// Please do not dispose of the returned mat as it will be reused.
        /// </summary>
        /// <returns>The mat of the current frame.</returns>
        public virtual ICameraFrame DequeueNextFrame()
        {
            if (!hasInitDone || !IsStreaming())
            {
                return null;
            }

            if (frameMat is null)
            {
                return null;
            }

            try
            {
                if (baseColorFormat == outputColorFormat)
                {
                    if (webCamTexture is Texture2D t2d)
                    {
                        Utils.texture2DToMat(t2d, frameMat, false);
                    }
                    else if (webCamTexture is WebCamTexture wct)
                    {
                        Utils.webCamTextureToMat(wct, frameMat, colors, false);
                    }
                }
                else
                {
                    if (webCamTexture is Texture2D t2d)
                    {
                        Utils.texture2DToMat(t2d, baseMat, false);
                        Imgproc.cvtColor(baseMat, frameMat, ColorConversionCodes(baseColorFormat, outputColorFormat));
                    }
                    else if (webCamTexture is WebCamTexture wct)
                    {
                        Utils.webCamTextureToMat(wct, baseMat, colors, false);
                    }

                    Imgproc.cvtColor(baseMat, frameMat, ColorConversionCodes(baseColorFormat, outputColorFormat));
                }

#if !UNITY_EDITOR && !(UNITY_STANDALONE || UNITY_WEBGL)
            if (rotatedFrameMat != null)
            {
                if (screenOrientation == ScreenOrientation.Portrait || screenOrientation == ScreenOrientation.PortraitUpsideDown)
                {
                    // (Orientation is Portrait, rotate90Degree is false)
                    if (webCamDevice.isFrontFacing)
                    {
                        FlipMat(frameMat, !flipHorizontal, !flipVertical);
                    }
                    else
                    {
                        FlipMat(frameMat, flipHorizontal, flipVertical);
                    }
                }
                else
                {
                    // (Orientation is Landscape, rotate90Degrees=true)
                    FlipMat(frameMat, flipVertical, flipHorizontal);
                }
                Core.rotate(frameMat, rotatedFrameMat, Core.ROTATE_90_CLOCKWISE);
                return new SimpleCameraFrameMat(rotatedFrameMat, fieldOfView);
            }
            else
            {
                if (screenOrientation == ScreenOrientation.Portrait || screenOrientation == ScreenOrientation.PortraitUpsideDown)
                {
                    // (Orientation is Portrait, rotate90Degree is ture)
                    if (webCamDevice.isFrontFacing)
                    {
                        FlipMat(frameMat, flipHorizontal, flipVertical);
                    }
                    else
                    {
                        FlipMat(frameMat, !flipHorizontal, !flipVertical);
                    }
                }
                else
                {
                    // (Orientation is Landscape, rotate90Degree is false)
                    FlipMat(frameMat, flipVertical, flipHorizontal);
                }
                return new SimpleCameraFrameMat(frameMat, fieldOfView);
            }
#else
                FlipMat(frameMat, flipVertical, flipHorizontal);

                if (rotatedFrameMat == null)
                    return new SimpleCameraFrameMat(frameMat, fieldOfView);

                Core.rotate(frameMat, rotatedFrameMat, Core.ROTATE_90_CLOCKWISE);
                return new SimpleCameraFrameMat(rotatedFrameMat, fieldOfView);

#endif
            }
            finally
            {
            }
        }

        /// <summary>
        /// Flips the mat.
        /// </summary>
        /// <param name="mat">Mat.</param>
        protected virtual void FlipMat(Mat mat, bool flipVertical, bool flipHorizontal)
        {
            //Since the order of pixels of WebCamTexture and Mat is opposite, the initial value of flipCode is set to 0 (flipVertical).
            int flipCode = 0;

            if (webCamTexture is WebCamTexture wtc)
            {
                if (webCamDevice.isFrontFacing)
                {
                    if (wtc.videoRotationAngle == 0 || wtc.videoRotationAngle == 90)
                    {
                        flipCode = -1;
                    }
                    else if (wtc.videoRotationAngle == 180 || wtc.videoRotationAngle == 270)
                    {
                        flipCode = int.MinValue;
                    }
                }
                else
                {
                    if (wtc.videoRotationAngle == 180 || wtc.videoRotationAngle == 270)
                    {
                        flipCode = 1;
                    }
                }
            }

            if (flipVertical)
            {
                if (flipCode == int.MinValue)
                {
                    flipCode = 0;
                }
                else if (flipCode == 0)
                {
                    flipCode = int.MinValue;
                }
                else if (flipCode == 1)
                {
                    flipCode = -1;
                }
                else if (flipCode == -1)
                {
                    flipCode = 1;
                }
            }

            if (flipHorizontal)
            {
                if (flipCode == int.MinValue)
                {
                    flipCode = 1;
                }
                else if (flipCode == 0)
                {
                    flipCode = -1;
                }
                else if (flipCode == 1)
                {
                    flipCode = int.MinValue;
                }
                else if (flipCode == -1)
                {
                    flipCode = 0;
                }
            }

            if (flipCode > int.MinValue)
            {
                Core.flip(mat, mat, flipCode);
            }
        }

        protected virtual int Channels(ColorFormat type)
        {
            switch (type)
            {
                case ColorFormat.GRAY:
                    return 1;
                case ColorFormat.RGB:
                case ColorFormat.BGR:
                    return 3;
                case ColorFormat.RGBA:
                case ColorFormat.BGRA:
                    return 4;
                default:
                    return 4;
            }
        }

        protected virtual int ColorConversionCodes(ColorFormat srcType, ColorFormat dstType)
        {
            if (srcType == ColorFormat.GRAY)
            {
                if (dstType == ColorFormat.RGB) return Imgproc.COLOR_GRAY2RGB;
                else if (dstType == ColorFormat.BGR) return Imgproc.COLOR_GRAY2BGR;
                else if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_GRAY2RGBA;
                else if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_GRAY2BGRA;
            }
            else if (srcType == ColorFormat.RGB)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_RGB2GRAY;
                else if (dstType == ColorFormat.BGR) return Imgproc.COLOR_RGB2BGR;
                else if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_RGB2RGBA;
                else if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_RGB2BGRA;
            }
            else if (srcType == ColorFormat.BGR)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_BGR2GRAY;
                else if (dstType == ColorFormat.RGB) return Imgproc.COLOR_BGR2RGB;
                else if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_BGR2RGBA;
                else if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_BGR2BGRA;
            }
            else if (srcType == ColorFormat.RGBA)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_RGBA2GRAY;
                else if (dstType == ColorFormat.RGB) return Imgproc.COLOR_RGBA2RGB;
                else if (dstType == ColorFormat.BGR) return Imgproc.COLOR_RGBA2BGR;
                else if (dstType == ColorFormat.BGRA) return Imgproc.COLOR_RGBA2BGRA;
            }
            else if (srcType == ColorFormat.BGRA)
            {
                if (dstType == ColorFormat.GRAY) return Imgproc.COLOR_BGRA2GRAY;
                else if (dstType == ColorFormat.RGB) return Imgproc.COLOR_BGRA2RGB;
                else if (dstType == ColorFormat.BGR) return Imgproc.COLOR_BGRA2BGR;
                else if (dstType == ColorFormat.RGBA) return Imgproc.COLOR_BGRA2RGBA;
            }

            return -1;
        }

        /// <summary>
        /// Gets the buffer colors.
        /// </summary>
        /// <returns>The buffer colors.</returns>
        public virtual Color32[] GetBufferColors()
        {
            return colors;
        }

        /// <summary>
        /// Cancel Init Coroutine.
        /// </summary>
        protected virtual void CancelInitCoroutine()
        {
            if (initCoroutine != null)
            {
                StopCoroutine(initCoroutine);
                ((IDisposable)initCoroutine).Dispose();
                initCoroutine = null;
            }
        }

        /// <summary>
        /// To release the resources.
        /// </summary>
        protected virtual void ReleaseResources()
        {
            isInitWaiting = false;
            hasInitDone = false;

            if (webCamTexture != null)
            {
                if (createWebCamTexture)
                {
                    ((WebCamTexture)webCamTexture).Stop();
                    Destroy(webCamTexture);
                    webCamTexture = null;
                }
            }

            if (frameMat != null)
            {
                frameMat.Dispose();
                frameMat = null;
            }

            if (baseMat != null)
            {
                baseMat.Dispose();
                baseMat = null;
            }

            if (rotatedFrameMat != null)
            {
                rotatedFrameMat.Dispose();
                rotatedFrameMat = null;
            }
        }

        /// <summary>
        /// Releases all resource used by the <see cref="WebCamTextureToMatHelper"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="WebCamTextureToMatHelper"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="WebCamTextureToMatHelper"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the <see cref="WebCamTextureToMatHelper"/> so
        /// the garbage collector can reclaim the memory that the <see cref="WebCamTextureToMatHelper"/> was occupying.</remarks>
        public virtual void Dispose()
        {
            if (colors != null)
                colors = null;

            if (isInitWaiting)
            {
                CancelInitCoroutine();
                ReleaseResources();
            }
            else if (hasInitDone)
            {
                ReleaseResources();

                if (Disposed != null)
                    Disposed.Invoke();
            }
        }
    }
}

#endif
#endif