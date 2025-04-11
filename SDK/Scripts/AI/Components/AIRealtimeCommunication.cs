/*
 * COPYRIGHT (C) 2025 Reach Cloud, LLC
 * ALL RIGHTS RESERVED
 * REDISTRIBUTION OR MODIFICATION OF THIS FILE IS PROHIBITED WITHOUT WRITTEN PERMISSION.
 */

#if !UNITY_WEBGL || UNITY_EDITOR

using UnityEngine;
using UnityEngine.Events;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent; // Using ConcurrentQueue for thread-safe operations
using MetaverseCloudEngine.Common.Enumerations;
#if MV_NATIVE_WEBSOCKETS
using NativeWebSocket;
#endif
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TriInspectorMVCE;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace MetaverseCloudEngine.Unity.AI.Components
{
    #region Function Definition Classes

    /// <summary>
    /// Enumerates the possible data types for parameters used in AI function calls.
    /// </summary>
    public enum AIRealtimeCommunicationFunctionParameterType
    {
        String,
        Float,
        Integer,
        Boolean,
        Vector2,
        Vector3,
        Vector4,
        Quaternion,
        Color,
        Color32,
        Enum,
    }

    /// <summary>
    /// Defines a single parameter for an AI-callable function, including its type,
    /// description, and events to invoke when the AI provides a value for it.
    /// </summary>
    [Serializable]
    public class AIRealtimeCommunicationFunctionParameter
    {
        /// <summary>
        /// The unique identifier for this parameter, used by the AI.
        /// </summary>
        [Required] public string parameterID = "";

        /// <summary>
        /// A description explaining the purpose of this parameter to the AI.
        /// </summary>
        [Required] public string description = "";

        /// <summary>
        /// The expected data type for this parameter's value.
        /// </summary>
        public AIRealtimeCommunicationFunctionParameterType type = AIRealtimeCommunicationFunctionParameterType.String;

        /// <summary>
        /// If the type is Enum, this list defines the possible string values.
        /// </summary>
        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Enum)]
        public List<string> enumValues = new();

        // --- Unity Events for different parameter types ---
        // These events are invoked on the main thread when the AI provides
        // a corresponding value for this parameter during a function call.

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.String)]
        public UnityEvent<string> onStringValue = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Float)]
        public UnityEvent<float> onFloatValue = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Integer)]
        public UnityEvent<int> onIntValue = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Boolean)]
        public UnityEvent<bool> onBoolValue = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Vector2)]
        public UnityEvent<Vector2> onVector2Value = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Vector3)]
        public UnityEvent<Vector3> onVector3Value = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Vector4)]
        public UnityEvent<Vector4> onVector4Value = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Quaternion)]
        public UnityEvent<Quaternion> onQuaternionValue = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Color)]
        public UnityEvent<Color> onColorValue = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Color32)]
        public UnityEvent<Color32> onColor32Value = new();

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Enum)]
        public UnityEvent<int> onEnumValue = new(); // Invoked with the index

        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Enum)]
        public UnityEvent<string> onEnumValueString = new(); // Invoked with the string value

        /// <summary>
        /// Provides specific formatting guidance for certain parameter types
        /// to help the AI structure its output correctly.
        /// </summary>
        /// <returns>A string describing the expected format, or null if none is needed.</returns>
        public string GetFormattingGuidance()
        {
            return type switch
            {
                AIRealtimeCommunicationFunctionParameterType.Vector2 => "x,y",
                AIRealtimeCommunicationFunctionParameterType.Vector3 => "x,y,z",
                AIRealtimeCommunicationFunctionParameterType.Vector4 => "x,y,z,w",
                AIRealtimeCommunicationFunctionParameterType.Quaternion => "x,y,z,w",
                AIRealtimeCommunicationFunctionParameterType.Color => "#RRGGBB",
                AIRealtimeCommunicationFunctionParameterType.Color32 => "#RRGGBBAA",
                _ => null,
            };
        }
    }

    /// <summary>
    /// Represents a function that the AI can invoke. Contains details about
    /// the function's purpose, parameters, and the UnityEvent to trigger when called.
    /// </summary>
    [Serializable]
    public class AIRealtimeCommunicationFunction
    {
        /// <summary>
        /// Identifier/name of the function as recognized by the AI. Must be unique.
        /// </summary>
        [Tooltip("Identifier/name of the function as recognized by the AI.")]
        public string functionID;

        /// <summary>
        /// Detailed description to help the AI understand when and why to call this function.
        /// </summary>
        [TextArea] [Tooltip("Description to help the AI decide when to call this function.")]
        public string functionDescription;

        /// <summary>
        /// The list of parameters that this function accepts.
        /// </summary>
        [Tooltip(
            "The parameters that this function accepts. Each parameter has a type, description, and optional enum values.")]
        public List<AIRealtimeCommunicationFunctionParameter> parameters = new();

        /// <summary>
        /// The UnityEvent invoked on the main thread when the AI calls this function (by its functionID).
        /// Parameter values are handled by events within AIRealtimeCommunicationFunctionParameter.
        /// </summary>
        [Tooltip("Event invoked when the AI calls this functionID.")]
        public UnityEvent onCalled = new();
    }

    #endregion

    /// <summary>
    /// Manages synchronous, real-time, two-way communication with an AI backend (like OpenAI's Realtime API).
    /// It handles streaming microphone audio input, receiving text and audio responses, processing function calls,
    /// and managing the connection lifecycle, all primarily within Unity's FixedUpdate loop.
    /// Token acquisition is handled asynchronously via a partial method implementation.
    /// </summary>
    [HideMonoScript]
    [RequireComponent(typeof(AudioSource))] // Require AudioSource for playback
    public partial class AIRealtimeCommunication : TriInspectorMonoBehaviour
    {
        private const string BetaHeaderName = "OpenAI-Beta";
        private const string BetaHeaderValue = "realtime=v1";
        private const string RealtimeEndpoint = "wss://api.openai.com/v1/realtime";

        [Tooltip("If true, the connection attempt begins automatically when the component starts or is enabled.")]
        [SerializeField]
        private bool connectOnStart = true;

        // --- Microphone Streaming ---
        [Header("Microphone")]
        [Range(8000, 48000)]
        [Tooltip(
            "The desired sample rate (in Hz) for microphone input. The actual rate might differ based on hardware.")]
        [SerializeField]
        private int micSampleRate = 16000;

        [Tooltip("How often (in seconds) microphone audio is sampled and sent to the AI.")]
        [SerializeField]
        private float sampleInterval = 0.2f;

        [Tooltip(
            "User setting to enable or disable microphone input streaming. Can be changed at runtime via the MicrophoneActive property.")]
        [SerializeField]
        [DisableInPlayMode]
        // Prevent accidental changes in Play mode Inspector
        private bool micActive = true; // User's preference set in Inspector.

        [Tooltip("If true, automatically enables the microphone if the AI response ends with a question mark ('?').")]
        [SerializeField]
        private bool enableMicOnQuestion = true;

        [Tooltip(
            "If true, automatically disables the microphone when the AI indicates the conversation is finished (e.g., response ends with ';').")]
        [SerializeField]
        private bool disableMicOnCommunicationFinished = true;

        // --- Output (GPT Response) ---
        [Header("Output (GPT Response)")]
        [TextArea(5, 10)]
        [Tooltip("The initial system prompt sent to the AI to define its role or context.")]
        [SerializeField]
        private string prompt = "You are a helpful assistant."; // Default prompt

        [Tooltip("The AudioSource component used to play back the AI's voice.")] [SerializeField]
        private AudioSource outputVoiceSource;

        [Tooltip("The desired voice preset for the AI's text-to-speech output.")] [SerializeField]
        private TextToSpeechVoicePreset outputVoice = TextToSpeechVoicePreset.Male;

        [Range(8000, 48000)]
        [Tooltip(
            "The sample rate (in Hz) requested for the AI's audio output. Affects playback speed/pitch if mismatched with actual received audio. Note: This is set during session creation, not session update.")]
        [SerializeField]
        private int outputSampleRate = 22050;

        // --- Function Calling ---
        [Header("Function Calling")]
        [Tooltip("A list defining the functions the AI is allowed to call within this Unity application.")]
        [SerializeField]
        private List<AIRealtimeCommunicationFunction> availableFunctions = new();

        // --- Debugging ---
        [Header("Debugging")]
        [Tooltip("Enable detailed logging of events and messages to the Unity console.")]
        [SerializeField]
        private bool logs = true;

        // --- Event Callbacks ---
        [Header("Event Callbacks")]
        [Tooltip("Invoked on the main thread when the WebSocket connection is successfully established.")]
        [SerializeField]
        private UnityEvent onConnected = new();

        [Tooltip("Invoked on the main thread when the WebSocket connection is closed or fails.")]
        [SerializeField]
        private UnityEvent onDisconnected = new();

        [Tooltip("Invoked on the main thread when microphone audio starts being sent after a period of silence.")]
        [SerializeField]
        private UnityEvent onMicStarted = new();

        [Tooltip(
            "Invoked on the main thread when the microphone stops sending data (either manually or automatically).")]
        [SerializeField]
        private UnityEvent onMicStopped = new();

        [Tooltip(
            "Invoked on the main thread when the AI requests vision processing via the 'vision_request' function.")]
        [SerializeField]
        private UnityEvent onVisionRequested = new();

        [Tooltip(
            "Invoked on the main thread when the vision processing (if requested) is complete (successfully or not).")]
        [SerializeField]
        private UnityEvent onVisionFinished = new();

        [Tooltip("Invoked on the main thread when the AI begins a new response turn (text or audio).")] [SerializeField]
        private UnityEvent onAIResponseStarted = new();

        [Tooltip("Invoked on the main thread with the complete transcribed text from the AI for a response turn.")]
        [SerializeField]
        private UnityEvent<string> onAIResponseString = new();

        [Tooltip("Invoked on the main thread when the AI finishes a response turn (stops sending audio/text delta).")]
        [SerializeField]
        private UnityEvent onAIResponseFinished = new();

        [Tooltip(
            "Invoked on the main thread when the AI signals the end of the interaction (e.g., response ends with ';') and the microphone is potentially disabled.")]
        [SerializeField]
        private UnityEvent onCommunicationFinished = new();

#if MV_NATIVE_WEBSOCKETS
        private WebSocket _websocket;
#endif
        private int _systemSampleRate; // Unity's audio engine output rate
        private AudioClip _micClip; // AudioClip capturing microphone input
        private readonly string _micDevice = null; // Use default system microphone

        private string _ephemeralToken; // Temporary token for authentication - Set via callback now
        private bool _pendingVision; // True if waiting for a vision response

        private bool _isMicRunning; // Actual state: true if Microphone.Start has been called and recording is active

        private float _sampleTimer; // Timer for sending microphone samples periodically
        private int _lastMicPos; // Last position read from the microphone buffer

        // Thread-safe queue for buffering incoming audio samples from the AI
        // Accessed by WebSocket thread (writing) and Audio thread (reading via OnAudioFilterRead)
        private readonly ConcurrentQueue<float> _streamBuffer = new ConcurrentQueue<float>();

        private bool _isAiSpeaking; // True if the AI is currently sending audio delta messages

        private string _currentTranscriptText = string.Empty; // Accumulates text delta for the current response turn

        private bool _isShuttingDown; // Flag to prevent actions during application quit/disable

        private AIAgent _visionHandler; // Dedicated AIAgent instance for handling vision requests

        private bool _isStarted; // True after Start() has been called
        private bool _connectCalled; // True if Connect() was called manually before Start()

        private int
            _responsesInProgress; // Counter for active 'response.created' events without a matching 'response.done'

        // Queue for actions that need to be executed on the main thread (FixedUpdate)
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        // State management for connection and reconnection logic
        private bool _isAcquiringToken; // True while waiting for the async token acquisition callback
        private bool _needsReconnect;
        private float _reconnectTimer;
        private const float ReconnectDelay = 2.0f;

        // State management for finishing speech playback
        private bool _isWaitingToFinishSpeaking;
        private string _finishedTranscript; // Store the transcript when waiting starts

        // A flag to indicate if the microphone initialization is pending.
        // This will help to prevent any garbled audio when the mic is not ready.
        private bool _micInitializationReadPending;
        
        [SerializeField]
        private float idleTimeout;   // Default: 0
        public float IdleTimeout
        {
            get => idleTimeout;
            set => idleTimeout = value;
        }

        private float _activityTimer;
        private bool _micActivationQueued; // Flag to indicate if mic activation is pending

        // --- Public Properties ---

        /// <summary>
        /// Gets the UnityEvent invoked when the WebSocket connection is established.
        /// </summary>
        public UnityEvent OnConnected => onConnected;

        /// <summary>
        /// Gets the UnityEvent invoked when the WebSocket connection is closed or fails.
        /// </summary>
        public UnityEvent OnDisconnected => onDisconnected;

        /// <summary>
        /// Gets the UnityEvent invoked when microphone audio starts being sent.
        /// </summary>
        public UnityEvent OnMicStarted => onMicStarted;

        /// <summary>
        /// Gets the UnityEvent invoked when the microphone stops sending data.
        /// </summary>
        public UnityEvent OnMicStopped => onMicStopped;

        /// <summary>
        /// Gets the UnityEvent invoked when the AI requests vision processing.
        /// </summary>
        public UnityEvent OnVisionRequested => onVisionRequested;

        /// <summary>
        /// Gets the UnityEvent invoked when vision processing completes.
        /// </summary>
        public UnityEvent OnVisionFinished => onVisionFinished;

        /// <summary>
        /// Gets the UnityEvent invoked when the AI begins a response turn.
        /// </summary>
        public UnityEvent OnAIResponseStarted => onAIResponseStarted;

        /// <summary>
        /// Gets the UnityEvent invoked with the final transcribed text of an AI response turn.
        /// </summary>
        public UnityEvent<string> OnAIResponseString => onAIResponseString;

        /// <summary>
        /// Gets the UnityEvent invoked when the AI finishes a response turn.
        /// </summary>
        public UnityEvent OnAIResponseFinished => onAIResponseFinished;

        /// <summary>
        /// Gets the UnityEvent invoked when the AI signals the end of the conversation.
        /// </summary>
        public UnityEvent OnCommunicationFinished => onCommunicationFinished;

        /// <summary>
        /// Gets the list of functions the AI can call, as defined in the Inspector.
        /// </summary>
        public List<AIRealtimeCommunicationFunction> AvailableFunctions => availableFunctions;

        /// <summary>
        /// Gets or sets whether the microphone should be actively streaming audio.
        /// Setting this property will attempt to start or stop the microphone accordingly,
        /// respecting the current connection state and AI speaking status.
        /// </summary>
        public bool MicrophoneActive
        {
            get => micActive;
            set
            {
                if ((micActive == value 
#if MV_NATIVE_WEBSOCKETS
                     && _websocket.State != WebSocketState.Closed
#endif
                     ) || _micActivationQueued) 
                {
                    if (_micActivationQueued)
                        micActive = value; // Mic activation is queued, so update the property
                    return; // No change
                }

                _micActivationQueued = true;
                micActive = value;
                if (logs) Log($"MicrophoneActive set to: {micActive}");

                // Enqueue the action to be processed in FixedUpdate
                _mainThreadActions.Enqueue(() =>
                {
#if MV_NATIVE_WEBSOCKETS
                    _micActivationQueued = false;
                    
                    if (micActive)
                    {
                        var isConnectingOrOpen = _websocket != null &&
                            (_websocket.State == WebSocketState.Connecting || _websocket.State == WebSocketState.Open);
                        if (!isConnectingOrOpen && !_isAcquiringToken)
                        {
                            // If the socket is disconnected (not open/connecting) and not acquiring token, connect now
                            Connect();
                        }
                    }
#endif
                    if (!micActive)
                    {
                        // User explicitly disabled the mic
                        StopMic();
                    }
                    else
                    {
#if MV_NATIVE_WEBSOCKETS
                        // User re-enabled the mic, try to start it if conditions allow
                        if (CanStartMic()) // Use helper function to check all conditions
                        {
                            StartMic();
                        }
#endif
                    }
                });
            }
        }

        /// <summary>
        /// Provides access to the internal AIAgent used for processing vision requests.
        /// Lazily initializes the agent if needed. Returns null if shutting down or not playing.
        /// </summary>
        public AIAgent VisionHandler
        {
            get
            {
                if (_isShuttingDown || !Application.isPlaying)
                    return null;

                if (!_visionHandler)
                {
                    if (logs) Log("Initializing Vision Handler AIAgent...");
                    _visionHandler = new GameObject("MVCE_Realtime_Vision_Handler").AddComponent<AIAgent>();
                    _visionHandler.gameObject.hideFlags = HideFlags.HideInHierarchy; // Keep scene clean
                    _visionHandler.FlushMemory(); // Start fresh

                    // Hook up vision events to our internal handlers (which will enqueue main thread actions)
                    _visionHandler.OnThinkingStarted.AddListener(HandleVisionThinkingStarted);
                    _visionHandler.OnThinkingFinished.AddListener(HandleVisionThinkingFinished);
                    _visionHandler.OnResponse.AddListener(HandleVisionResponse);
                    _visionHandler.OnResponseFailed.AddListener(HandleVisionResponseFailed);

                    // Configure the vision agent's prompt
                    _visionHandler.IntelligencePreset = AiCharacterIntelligencePreset.PreferSpeed;
                    _visionHandler.Prompt =
                        "You are assisting another AI model allowing it to process vision requests. " +
                        "You will receive a short prompt describing the output that is needed from the vision AI. " +
                        "Your job is to process this request and return a response to the other AI model.";
                    _visionHandler.SampleData = "The shirt's color is green and has a pocket on the left side.";
                    if (logs) Log("Vision Handler AIAgent Initialized.");
                }

                return _visionHandler;
            }
        }

        /// <summary>
        /// Indicates if the component is currently busy with communication tasks
        /// (e.g., AI speaking, mic running, processing vision, connecting, waiting for response or token).
        /// </summary>
        public bool IsProcessing {
            get {
                var isConnecting = false;
#if MV_NATIVE_WEBSOCKETS
                isConnecting = _websocket?.State == WebSocketState.Connecting;
#endif

                return _isAiSpeaking ||
                       _isMicRunning ||
                       _responsesInProgress > 0 ||
                       _pendingVision ||
                       _isAcquiringToken ||
                       _needsReconnect ||
                       isConnecting ||
                       _isWaitingToFinishSpeaking;
            }
        }


        #region Unity Lifecycle

        /// <summary>
        /// Called when the script instance is first enabled. Sets up initial state.
        /// </summary>
        private void Awake()
        {
            // Ensure AudioSource exists if not assigned
            if (outputVoiceSource == null)
            {
                outputVoiceSource = GetComponent<AudioSource>();
                if (outputVoiceSource == null) // Add if still missing
                {
                    if (logs) LogWarning("AudioSource component not found or assigned. Adding one.");
                    outputVoiceSource = gameObject.AddComponent<AudioSource>();
                }
            }

            outputVoiceSource.playOnAwake = false; // Ensure it doesn't play automatically
        }

        /// <summary>
        /// Called before the first frame update. Initiates connection if configured.
        /// </summary>
        private void Start()
        {
            _isStarted = true;
            // Needs to happen on main thread, so queue it.
            _mainThreadActions.Enqueue(() =>
            {
                if (_isShuttingDown)
                    return;
                
                if (connectOnStart || _connectCalled)
                    Connect();
            });
        }

        /// <summary>
        /// Called every fixed framerate frame. Processes main thread actions,
        /// microphone sampling, and state machine updates.
        /// </summary>
        private void FixedUpdate()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                return;
            
            if (_isShuttingDown) return;

            // 1. Process any actions queued from background threads (WebSocket, Vision, Token Acquisition)
            while (_mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    LogError($"Error executing main thread action: {e.Message}\n{e.StackTrace}");
                }
            }

            // 2. Dispatch messages from WebSocket (must be called regularly on main thread)
#if MV_NATIVE_WEBSOCKETS
            _websocket?.DispatchMessageQueue();
#endif

            // 3. Process microphone sampling if active
            ProcessMicrophoneSampling();

            // 4. Update state machines (reconnection, finishing speech)
            UpdateReconnectionState();
            UpdateSpeakingFinishedState();

            UpdateIdleTimer();
        }

        /// <summary>
        /// Idle activity check: If we've gone idleDisconnectTime seconds
        /// without sending or receiving from the WebSocket, disconnect.
        /// </summary>
        private void UpdateIdleTimer()
        {
#if MV_NATIVE_WEBSOCKETS
            if (_websocket != null && _websocket.State == WebSocketState.Open && idleTimeout > 0f)
            {
                _activityTimer += Time.fixedDeltaTime;
                if (_activityTimer >= idleTimeout)
                {
                    Log($"Idle timeout reached ({idleTimeout}s). Closing connection due to inactivity.");
                    DisconnectInternal();
                }
            }
#endif
        }

        /// <summary>
        /// Called when the application is quitting. Sets shutdown flag.
        /// </summary>
        private void OnApplicationQuit()
        {
            Log("Application quitting. Setting shutdown flag.");
            _isShuttingDown = true;
            DisconnectInternal(); // Attempt immediate cleanup
        }

        /// <summary>
        /// Called when the component is enabled. Initiates connection if configured and already started.
        /// </summary>
        private void OnEnable()
        {
            _isShuttingDown = false; // Reset shutdown flag
            // Needs to happen on main thread, so queue it.
            _mainThreadActions.Enqueue(() =>
            {
                // Only connect if Start() has run and connectOnStart is true
                if (_isStarted && connectOnStart)
                    Connect();
            });
        }

        /// <summary>
        /// Called when the component is disabled or destroyed. Sets shutdown flag and disconnects.
        /// </summary>
        private void OnDisable()
        {
            Log("Component disabled. Setting shutdown flag and disconnecting.");
            _isShuttingDown = true;
            // Enqueue disconnect to ensure it runs on main thread relative to other actions
            _mainThreadActions.Enqueue(DisconnectInternal);
        }

        /// <summary>
        /// Called when the GameObject is destroyed. Ensures final disconnection.
        /// </summary>
        private void OnDestroy()
        {
            Log("Component destroyed. Ensuring disconnection.");
            _isShuttingDown = true;
            DisconnectInternal(); // Attempt immediate cleanup without queueing
            if (_visionHandler) // Clean up vision handler GameObject
            {
                Destroy(_visionHandler.gameObject);
                _visionHandler = null;
            }
        }

        #endregion

        #region WebSocket Connection Management

        /// <summary>
        /// Initiates the connection process by first asynchronously acquiring the ephemeral token.
        /// Called from FixedUpdate context.
        /// </summary>
        private void InitiateConnection()
        {
            if (_isShuttingDown) return;
            if (_isAcquiringToken)
            {
                 Log("Connection attempt ignored, already acquiring token.");
                 return;
            }

            var isConnectingOrOpen = false;
#if MV_NATIVE_WEBSOCKETS
            isConnectingOrOpen = _websocket != null &&
                (_websocket.State == WebSocketState.Connecting || _websocket.State == WebSocketState.Open);
#endif

            if(isConnectingOrOpen)
            {
                Log("Connection attempt ignored, already connecting or open.");
                return;
            }

            Log("Initiating connection - Step 1: Acquiring Ephemeral Token...");
            _isAcquiringToken = true; // Set flag

            try
            {
                AcquireEphemeralTokenImplementation();
            }
            catch (Exception e)
            {
                LogError($"Error starting token acquisition process: {e.Message}");
                 _mainThreadActions.Enqueue(() => HandleTokenAcquisitionFailed("Failed to start token acquisition"));
            }
        }

        /// <summary>
        /// Asynchronously acquires the necessary ephemeral token for authentication.
        /// **PARTIAL METHOD:** Implementation must enqueue `HandleTokenAcquired(token)` or `HandleTokenAcquisitionFailed(errorMsg)`.
        /// </summary>
        partial void AcquireEphemeralTokenImplementation();

        /// <summary>
        /// **CALLBACK (Main Thread):** Called when the token is acquired successfully. Proceeds to WebSocket connection.
        /// </summary>
        private void HandleTokenAcquired(string acquiredToken) {
             if (_isShuttingDown) return;
             if (!_isAcquiringToken) { LogWarning("HandleTokenAcquired called, but was not in acquiring state. Ignoring."); return; }
             if (string.IsNullOrEmpty(acquiredToken)) { HandleTokenAcquisitionFailed("Acquired token is null or empty."); return; }

             Log("Ephemeral token acquired successfully. Proceeding to Step 2: WebSocket Connection.");
             _ephemeralToken = acquiredToken;
             _isAcquiringToken = false; // Clear flag *before* starting next step
             ConnectWebSocket();
        }

        /// <summary>
        /// **CALLBACK (Main Thread):** Called if token acquisition fails.
        /// </summary>
        private void HandleTokenAcquisitionFailed(string errorMessage) {
             if (_isShuttingDown) return;
             if (!_isAcquiringToken && !string.IsNullOrEmpty(_ephemeralToken)) {
                 LogError($"Token acquisition failed unexpectedly (State: Not Acquiring, Token Exists? {!string.IsNullOrEmpty(_ephemeralToken)}): {errorMessage}");
             }
             else if (_isAcquiringToken) {
                 LogError($"Ephemeral token acquisition failed: {errorMessage}");
                 _ephemeralToken = null;
                 _isAcquiringToken = false;
                 HandleConnectionFailure("Token acquisition failed");
             }
             else { LogWarning($"HandleTokenAcquisitionFailed called unexpectedly: {errorMessage}"); }
        }

        /// <summary>
        /// Sets up and connects the WebSocket using the acquired token. Runs on the main thread.
        /// </summary>
        private void ConnectWebSocket() {
             if (_isShuttingDown || string.IsNullOrEmpty(_ephemeralToken)) { LogError("ConnectWebSocket called during shutdown or without a token. Aborting."); return; }

#if MV_NATIVE_WEBSOCKETS
            if (_websocket != null)
            {
                LogWarning("Cleaning up existing websocket before creating a new one.");
                _websocket.OnOpen -= HandleWebSocketOpen;
                _websocket.OnError -= HandleWebSocketError;
                _websocket.OnClose -= HandleWebSocketClose;
                _websocket.OnMessage -= HandleWebSocketMessage;
                try { _websocket.CancelConnection(); } catch { /* ignored */ }
                _websocket = null;
            }

            Log($"Attempting to connect WebSocket to {RealtimeEndpoint}");
            try
            {
                 _websocket = new WebSocket(RealtimeEndpoint, new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_ephemeralToken}" },
                    { BetaHeaderName, BetaHeaderValue }
                });

                _websocket.OnOpen += HandleWebSocketOpen;
                _websocket.OnError += HandleWebSocketError;
                _websocket.OnClose += HandleWebSocketClose;
                _websocket.OnMessage += HandleWebSocketMessage;

                _websocket.Connect();
                Log("WebSocket Connect() method called.");

                // Reset activity timer on connect
                _activityTimer = 0f;
            }
            catch (Exception e)
            {
                LogError($"WebSocket initialization/connection error: {e.Message}");
                HandleConnectionFailure("WebSocket setup error");
            }
#else
            LogError("MV_NATIVE_WEBSOCKETS is not defined. Cannot establish WebSocket connection.");
            HandleConnectionFailure("Missing dependency");
#endif
        }

        /// <summary>
        /// Handles WebSocket open event (from WebSocket thread). Enqueues `ProcessWebSocketOpen`.
        /// </summary>
        private void HandleWebSocketOpen() 
        {
            // Reset timer because we just had activity
            _activityTimer = 0f;

            _mainThreadActions.Enqueue(ProcessWebSocketOpen);
        }

        /// <summary>
        /// Processes WebSocket open event (runs on main thread).
        /// </summary>
        private void ProcessWebSocketOpen()
        {
            if (_isShuttingDown) return;

            var isOpen = false;
#if MV_NATIVE_WEBSOCKETS
            isOpen = _websocket?.State == WebSocketState.Open;
#endif

            if (!isOpen)
            {
#if MV_NATIVE_WEBSOCKETS
                LogWarning("ProcessWebSocketOpen called but socket is not open. Ignoring.");
#else
                LogWarning("ProcessWebSocketOpen called but MV_NATIVE_WEBSOCKETS is not defined.");
#endif
                return;
            }

            Log("WebSocket connection established successfully!");
            _needsReconnect = false; // Clear any pending reconnect state

            if (outputVoiceSource) { outputVoiceSource.volume = 1f; }
            SendSessionUpdate();
            if (CanStartMic()) { StartMic(); } // Start mic if conditions met

            try { onConnected?.Invoke(); }
            catch (Exception e) { LogError($"Error in onConnected event handler: {e.Message}"); }
        }

        /// <summary>
        /// Handles WebSocket error event (from WebSocket thread). Enqueues logging.
        /// </summary>
        private void HandleWebSocketError(string errMsg) { _mainThreadActions.Enqueue(() => LogError($"WebSocket Error Reported: {errMsg}")); }

// Method definition requires WebSocketCloseCode type, so wrap the whole method
#if MV_NATIVE_WEBSOCKETS
        /// <summary>
        /// Handles WebSocket close event (from WebSocket thread). Enqueues `ProcessWebSocketClose`.
        /// </summary>
        private void HandleWebSocketClose(WebSocketCloseCode code) { _mainThreadActions.Enqueue(() => ProcessWebSocketClose(code)); }

        /// <summary>
        /// Processes WebSocket close event (runs on main thread).
        /// </summary>
        private void ProcessWebSocketClose(WebSocketCloseCode code)
        {
            if (_isShuttingDown) { Log($"WebSocket closed (Code: {code}) during shutdown/disconnect. No further action."); return; }
            if (_isAcquiringToken) {
                LogWarning($"WebSocket closed (Code: {code}) while acquiring token. Potential race condition or token issue?");
                if (_websocket != null) {
                    _websocket.OnOpen -= HandleWebSocketOpen;
                    _websocket.OnError -= HandleWebSocketError;
                    _websocket.OnClose -= HandleWebSocketClose;
                    _websocket.OnMessage -= HandleWebSocketMessage;
                    _websocket = null;
                }
                ResetCommunicationState();
                try { onDisconnected?.Invoke(); } catch (Exception e) { LogError($"Error in onDisconnected handler: {e.Message}"); }
                return;
            }

            LogWarning($"WebSocket connection closed unexpectedly (Code: {code}).");
            if (outputVoiceSource) { outputVoiceSource.Stop(); outputVoiceSource.volume = 0f; }

            if (_websocket != null)
            {
                _websocket.OnOpen -= HandleWebSocketOpen;
                _websocket.OnError -= HandleWebSocketError;
                _websocket.OnClose -= HandleWebSocketClose;
                _websocket.OnMessage -= HandleWebSocketMessage;
                _websocket = null;
            }

            ResetCommunicationState();
            try { onDisconnected?.Invoke(); } catch (Exception e) { LogError($"Error in onDisconnected event handler: {e.Message}"); }

            if (!_isShuttingDown) { Log($"Attempting to reconnect in {ReconnectDelay} seconds..."); StartReconnectionAttempt(); }
        }
#endif // <-- End method wraps for HandleWebSocketClose and ProcessWebSocketClose


        /// <summary>
        /// Cleans up WebSocket connection and associated state. Internal use. Runs on Main Thread via Queue or directly.
        /// </summary>
        private void DisconnectInternal()
        {
            Log("DisconnectInternal called.");
            _needsReconnect = false;
            _isAcquiringToken = false;
            _ephemeralToken = null;
            _activityTimer = 0;

#if MV_NATIVE_WEBSOCKETS
            if (_websocket != null)
            {
                Log($"Closing WebSocket connection. Current state: {_websocket.State}");
                _websocket.OnOpen -= HandleWebSocketOpen;
                _websocket.OnMessage -= HandleWebSocketMessage;
                _websocket.OnError -= HandleWebSocketError;
                _websocket.OnClose -= HandleWebSocketClose;

                if (_websocket.State == WebSocketState.Open || _websocket.State == WebSocketState.Connecting)
                {
                    try { _websocket.Close(); } catch (Exception e) { LogError($"Error while closing websocket: {e.Message}"); }
                }
                _websocket = null;
            }
#endif
            ResetCommunicationState();

            if (_visionHandler) { Log("Destroying Vision Handler GameObject."); Destroy(_visionHandler.gameObject); _visionHandler = null; }
            StopMic();
            if (outputVoiceSource)
            {
                Log("Stopping output audio source.");
                outputVoiceSource.Stop();
                if (outputVoiceSource.clip != null && outputVoiceSource.clip.name == "StreamingClip") { Destroy(outputVoiceSource.clip); }
                outputVoiceSource.clip = null;
            }
            Log("DisconnectInternal finished.");
        }

        /// <summary>
        /// Resets flags related to the active communication state.
        /// </summary>
        private void ResetCommunicationState()
        {
            Log("Resetting communication state flags.");
            _isAiSpeaking = false;
            _currentTranscriptText = string.Empty;
            _responsesInProgress = 0;
            _pendingVision = false;
            _isWaitingToFinishSpeaking = false;
            _streamBuffer.Clear();
            if (!_isShuttingDown)
                onCommunicationFinished?.Invoke();
        }

        /// <summary>
        /// Sets flags to initiate the reconnection process timer.
        /// </summary>
        private void StartReconnectionAttempt()
        {
             var isConnecting = false;
#if MV_NATIVE_WEBSOCKETS
            isConnecting = _websocket is { State: WebSocketState.Connecting };
#endif

            if (_isShuttingDown || _needsReconnect || _isAcquiringToken || isConnecting) return;

            _needsReconnect = true;
            _reconnectTimer = ReconnectDelay;
            Log($"Reconnection timer started ({_reconnectTimer}s).");
        }

        /// <summary>
        /// Called from FixedUpdate to manage the reconnection timer and trigger a connection attempt.
        /// </summary>
        private void UpdateReconnectionState()
        {
            if (_needsReconnect && !_isShuttingDown)
            {
                _reconnectTimer -= Time.fixedDeltaTime;
                if (_reconnectTimer <= 0)
                {
                    _needsReconnect = false;
                    Log("Reconnection timer elapsed. Attempting to reconnect now...");
                    DisconnectInternal();
                    InitiateConnection();
                }
            }
        }

        /// <summary>
        /// Handles connection failures by logging, invoking disconnected event, and potentially starting reconnect.
        /// </summary>
        private void HandleConnectionFailure(string reason)
        {
            LogError($"Connection failed: {reason}");
            ResetCommunicationState();
            try { onDisconnected?.Invoke(); } catch (Exception e) { LogError($"Error in onDisconnected handler: {e.Message}"); }
            if (!_isShuttingDown) { Log($"Attempting to reconnect after failure in {ReconnectDelay} seconds..."); StartReconnectionAttempt(); }
        }

        #endregion

        #region Session Update (Sending Configuration and Tools)

        /// <summary>
        /// Sends the 'session.update' message to the AI, configuring modalities (text, audio)
        /// and defining the available function-calling tools.
        /// Called from FixedUpdate context after connection is established.
        /// </summary>
        private void SendSessionUpdate()
        {
#if MV_NATIVE_WEBSOCKETS
            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                LogWarning("Cannot send session update: WebSocket is not open.");
                return;
            }

            Log("Building session update message with tools...");
            var toolList = availableFunctions.Select(f => new
                {
                    type = "function",
                    name = f.functionID,
                    description = string.IsNullOrEmpty(f.functionDescription)
                        ? $"Executes the '{f.functionID}' action." : f.functionDescription,
                    parameters = f.parameters.Count > 0 ? new
                        {
                            type = "object",
                            properties = f.parameters.ToDictionary(p => p.parameterID, p => new
                            {
                                type = p.type switch {
                                    AIRealtimeCommunicationFunctionParameterType.Float => "number",
                                    AIRealtimeCommunicationFunctionParameterType.Integer => "number",
                                    AIRealtimeCommunicationFunctionParameterType.Boolean => "boolean",
                                    _ => "string"
                                },
                                description = BuildParameterDescription(p),
                                @enum = p.type == AIRealtimeCommunicationFunctionParameterType.Enum && p.enumValues.Count > 0 ? p.enumValues.ToArray() : null
                            })
                        } : null,
                })
                .Cast<object>()
                .ToList();

            toolList.Add(new
            {
                type = "function",
                name = "vision_request",
                description = "Call this function when you need to understand the visual scene or answer a question based on what the user is seeing. Provide a concise prompt describing what visual information you need.",
                parameters = new { type = "object", properties = new { vision_request = new { type = "string", description = "A short prompt describing the visual information needed (e.g., 'What color is the object the user is pointing at?', 'Describe the current scene.')" } }, required = new[] { "vision_request" } }
            });

            // Construct the session object based on valid 'session.update' parameters
            var sessionConfig = new Dictionary<string, object>
            {
                { "modalities", new[] { "text", "audio" } },
                { "input_audio_format", "pcm16" },
                { "output_audio_format", "pcm16" },
                { "turn_detection", new Dictionary<string, object> { // Changed to Dictionary for modification
                    { "type", "server_vad" },
                    { "create_response", false } // Turn off automatic response creation
                    // Default silence/threshold values will be used if not specified here
                }},
                { "tools", toolList },
                { "tool_choice", "auto" },
                { "input_audio_transcription", new { model = "whisper-1" } },
            };

            var sessionUpdatePayload = new
            {
                type = "session.update",
                session = sessionConfig.Where(kvp => kvp.Value != null)
                                       .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            string jsonPayload;
            try
            {
                 jsonPayload = JsonConvert.SerializeObject(sessionUpdatePayload, Formatting.None,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }); 
            }
            catch (Exception e) { LogError($"Failed to serialize session update message: {e.Message}"); return; }

            Log("Sending session.update message...");
            _websocket.SendText(jsonPayload);
            // Reset idle timer on activity
            _activityTimer = 0f;

            Log("session.update message sent.");

#else
            LogWarning("Cannot send session update: MV_NATIVE_WEBSOCKETS not defined.");
#endif
        }

        /// <summary>
        /// Helper to build the description string for a function parameter, including type and formatting hints.
        /// </summary>
        private string BuildParameterDescription(AIRealtimeCommunicationFunctionParameter p)
        {
            var sb = new StringBuilder(p.description);
            sb.Append($" (Type: {p.type}");
            var format = p.GetFormattingGuidance();
            if (!string.IsNullOrEmpty(format)) { sb.Append($", Format: '{format}'"); }
            sb.Append(")");
            return sb.ToString();
        }

        #endregion

        #region Microphone Handling

        /// <summary>
        /// Checks conditions and starts the microphone recording process. Runs on Main Thread.
        /// </summary>
        private void StartMic()
        {
            if (_isMicRunning) { return; } // Already running
            if (!micActive) { LogWarning("StartMic called, but MicrophoneActive is false."); return; }

            var socketOpen = false;
#if MV_NATIVE_WEBSOCKETS
            socketOpen = _websocket is { State: WebSocketState.Open };
#endif

            if (!socketOpen) { LogWarning("StartMic called, but WebSocket is not open."); return; }

            // Check all potential blocking states
            if (_isAiSpeaking || _pendingVision || _isWaitingToFinishSpeaking || _isAcquiringToken) {
                Log($"StartMic called, but blocked by: AI Speaking({_isAiSpeaking}), Pending Vision({_pendingVision}), Finishing Speech({_isWaitingToFinishSpeaking}), Acquiring Token({_isAcquiringToken}). Mic start deferred.");
                return;
            }

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Log("Requesting Android microphone permission...");
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += (permissionString) => { if (permissionString == Permission.Microphone) { Log("Microphone permission granted by user."); _mainThreadActions.Enqueue(StartMicClip); } };
                callbacks.PermissionDenied += (permissionString) => { if (permissionString == Permission.Microphone) { LogWarning("Microphone permission denied by user."); _mainThreadActions.Enqueue(() => micActive = false); } };
                callbacks.PermissionDeniedAndDontAskAgain += (permissionString) => { if (permissionString == Permission.Microphone) { LogWarning("Microphone permission permanently denied by user."); _mainThreadActions.Enqueue(() => micActive = false); } };
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                return; // Wait for callback
            }
#endif
            StartMicClip(); // If not Android or permission granted
        }

        /// <summary>
        /// Starts the actual Microphone.Start() process. Runs on Main Thread.
        /// </summary>
        private void StartMicClip()
        {
            if (_isMicRunning || _isShuttingDown) return;

            Log($"Attempting to start microphone '{_micDevice ?? "Default"}' with rate {micSampleRate}Hz.");
            try
            {
                if (Microphone.IsRecording(_micDevice)) { LogWarning("Microphone was already recording. Stopping it before restarting."); Microphone.End(_micDevice); }
                if (_micClip != null) { Destroy(_micClip); _micClip = null; }

                _micClip = Microphone.Start(_micDevice, true, 10, micSampleRate); // Keep buffer duration reasonable (e.g., 10s)
                if (_micClip == null) { LogError("Failed to start microphone. Microphone.Start returned null."); micActive = false; return; }

                // Check recording state with retries (synchronous delay - consider alternatives if problematic)
                if (!Microphone.IsRecording(_micDevice)) { LogError("Microphone failed to enter recording state."); Microphone.End(_micDevice); Destroy(_micClip); _micClip = null; micActive = false; return; }

                _sampleTimer = 0f;
                _isMicRunning = true;
                _hasSentFirstAudioChunk = false;
                _micInitializationReadPending = true;
                Log("Microphone recording started successfully.");
            }
            catch (Exception e)
            {
                LogError($"Exception starting microphone: {e.Message}\n{e.StackTrace}");
                if (_micClip != null) Destroy(_micClip); _micClip = null; _isMicRunning = false; micActive = false;
            }
        }

        /// <summary>
        /// Stops the microphone recording process. Runs on Main Thread.
        /// </summary>
        private void StopMic()
        {
            if (!_isMicRunning) { return; }

            Log("Stopping microphone recording...");
            var wasSendingAudio = _hasSentFirstAudioChunk;
            _isMicRunning = false;
            _hasSentFirstAudioChunk = false;

            try
            {
                if (Microphone.IsRecording(_micDevice)) { Microphone.End(_micDevice); Log("Microphone.End() called."); }
                else { LogWarning("StopMic called, but Microphone.IsRecording was already false."); }
                if (_micClip != null) { Destroy(_micClip); _micClip = null; Log("Microphone AudioClip destroyed."); }
            }
            catch (Exception e) { LogError($"Exception stopping microphone: {e.Message}"); }
            finally
            {
                _lastMicPos = 0;
                if (wasSendingAudio) {
                    try { onMicStopped?.Invoke(); Log("onMicStopped event invoked."); }
                    catch (Exception e) { LogError($"Error in onMicStopped event handler: {e.Message}"); }
                } else { Log("Mic stopped, but no audio was sent since last start, so onMicStopped was not invoked."); }
            }
        }

        /// <summary>
        /// Called from FixedUpdate to check for new microphone data and process it.
        /// </summary>
        private void ProcessMicrophoneSampling()
        {
            if (!_isMicRunning || _micClip == null) return;

            _sampleTimer += Time.fixedDeltaTime;
            if (_sampleTimer >= sampleInterval)
            {
                _sampleTimer -= sampleInterval;
                if (!Microphone.IsRecording(_micDevice))
                {
                    LogWarning("Microphone stopped recording unexpectedly. Restarting...");
                    StopMic();
                    StartMic();
                    return;
                }
                ProcessAudioFrame();
            }
        }

        /// <summary>
        /// Reads the latest audio data chunk from the microphone buffer and sends it. Runs on Main Thread.
        /// </summary>
        private void ProcessAudioFrame()
        {
            if (_micClip == null || !_isMicRunning) return;

            try
            {
                var currentPos = Microphone.GetPosition(_micDevice);
                
                // Perform initial synchronization read without sending data
                if (_micInitializationReadPending)
                {
                    _lastMicPos = currentPos; // Sync position to whatever is current *after* starting
                    _micInitializationReadPending = false; // Mark synchronization as complete
                    Log("Initial microphone position synchronized. Skipping first frame send.");
                    return; // Exit here, don't process/send this initial (potentially noisy) frame
                }

                var samplesAvailable = (currentPos - _lastMicPos + _micClip.samples) % _micClip.samples; // Handles wrap-around correctly

                if (samplesAvailable <= 0) { return; } // No new data

                var samples = new float[samplesAvailable];
                _micClip.GetData(samples, _lastMicPos); // Reads available samples from last position

                _lastMicPos = currentPos; // Update last position *after* reading
                SendAudioChunk(samples);
            }
            catch (Exception e) { LogError($"Error processing audio frame: {e.Message}\n{e.StackTrace}"); }
        }

        /// <summary>
        /// Converts float audio samples (-1.0 to 1.0) into 16-bit PCM byte array.
        /// </summary>
        private static byte[] ConvertFloatsToPCM16Bytes(float[] samples)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<byte>();
            var pcmBytes = new byte[samples.Length * 2];
            var byteIndex = 0;
            foreach (var t in samples)
            {
                var pcmValue = (short)(Mathf.Clamp(t, -1.0f, 1.0f) * 32767f);
                pcmBytes[byteIndex++] = (byte)(pcmValue & 0xFF);
                pcmBytes[byteIndex++] = (byte)((pcmValue >> 8) & 0xFF);
            }
            return pcmBytes;
        }

        /// <summary>
        /// Sends a chunk of processed audio data (PCM16, Base64 encoded) over the WebSocket. Runs on Main Thread.
        /// </summary>
        private void SendAudioChunk(float[] samples)
        {
#if MV_NATIVE_WEBSOCKETS
            if (_websocket == null || _websocket.State != WebSocketState.Open || _isShuttingDown) return;
            if (samples == null || samples.Length == 0) return;

            try
            {
                var pcmBytes = ConvertFloatsToPCM16Bytes(samples);
                if (pcmBytes.Length == 0) return;
                var base64Chunk = Convert.ToBase64String(pcmBytes);
                if (string.IsNullOrEmpty(base64Chunk)) return;

                var appendMsg = new { type = "input_audio_buffer.append", audio = base64Chunk };
                var json = JsonConvert.SerializeObject(appendMsg);
                _websocket.SendText(json);

                // Reset idle timer on activity
                _activityTimer = 0f;

                if (!_hasSentFirstAudioChunk) {
                    _hasSentFirstAudioChunk = true;
                    try { onMicStarted?.Invoke(); Log("onMicStarted event invoked (first audio chunk sent)."); }
                    catch (Exception e) { LogError($"Error in onMicStarted event handler: {e.Message}"); }
                }
            }
            catch (Exception e) { LogError($"SendAudioChunk error: {e.Message}\n{e.StackTrace}"); }
#endif
        }

        private bool _hasSentFirstAudioChunk; // Flag for invoking onMicStarted correctly

        #endregion

        #region AI Response Handling (WebSocket Messages)

        /// <summary>
        /// Handles incoming raw WebSocket message data (from WebSocket thread). 
        /// Resets idle timer and enqueues `ProcessWebSocketMessage`.
        /// </summary>
        private void HandleWebSocketMessage(byte[] data)
        {
            // Reset idle timer on inbound message
            _activityTimer = 0f;

            if (data == null || data.Length == 0) return;
            try {
                var rawJson = Encoding.UTF8.GetString(data);
                _mainThreadActions.Enqueue(() => ProcessWebSocketMessage(rawJson));
            } catch (Exception e) { LogError($"Error decoding WebSocket message bytes: {e.Message}"); }
        }

        /// <summary>
        /// Processes a received WebSocket message JSON string (runs on main thread).
        /// </summary>
        private void ProcessWebSocketMessage(string rawJson)
        {
            if (_isShuttingDown || string.IsNullOrEmpty(rawJson)) return;
            if (logs) Log($"Received: {rawJson}");

            try
            {
                var responseJson = JObject.Parse(rawJson);
                var msgType = responseJson["type"]?.ToString();
                if (string.IsNullOrEmpty(msgType)) { LogWarning("Received message without a 'type' field."); return; }

                switch (msgType)
                {
                    case "session.created": Log($"{msgType} event. Session ID: {responseJson["session"]!["id"]?.ToString() ?? "N/A"}"); break;
                    case "session.updated": Log($"{msgType} event. Session ID: {responseJson["session"]!["id"]?.ToString() ?? "N/A"}"); break;
                    case "response.audio.delta": HandleAudioDelta(responseJson); break;
                    case "response.audio_transcript.delta": HandleAudioTranscriptDelta(responseJson); break; 
                    case "response.text.delta": HandleAudioTranscriptDelta(responseJson); break; 
                    case "response.created": HandleResponseCreated(); break;
                    case "response.done": HandleResponseDone(responseJson); break;
                    case "conversation.item.created": Log($"'{msgType}' confirmed for item ID: {responseJson["item"]?["item_id"]}"); break;
                    case "conversation.item.input_audio_transcription.completed": HandleTranscriptCompleted(responseJson); break; // <<< MODIFICATION: Added handler
                    case "function_call.created": Log($"'{msgType}' detected for call_id: {responseJson["call_id"]}. Waiting for response.done."); break;
                    case "response.audio_transcript.done": HandleAudioTranscriptDone(responseJson); break;
                    case "response.text.done": HandleAudioTranscriptDone(responseJson); break;
                    case "response.function_call_arguments.delta": HandleFunctionArgsDelta(responseJson); break;
                    case "response.function_call_arguments.done": HandleFunctionArgsDone(responseJson); break;
                    case "error": HandleServerError(responseJson); break;
                    default: LogWarning($"Unhandled message type received: {msgType}"); break;
                }
            }
            catch (JsonException jsonEx) { LogError($"JSON parsing error: {jsonEx.Message}\nRaw JSON: {rawJson}"); }
            catch (Exception ex) { LogError($"Error processing WebSocket message: {ex.Message}\nStack Trace: {ex.StackTrace}\nRaw JSON: {rawJson}"); }
        }

        /// <summary> Handles the 'response.created' message. Runs on Main Thread. </summary>
        private void HandleResponseCreated()
        {
            if (_isShuttingDown) return;
            Log("response.created received. AI starting response.");
            _responsesInProgress++;
            Log($"Responses in progress: {_responsesInProgress}");

            if (_responsesInProgress == 1 && !_isAiSpeaking) {
                _isAiSpeaking = true;
                _currentTranscriptText = string.Empty;
                _functionCallArgsBuffer.Clear(); // Clear function arg buffer too
                Log("Marked AI as speaking. Stopping microphone.");
                StopMic();
                try { onAIResponseStarted?.Invoke(); Log("onAIResponseStarted event invoked."); }
                catch (Exception e) { LogError($"Error in onAIResponseStarted handler: {e.Message}"); }
            }
            else if (_responsesInProgress > 1) { LogWarning("response.created received while another response was already in progress. Nested responses?"); }
        }

        /// <summary> Accumulates partial function call arguments. </summary>
        private readonly Dictionary<string, StringBuilder> _functionCallArgsBuffer = new Dictionary<string, StringBuilder>();

        /// <summary> Handles the 'response.function_call_arguments.delta' message. Runs on Main Thread. </summary>
        private void HandleFunctionArgsDelta(JObject jObj) {
            var callId = jObj["call_id"]?.ToString();
            var delta = jObj["delta"]?.ToString();
            if(string.IsNullOrEmpty(callId) || delta == null) return;

            if(!_functionCallArgsBuffer.TryGetValue(callId, out var buffer)) {
                buffer = new StringBuilder();
                _functionCallArgsBuffer[callId] = buffer;
            }
            buffer.Append(delta);
        }

        /// <summary> Handles the 'response.function_call_arguments.done' message. Runs on Main Thread. </summary>
        private void HandleFunctionArgsDone(JObject jObj) {
            var callId = jObj["call_id"]?.ToString();
            var finalArgs = jObj["arguments"]?.ToString();
            if(string.IsNullOrEmpty(callId)) return;

            if (_functionCallArgsBuffer.TryGetValue(callId, out var bufferedArgs)) {
                var bufferedResult = bufferedArgs.ToString();
                if(logs && bufferedResult != finalArgs) {
                    LogWarning($"Function call {callId} final args ('{finalArgs}') differ from buffered delta ('{bufferedResult}'). Using final args.");
                }
                _functionCallArgsBuffer.Remove(callId);
            } 
            else if(string.IsNullOrEmpty(finalArgs)) {
                LogWarning($"Function call arguments done for {callId}, but no arguments provided or buffered.");
                finalArgs = "{}";
            }
            Log($"Function call arguments complete for {callId}. Final Args: {finalArgs}");
        }

        /// <summary> Handles the 'response.done' message. Runs on Main Thread. </summary>
        private void HandleResponseDone(JObject responseJson)
        {
            if (_isShuttingDown) return;
            Log("response.done received. AI finished response turn.");

            _responsesInProgress--;
            if (_responsesInProgress < 0) { LogWarning("response.done received but no response was marked as in progress. Resetting count to 0."); _responsesInProgress = 0; }
            Log($"Responses in progress: {_responsesInProgress}");

            ProcessFunctionCallsFromResponse(responseJson);

            var finalTranscript = _currentTranscriptText;
            // Fallback: try to get text directly from the response.done if delta accumulation was empty
            if (string.IsNullOrEmpty(finalTranscript)) {
                finalTranscript = responseJson["response"]?["output"]?.FirstOrDefault(o => o["type"]?.ToString() == "message")?["content"]?.FirstOrDefault(c => c["type"]?.ToString() == "text")?["text"]?.ToString();
            }

            Log($"Final transcript for this turn: \"{finalTranscript ?? ""}\"");
            try { onAIResponseString?.Invoke(finalTranscript ?? string.Empty); Log("onAIResponseString event invoked."); }
            catch (Exception e) { LogError($"Error in onAIResponseString handler: {e.Message}"); }
            _currentTranscriptText = string.Empty; 

            if (_responsesInProgress == 0 && !_pendingVision) {
                Log("All responses complete and no pending vision. Finishing speaking state.");
                _isWaitingToFinishSpeaking = true;
                _finishedTranscript = finalTranscript; 
                Log("Waiting for audio buffer to empty before potentially restarting mic.");
                try { onAIResponseFinished?.Invoke(); Log("onAIResponseFinished event invoked."); }
                catch (Exception e) { LogError($"Error in onAIResponseFinished handler: {e.Message}"); }
            }
            else { Log($"Not finishing speaking state yet. Responses in progress: {_responsesInProgress}, Pending Vision: {_pendingVision}"); }
        }
        
        /// <summary> Handles the 'conversation.item.input_audio_transcription.completed' message. Runs on Main Thread. Checks for completed transcription and triggers response if coherent. </summary>
        private void HandleTranscriptCompleted(JObject jObj)
        {
            if (_isShuttingDown) return;

            // Check if this update contains the completed input audio transcription
            var transcript = jObj["transcript"]?.ToString();

            if (transcript != null) // We got a completed transcript string
            {
                Log($"Received completed input transcript: \"{transcript}\"");

                // Check for coherence: not null/empty, and not empty after trimming and removing newlines
                var cleanedTranscript = transcript.Trim().Replace("\n", "").Replace("\r", "");
                if (!string.IsNullOrEmpty(cleanedTranscript))
                {
                    const string allowedSinglePhrases = "Yes.,No.,Okay.";
                    var wordCount = cleanedTranscript.Split(' ').Length;
                    if (wordCount == 1 && !allowedSinglePhrases.Split(',').Any(phrase => cleanedTranscript.Contains(phrase)))
                    {
                        Log("Transcript contains incoherent phrases. Not triggering AI response.");
                        // Optionally restart mic if it was stopped and conditions allow
                        if (micActive && CanStartMic()) {
                            Log("Attempting to restart microphone after incoherent input.");
                            StartMic();
                        }
                        return;
                    }
                    
                    Log("Transcript is coherent. Triggering AI response.");
                    TriggerResponseInternal(true); // Force trigger based on coherent input
                }
                else
                {
                    Log("Transcript is empty or incoherent after cleaning. Not triggering response.");
                    // Optionally restart mic if it was stopped and conditions allow
                    if (micActive && CanStartMic()) {
                        Log("Attempting to restart microphone after incoherent input.");
                        StartMic();
                    }
                }
            }
            else
            {
                // Log other conversation item updates if needed for debugging
                 Log($"'conversation.item.updated' for item ID: {jObj["item_id"]}");
            }
        }


        /// <summary> Called from FixedUpdate to check if the AI has finished speaking (audio buffer empty) and handle subsequent actions. </summary>
        private void UpdateSpeakingFinishedState()
        {
            if (!_isWaitingToFinishSpeaking || _isShuttingDown) return;

            if (_streamBuffer.IsEmpty)
            {
                Log("Audio buffer is empty. AI has finished speaking.");
                _isWaitingToFinishSpeaking = false;
                _isAiSpeaking = false;

                var transcript = _finishedTranscript ?? string.Empty;
                _finishedTranscript = null;
                var shouldRestartMic = micActive;

                if (!string.IsNullOrEmpty(transcript))
                {
                    if (transcript.TrimEnd().EndsWith("?") || transcript.EndsWith("?;"))
                    {
                        Log("Transcript ends with '?'.");
                        if (enableMicOnQuestion) { Log("Enabling mic."); if (!micActive) MicrophoneActive = true; shouldRestartMic = true; }
                        else { Log("enableMicOnQuestion is false."); shouldRestartMic = micActive; }
                    }
                    else if (transcript.TrimEnd().EndsWith(";") || transcript.Contains(".;"))
                    {
                        Log("Transcript ends with ';'. Signaling communication finished.");
                        if (disableMicOnCommunicationFinished) { Log("Disabling mic."); if (micActive) MicrophoneActive = false; shouldRestartMic = false; }
                        else { Log("disableMicOnCommunicationFinished is false."); shouldRestartMic = micActive; }
                        try { onCommunicationFinished?.Invoke(); Log("onCommunicationFinished event invoked."); }
                        catch (Exception e) { LogError($"Error in onCommunicationFinished handler: {e.Message}"); }
                    }
                }

                // Only restart mic if conditions allow (including user preference `micActive`)
                if (shouldRestartMic && CanStartMic()) { Log("Attempting to start microphone."); StartMic(); }
                else { Log($"Microphone remains inactive (ShouldStart: {shouldRestartMic}, CanStart: {CanStartMic()})."); if (!micActive && _isMicRunning) { StopMic(); } }
            }
            else
            {
                _activityTimer = 0;
            }
        }

        /// <summary> Handles 'response.audio.delta' message. Runs on Main Thread. </summary>
        private void HandleAudioDelta(JObject jObj)
        {
            if (_isShuttingDown) return;
            var base64Data = jObj["delta"]?.ToString();
            if (string.IsNullOrEmpty(base64Data)) return;
            try {
                var pcmBytes = Convert.FromBase64String(base64Data);
                if (pcmBytes.Length == 0) return;
                var samples = Convert16BitPCMToFloats(pcmBytes);
                if (samples.Length == 0) return;
                var finalSamples = Resample(samples, outputSampleRate, _systemSampleRate);
                if (finalSamples.Length == 0) return;
                foreach (var s in finalSamples) { _streamBuffer.Enqueue(s); }
            } catch (FormatException formatEx) { LogError($"Base64 decoding error for audio delta: {formatEx.Message}"); }
            catch (Exception e) { LogError($"Error handling audio delta: {e.Message}\n{e.StackTrace}"); }
        }

        /// <summary> Handles 'response.audio_transcript.delta' or 'response.text.delta'. Runs on Main Thread. </summary>
        private void HandleAudioTranscriptDelta(JObject jObj)
        {
            if (_isShuttingDown) return;
            var delta = jObj["delta"]?.ToString();
            if (delta == null) return; 
            _currentTranscriptText += delta;
        }

        /// <summary> Handles 'response.audio_transcript.done' or 'response.text.done'. Runs on Main Thread. </summary>
        private void HandleAudioTranscriptDone(JObject jObj)
        {
            if (_isShuttingDown) return;
            var finalValue = jObj["transcript"]?.ToString() ?? jObj["text"]?.ToString();
            if (finalValue != null)
            {
                if(logs && _currentTranscriptText != finalValue)
                {
                    LogWarning($"Final transcript/text ('{finalValue}') differs from buffered delta ('{_currentTranscriptText}'). Using final value.");
                }
                _currentTranscriptText = finalValue;
            }
            else
            {
                LogWarning($"Received transcript/text done event, but no final value found. Using buffered value: '{_currentTranscriptText}'");
            }
        }

        /// <summary> Handles the 'error' message from the server. Runs on Main Thread. </summary>
        private void HandleServerError(JObject responseJson)
        {
            if (_isShuttingDown) return;
            var errorCode = responseJson["error"]?["code"]?.ToString();
            var errorMessage = responseJson["error"]?["message"]?.ToString();
            LogError($"Server Error Received! Code: {errorCode ?? "N/A"}, Message: {errorMessage ?? "N/A"}");

            if (errorCode is "token_expired" or "invalid_token") { LogError("Token expired/invalid. Reconnecting."); DisconnectInternal(); StartReconnectionAttempt(); }
            else if (errorCode == "session_not_found") { LogError("Session not found. Reconnecting."); DisconnectInternal(); StartReconnectionAttempt(); }
            else if (errorCode == "rate_limit_exceeded") { LogWarning("Rate limit exceeded. Reconnecting with delay."); DisconnectInternal(); StartReconnectionAttempt(); }
            else { LogError("Unhandled server error. Disconnecting."); DisconnectInternal(); if (!_needsReconnect && !_isAcquiringToken) { StartReconnectionAttempt(); } }
        }

        #endregion

        #region Function Call Handling

        /// <summary> Parses 'response.done' message for function calls and triggers them. Runs on Main Thread. </summary>
        private void ProcessFunctionCallsFromResponse(JObject responseJson)
        {
            var responseObj = responseJson["response"];
            if (responseObj?["output"] is not JArray outputArray) return;

            foreach (var item in outputArray)
            {
                if (item is not JObject itemObj) continue;
                var itemType = itemObj["type"]?.ToString();
                if (itemType != "function_call") continue;

                var functionName = itemObj["name"]?.ToString();
                var callId = itemObj["call_id"]?.ToString();
                string argumentsJson;

                if (!string.IsNullOrEmpty(callId) && _functionCallArgsBuffer.TryGetValue(callId, out var bufferedArgs))
                {
                    argumentsJson = bufferedArgs.ToString();
                    _functionCallArgsBuffer.Remove(callId);
                }
                else
                {
                    argumentsJson = itemObj["arguments"]?.ToString();
                    if(!string.IsNullOrEmpty(callId)) _functionCallArgsBuffer.Remove(callId);
                }

                if (string.IsNullOrEmpty(functionName))
                {
                    LogWarning("Found function_call with missing name.");
                    continue;
                }

                Log($"Processing function_call: Name='{functionName}', CallID='{callId ?? "N/A"}', Args='{argumentsJson ?? "None"}'");

                if (functionName == "vision_request") { HandleVisionFunctionCall(argumentsJson); }
                else { TriggerUserFunctionCall(functionName, argumentsJson); }
            }

            if(_functionCallArgsBuffer.Count > 0)
            {
                LogWarning($"Clearing {_functionCallArgsBuffer.Count} stale function call argument buffers.");
                _functionCallArgsBuffer.Clear();
            }
        }

        /// <summary> Handles 'vision_request' function call. Runs on Main Thread. </summary>
        private void HandleVisionFunctionCall(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson)) { LogWarning("Vision request called with empty arguments."); ProcessVisionResponseFailed("Missing arguments for vision request."); return; }
            try {
                var visionArgs = JObject.Parse(argumentsJson);
                var visionPrompt = visionArgs["vision_request"]?.ToString();
                if (string.IsNullOrWhiteSpace(visionPrompt)) { LogWarning("Vision request missing 'vision_request' property."); ProcessVisionResponseFailed("Missing 'vision_request' parameter."); return; }

                Log($"Vision requested with prompt: \"{visionPrompt}\"");
                _pendingVision = true; Log("Pending vision set to true. Stopping microphone."); StopMic();
                var handler = VisionHandler;
                if (handler != null) { handler.SubmitGameScreenshot(visionPrompt); }
                else { LogError("VisionHandler is null."); HandleVisionResponseFailedInternal("Vision system not available."); }
            } catch (JsonException jsonEx) { LogError($"Failed to parse vision args: {jsonEx.Message}\nJSON: {argumentsJson}"); HandleVisionResponseFailedInternal("Invalid vision arguments format."); }
            catch (Exception e) { LogError($"Error processing vision call: {e.Message}"); HandleVisionResponseFailedInternal("Internal vision error."); }
        }

        /// <summary> Finds and triggers a user-defined function. Runs on Main Thread. </summary>
        private void TriggerUserFunctionCall(string functionID, string argumentsJson)
        {
            var functionDefinition = availableFunctions.FirstOrDefault(f => f.functionID == functionID);
            if (functionDefinition == null) { LogWarning($"AI called undefined function: '{functionID}'."); return; }

            Log($"Invoking user function: '{functionID}'");
            try { functionDefinition.onCalled?.Invoke(); } catch (Exception e) { LogError($"Error invoking onCalled for '{functionID}': {e.Message}"); }

            if (!string.IsNullOrEmpty(argumentsJson) && functionDefinition.parameters.Count > 0)
            {
                try
                {
                    var arguments = JObject.Parse(argumentsJson);
                    foreach (var paramDef in functionDefinition.parameters)
                    {
                        if (arguments.TryGetValue(paramDef.parameterID, StringComparison.OrdinalIgnoreCase, out var paramValueToken))
                        {
                            ParseAndInvokeParameter(paramDef, paramValueToken);
                        }
                        else
                        {
                            Log($"Argument '{paramDef.parameterID}' not provided for '{functionID}'. Skipping.");
                        }
                    }
                }
                catch (JsonException jsonEx) { LogError($"Failed to parse args JSON for '{functionID}': {jsonEx.Message}\nJSON: {argumentsJson}"); }
                catch (Exception e) { LogError($"Error processing args for '{functionID}': {e.Message}"); }
            }
            else if (!string.IsNullOrEmpty(argumentsJson))
            {
                LogWarning($"Function '{functionID}' received args '{argumentsJson}' but defines no parameters.");
            }
            else if (functionDefinition.parameters.Count > 0)
            {
                Log($"Function '{functionID}' defines parameters but received no arguments.");
            }
        }

        /// <summary> Parses a JToken value and invokes the corresponding parameter event. Runs on Main Thread. </summary>
        private void ParseAndInvokeParameter(AIRealtimeCommunicationFunctionParameter paramDef, JToken valueToken)
        {
            if (valueToken == null || valueToken.Type == JTokenType.Null)
            {
                LogWarning($"Null value for parameter '{paramDef.parameterID}'.");
                return;
            }
            var valueString = valueToken.ToString(); 
            Log($"Parsing parameter '{paramDef.parameterID}', Type: {paramDef.type}, Raw Value: '{valueString}'");
            try
            {
                switch (paramDef.type)
                {
                    case AIRealtimeCommunicationFunctionParameterType.String:
                        paramDef.onStringValue?.Invoke(valueString);
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Float:
                        if (float.TryParse(valueString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fVal))
                            paramDef.onFloatValue?.Invoke(fVal);
                        else
                            LogWarning($"Failed parse float: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Integer:
                        if (int.TryParse(valueString, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var iVal))
                            paramDef.onIntValue?.Invoke(iVal);
                        else
                            LogWarning($"Failed parse int: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Boolean:
                        bool bVal;
                        if (valueToken.Type == JTokenType.Boolean)
                        {
                            bVal = valueToken.Value<bool>();
                        }
                        else if (!bool.TryParse(valueString, out bVal))
                        {
                            if (valueString == "1") bVal = true;
                            else if (valueString == "0") bVal = false;
                            else { LogWarning($"Failed parse bool: '{valueString}'"); return; }
                        }
                        paramDef.onBoolValue?.Invoke(bVal);
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Vector2:
                        if (TryParseVector2(valueString, out var v2)) paramDef.onVector2Value?.Invoke(v2);
                        else LogWarning($"Failed parse Vector2: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Vector3:
                        if (TryParseVector3(valueString, out var v3)) paramDef.onVector3Value?.Invoke(v3);
                        else LogWarning($"Failed parse Vector3: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Vector4:
                        if (TryParseVector4(valueString, out var v4)) paramDef.onVector4Value?.Invoke(v4);
                        else LogWarning($"Failed parse Vector4: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Quaternion:
                        if (TryParseVector4(valueString, out var q)) paramDef.onQuaternionValue?.Invoke(new Quaternion(q.x, q.y, q.z, q.w));
                        else LogWarning($"Failed parse Quaternion: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Color:
                        var hc = valueString.StartsWith("#") ? valueString : "#" + valueString;
                        if (ColorUtility.TryParseHtmlString(hc, out var c)) paramDef.onColorValue?.Invoke(c);
                        else LogWarning($"Failed parse Color: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Color32:
                        var hc32 = valueString.StartsWith("#") ? valueString : "#" + valueString;
                        if (ColorUtility.TryParseHtmlString(hc32, out var c32)) paramDef.onColor32Value?.Invoke(c32);
                        else LogWarning($"Failed parse Color32: '{valueString}'");
                        break;
                    case AIRealtimeCommunicationFunctionParameterType.Enum:
                        var idx = paramDef.enumValues.FindIndex(e => string.Equals(e, valueString, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            paramDef.onEnumValue?.Invoke(idx);
                            paramDef.onEnumValueString?.Invoke(paramDef.enumValues[idx]);
                        }
                        else if (int.TryParse(valueString, out var iEnum) && iEnum >= 0 && iEnum < paramDef.enumValues.Count)
                        {
                            paramDef.onEnumValue?.Invoke(iEnum);
                            paramDef.onEnumValueString?.Invoke(paramDef.enumValues[iEnum]);
                        }
                        else
                        {
                            LogWarning($"Failed parse Enum: '{valueString}'");
                        }
                        break;
                    default:
                        LogWarning($"Unhandled param type: '{paramDef.type}'");
                        break;
                }
            }
            catch (Exception e)
            {
                LogError($"Error invoking param event for '{paramDef.parameterID}': {e.Message}\n{e.StackTrace}");
            }
        }

        // --- Argument Parsing Helpers ---
        private static bool TryParseVector2(string v, out Vector2 r)
        {
            r=Vector2.zero;
            if(string.IsNullOrWhiteSpace(v)) return false;
            var p=v.Trim('(',')',' ').Split(',');
            return p.Length==2 &&
                   float.TryParse(p[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.x) &&
                   float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.y);
        }
        private static bool TryParseVector3(string v, out Vector3 r)
        {
            r=Vector3.zero;
            if(string.IsNullOrWhiteSpace(v)) return false;
            var p=v.Trim('(',')',' ').Split(',');
            return p.Length==3 &&
                   float.TryParse(p[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.x) &&
                   float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.y) &&
                   float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.z);
        }
        private static bool TryParseVector4(string v, out Vector4 r)
        {
            r=Vector4.zero;
            if(string.IsNullOrWhiteSpace(v)) return false;
            var p=v.Trim('(',')',' ').Split(',');
            return p.Length==4 &&
                   float.TryParse(p[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.x) &&
                   float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.y) &&
                   float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.z) &&
                   float.TryParse(p[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out r.w);
        }

        #endregion

        #region Audio Playback and Utility

        /// <summary> Unity callback on audio thread. Feeds buffered data to output. </summary>
        private void OnAudioFilterRead(float[] data, int channels) {
            for (var i = 0; i < data.Length; i += channels)
            {
                if (_streamBuffer.TryDequeue(out var sample))
                {
                    for (var j = 0; j < channels; ++j) data[i + j] = sample;
                }
                else
                {
                    for (var j = 0; j < channels; ++j) data[i + j] = 0f;
                }
            }
        }

        /// <summary> Converts 16-bit PCM bytes to float samples. </summary>
        private float[] Convert16BitPCMToFloats(byte[] pcmData)
        {
            if (pcmData == null || pcmData.Length < 2) return Array.Empty<float>();
            var samples = new float[pcmData.Length / 2];
            for (int i = 0, pcmIndex = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(pcmData[pcmIndex++] | (pcmData[pcmIndex++] << 8)) / 32768f;
            }
            return samples;
        }

        /// <summary> Simple linear interpolation resampling. </summary>
        private float[] Resample(float[] input, int inputRate, int outputRate)
        {
            if (input == null || input.Length == 0 || inputRate <= 0 || outputRate <= 0 || inputRate == outputRate)
                return input ?? Array.Empty<float>();

            var ratio = (double)inputRate / outputRate;
            var outputLength = (int)Math.Ceiling(input.Length / ratio);
            if (outputLength <= 0) return Array.Empty<float>();

            var output = new float[outputLength];
            for (var i = 0; i < outputLength; i++)
            {
                var idxD = i * ratio;
                var idx0 = (int)Math.Floor(idxD);
                var idx1 = Math.Min(idx0 + 1, input.Length - 1);
                idx0 = Math.Max(0, idx0);
                output[i] = (idx0 == idx1)
                    ? input[idx0]
                    : (float)(input[idx0] * (1.0 - (idxD - idx0)) + input[idx1] * (idxD - idx0));
            }
            return output;
        }

        #endregion

        #region Vision Handling (Callbacks from VisionHandler AIAgent)

        /// <summary> Callback when VisionHandler starts thinking. Queues OnVisionRequested invocation. </summary>
        private void HandleVisionThinkingStarted()
        {
            _mainThreadActions.Enqueue(() =>
            {
                Log("Vision processing started.");
                try { onVisionRequested?.Invoke(); }
                catch (Exception e) { LogError($"Error in onVisionRequested handler: {e.Message}"); }
            });
        }

        /// <summary> Callback when VisionHandler finishes thinking. Logs info. </summary>
        private void HandleVisionThinkingFinished()
        {
            _mainThreadActions.Enqueue(() => Log("Vision processing finished by VisionHandler."));
        }

        /// <summary> Callback for successful VisionHandler response. Queues processing. </summary>
        private void HandleVisionResponse(string visionResponse)
        {
            _mainThreadActions.Enqueue(() => ProcessVisionResponse(visionResponse));
        }

        /// <summary> Callback for failed VisionHandler response. Queues processing. </summary>
        private void HandleVisionResponseFailed()
        {
            _mainThreadActions.Enqueue(() => ProcessVisionResponseFailed("Vision agent failed to generate a response."));
        }

        /// <summary> Processes successful vision response. Sends result back to main AI. Runs on Main Thread. </summary>
        private void ProcessVisionResponse(string visionResponse)
        {
            if (_isShuttingDown || !_pendingVision)
            {
                if(!_pendingVision) LogWarning("Received vision response, but no request pending.");
                return;
            }
            Log($"Vision response received: \"{visionResponse}\"");
            if (string.IsNullOrWhiteSpace(visionResponse))
            {
                LogWarning("Vision response empty.");
                ProcessVisionResponseFailed("Received empty response.");
                return;
            }

#if MV_NATIVE_WEBSOCKETS
            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                LogError("WebSocket not open. Cannot send vision response.");
                _pendingVision = false;
                InvokeVisionFinishedEvent();
                if (CanStartMic()) StartMic();
                return;
            }
            Log("Sending vision response back to main AI...");
            var visionMsg = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "system",
                    content = new[]
                    {
                        new { type = "input_text", text = $"[Vision System Response]: {visionResponse}" }
                    }
                }
            };
            try
            {
                var json = JsonConvert.SerializeObject(visionMsg);
                _websocket.SendText(json);

                // Reset idle timer on activity
                _activityTimer = 0f;

                Log("Vision response sent.");
                TriggerResponseInternal(true); // Force trigger after sending system message
            }
            catch (Exception e)
            {
                LogError($"Failed to send vision response: {e.Message}");
                ProcessVisionResponseFailed("Failed to send vision result.");
                return;
            }
#else
            LogError("Cannot send vision response: MV_NATIVE_WEBSOCKETS not defined.");
            ProcessVisionResponseFailed("Cannot send vision result (missing dependency).");
            return;
#endif
            _pendingVision = false;
            InvokeVisionFinishedEvent();
            Log("Vision handling complete.");
        }

        /// <summary> Processes failed vision response. Sends error message back to main AI. Runs on Main Thread. </summary>
        private void ProcessVisionResponseFailed(string failureReason)
        {
            if (_isShuttingDown || !_pendingVision)
            {
                if(!_pendingVision) LogWarning("Received vision failure, but no request pending.");
                return;
            }
            LogError($"Vision processing failed: {failureReason}");

#if MV_NATIVE_WEBSOCKETS
            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                LogError("WebSocket not open. Cannot send vision failure message.");
                _pendingVision = false;
                InvokeVisionFinishedEvent();
                if (CanStartMic()) StartMic();
                return;
            }
            Log("Sending vision failure message back to main AI...");
            var failureMsg = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "system",
                    content = new[]
                    {
                        new { type = "input_text", text = "[Vision System Error]: I'm sorry, I couldn't process the visual information at this time." }
                    }
                }
            };
            try
            {
                var json = JsonConvert.SerializeObject(failureMsg);
                _websocket.SendText(json);

                // Reset idle timer on activity
                _activityTimer = 0f;

                Log("Vision failure message sent.");
                TriggerResponseInternal(true); // Force trigger after sending system message
            }
            catch (Exception e)
            {
                LogError($"Failed to send vision failure message: {e.Message}");
            }
#else
            LogError("Cannot send vision failure message: MV_NATIVE_WEBSOCKETS not defined.");
#endif
            _pendingVision = false;
            InvokeVisionFinishedEvent();
            Log("Vision failure handling complete.");
        }

        /// <summary> Checks if conditions allow starting the microphone. </summary>
        private bool CanStartMic()
        {
            var socketOpen = false;
#if MV_NATIVE_WEBSOCKETS
            socketOpen = _websocket is { State: WebSocketState.Open };
#endif
            return micActive && 
                   !_isMicRunning && 
                   !_isAiSpeaking && 
                   !_pendingVision && 
                   !_isWaitingToFinishSpeaking && 
                   !_isAcquiringToken && 
                   socketOpen && 
                   !_isShuttingDown;
        }

        /// <summary> Helper method to invoke the onVisionFinished event safely. </summary>
        private void InvokeVisionFinishedEvent()
        {
            try
            {
                onVisionFinished?.Invoke();
                Log("onVisionFinished event invoked.");
            }
            catch (Exception e)
            {
                LogError($"Error in onVisionFinished handler: {e.Message}");
            }
        }

        /// <summary> Internal callback for VisionHandler failure. Queues `ProcessVisionResponseFailed`. </summary>
        private void HandleVisionResponseFailedInternal(string reason = "Vision agent failed to generate a response.")
        {
            ProcessVisionResponseFailed(reason);
        }

        #endregion

        #region Public API Methods

        /// <summary> Public method to start the connection process. Handles checks and queues the action. </summary>
        public void Connect()
        {
            if (_isShuttingDown)
                return;
            _connectCalled = true;
            if (!isActiveAndEnabled) { LogWarning("Connect() called, but component is not active/enabled."); return; }
            if (!_isStarted) { LogWarning("Connect() called before Start(). Will attempt in Start()."); return; }
            if (_isShuttingDown) { LogWarning("Connect() called while shutting down."); return; }
            if (_isAcquiringToken) { Log("Connect() called, but acquiring token."); return; }

            var isConnectingOrOpen = false;
#if MV_NATIVE_WEBSOCKETS
            isConnectingOrOpen = _websocket != null &&
                                 (_websocket.State == WebSocketState.Open || _websocket.State == WebSocketState.Connecting);
#endif
            if (isConnectingOrOpen) { Log("Connect() called, but already connected/connecting."); return; }
            if (_needsReconnect) { Log("Connect() called while reconnect pending."); return; }

            Log("Public Connect() called. Enqueuing connection logic.");
            _mainThreadActions.Enqueue(() =>
            {
                if (_isShuttingDown)
                    return;
                Log("Processing enqueued Connect() action.");
                DisconnectInternal(); // Clean up first
                _systemSampleRate = AudioSettings.outputSampleRate;
                Log($"System audio rate: {_systemSampleRate}Hz.");
                if (outputVoiceSource)
                {
                    if (outputVoiceSource.isPlaying) outputVoiceSource.Stop();
                    if (outputVoiceSource.clip != null && outputVoiceSource.clip.name == "StreamingClip")
                    {
                        Destroy(outputVoiceSource.clip);
                    }
                    // Ensure the AudioClip uses the system sample rate for playback via OnAudioFilterRead
                    outputVoiceSource.clip = AudioClip.Create("StreamingClip", _systemSampleRate * 2, 1, _systemSampleRate, true); // Longer buffer just in case
                    outputVoiceSource.loop = true;
                    outputVoiceSource.Play();
                    Log("Output AudioSource prepared.");
                }
                else
                {
                    LogError("Cannot prepare audio output: outputVoiceSource is null.");
                }
                InitiateConnection();
            });
        }

        /// <summary> Dummy callback for AudioClip.Create. </summary>
        private void OnAudioPCMRead(float[] data)
        {
            // Required by Unity, logic is in OnAudioFilterRead
        }

        /// <summary> Public method to disconnect. Sets shutdown flag and queues internal disconnect. </summary>
        public void Disconnect()
        {
            var wasShuttingDown = _isShuttingDown;
            _isShuttingDown = true; // Prevent reconnects after manual disconnect
            if (wasShuttingDown) { Log("Disconnect() called while already shutting down/disconnecting."); }
            Log("Public Disconnect() method called. Enqueuing disconnection logic.");
            _mainThreadActions.Enqueue(DisconnectInternal);
        }

        /// <summary> Public method to set microphone active state. </summary>
        public void SetMicrophoneActive(bool active) { MicrophoneActive = active; }

        /// <summary> Public method to set microphone inactive state. </summary>
        public void SetMicrophoneInactive(bool inactive) { MicrophoneActive = !inactive; }

        /// <summary>
        /// Public method to manually trigger an AI response. Use with caution, as it bypasses
        /// the automatic triggering based on coherent transcription.
        /// </summary>
        public void TriggerResponse()
        {
            _mainThreadActions.Enqueue(() => TriggerResponseInternal(false));
        }

        /// <summary> Sends text input and immediately requests a response. </summary>
        public void SendTextWithResponse(string text)
        {
            if (string.IsNullOrEmpty(text)) { LogWarning("SendTextWithResponse called with empty text."); return; }
            if (_isShuttingDown) return;
            _mainThreadActions.Enqueue(() =>
            {
                var busy = IsProcessing && !_isWaitingToFinishSpeaking;
                if (busy) { LogWarning("SendTextWithResponse called while processing."); return; }
#if MV_NATIVE_WEBSOCKETS
                if (_websocket == null || _websocket.State != WebSocketState.Open)
                {
                    LogWarning("SendTextWithResponse: WebSocket not open.");
                    return;
                }
                Log($"Sending text & requesting response: \"{text}\"");
                var textMsg = new
                {
                    type = "conversation.item.create",
                    item = new
                    {
                        type = "message",
                        role = "user",
                        content = new[]
                        {
                            new { type = "input_text", text }
                        }
                    }
                };
                try
                {
                    var json = JsonConvert.SerializeObject(textMsg);
                    _websocket.SendText(json);

                    // Reset idle timer on activity
                    _activityTimer = 0f;

                    Log("User text sent.");
                    TriggerResponseInternal(true); // Force trigger after explicit text input
                }
                catch (Exception e)
                {
                    LogError($"Failed to send text: {e.Message}");
                }
#else
                LogWarning("SendTextWithResponse: MV_NATIVE_WEBSOCKETS not defined.");
#endif
            });
        }

        /// <summary> Sends text input without immediately requesting a response. </summary>
        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) { LogWarning("SendText called with empty text."); return; }
            if (_isShuttingDown) return;
            _mainThreadActions.Enqueue(() =>
            {
                var busy = IsProcessing && !_isWaitingToFinishSpeaking;
                if (busy) { LogWarning("SendText called while processing."); return; }
#if MV_NATIVE_WEBSOCKETS
                if (_websocket == null || _websocket.State != WebSocketState.Open)
                {
                    LogWarning("SendText: WebSocket not open.");
                    return;
                }
                Log($"Sending text: \"{text}\"");
                var textMsg = new
                {
                    type = "conversation.item.create",
                    item = new
                    {
                        type = "message",
                        role = "user",
                        content = new[]
                        {
                            new { type = "input_text", text }
                        }
                    }
                };
                try
                {
                    var json = JsonConvert.SerializeObject(textMsg);
                    _websocket.SendText(json);

                    // Reset idle timer on activity
                    _activityTimer = 0f;

                    Log("User text sent.");
                    // NOTE: No TriggerResponseInternal call here, as requested by method name
                }
                catch (Exception e)
                {
                    LogError($"Failed to send text: {e.Message}");
                }
#else
                LogWarning("SendText: MV_NATIVE_WEBSOCKETS not defined.");
#endif
            });
        }

        /// <summary> Internal method to send 'response.create' message. Runs on Main Thread. </summary>
        private void TriggerResponseInternal(bool force)
        {
            if (_isShuttingDown) return;
            
            // Prevent triggering if AI is already speaking or waiting to finish, unless forced.
            // Allow triggering even if the mic is running (e.g., triggered by transcript complete event)
            var busy = (_isAiSpeaking || _isWaitingToFinishSpeaking) && !_pendingVision; // Allow trigger if pending vision finished
            if (!force && busy)
            {
                Log($"TriggerResponseInternal: Processing (AI Speaking: {_isAiSpeaking}, WaitingToFinish: {_isWaitingToFinishSpeaking}), request ignored.");
                return;
            }
            // Prevent triggering if a response is already actively being generated/streamed
            if (_responsesInProgress > 0 && !force) {
                 Log($"TriggerResponseInternal: Response already in progress ({_responsesInProgress}), request ignored.");
                 return;
            }


#if MV_NATIVE_WEBSOCKETS
            if (_websocket == null || _websocket.State != WebSocketState.Open)
            {
                LogWarning("TriggerResponseInternal: WebSocket not open.");
                return;
            }
            Log("Triggering AI response creation...");
            StopMic(); // Ensure mic is stopped before requesting response
            var responseMsg = new
            {
                type = "response.create",
                response = new
                {
                    modalities = new[] { "text", "audio" }
                }
            };
            try
            {
                var json = JsonConvert.SerializeObject(responseMsg);
                _websocket.SendText(json);

                // Reset idle timer on activity
                _activityTimer = 0f;

                Log("response.create sent.");
            }
            catch (Exception e)
            {
                LogError($"Failed to send response.create: {e.Message}");
            }
#else
            LogWarning("TriggerResponseInternal: MV_NATIVE_WEBSOCKETS not defined.");
#endif
        }

        #endregion

        #region Logging Helpers
        private void Log(string message) { if (logs) Debug.Log($"[AIRealtimeComms] {message}"); }
        private void LogWarning(string message) { if (logs) Debug.LogWarning($"[AIRealtimeComms] {message}"); }
        private void LogError(string message) { Debug.LogError($"[AIRealtimeComms] ERROR: {message}"); }
        #endregion
    }

} // End namespace

#endif // End #if !UNITY_WEBGL || UNITY_EDITOR
