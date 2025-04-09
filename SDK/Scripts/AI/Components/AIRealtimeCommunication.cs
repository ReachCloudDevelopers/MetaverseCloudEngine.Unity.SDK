#if !UNITY_WEBGL || UNITY_EDITOR

using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
    #region Function Definition Class

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

    [Serializable]
    public class AIRealtimeCommunicationFunctionParameter
    {
        [Required]
        public string parameterID = "";
        [Required]
        public string description = "";
        public AIRealtimeCommunicationFunctionParameterType type = AIRealtimeCommunicationFunctionParameterType.String;
        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Enum)]
        public List<string> enumValues = new();

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
        public UnityEvent<int> onEnumValue = new();
        [ShowIf(nameof(type), AIRealtimeCommunicationFunctionParameterType.Enum)]
        public UnityEvent<string> onEnumValueString = new();

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
    /// A class that represents a function that the AI can call.
    /// </summary>
    [Serializable]
    public class AIRealtimeCommunicationFunction
    {   
        [Tooltip("Identifier/name of the function as recognized by the AI.")]
        public string functionID;
        [TextArea]
        [Tooltip("Description to help the AI decide when to call this function.")]
        public string functionDescription;
        [Tooltip("The parameters that this function accepts. Each parameter has a type, description, and optional enum values.")]
        public List<AIRealtimeCommunicationFunctionParameter> parameters = new ();
        [Tooltip("Event invoked when the AI calls this functionID.")]
        public UnityEvent onCalled = new();
    }

    #endregion

    /// <summary>
    /// A Unity MonoBehaviour to stream audio from the microphone to OpenAI's GPT-4o Realtime API,
    /// including function-calling support.
    /// </summary>
    [HideMonoScript]
    public partial class AIRealtimeCommunication : TriInspectorMonoBehaviour
    {
        private const string BetaHeaderName = "OpenAI-Beta";
        private const string BetaHeaderValue = "realtime=v1";
        private const string RealtimeEndpoint = "wss://api.openai.com/v1/realtime";

        [Tooltip("If the AI should automatically connect on start/enable.")]
        [SerializeField] private bool connectOnStart = true;

        // Microphone Streaming
        [Header("Microphone")]
        [Range(8000, 48000)]
        [Tooltip("The approximate sample rate (in Hz) of the user's microphone.")]
        [SerializeField]
        private int micSampleRate = 16000;
        [Tooltip("How frequently the user's microphone is sampled and sent to the AI for processing.")]
        [SerializeField]
        private float sampleInterval = 0.2f;
        [Tooltip("Enables or disables the microphone of the user from sending audio.")]
        [SerializeField]
        [DisableInPlayMode]
        private bool micActive = true; // The user's setting in the Inspector.
        [Tooltip("Automatically re-enables the microphone if the AI asks a question. This is useful if you have a " +
                 "custom microphone lifecycle and want to ensure the mic is re-enabled after a response.")]
        [SerializeField]
        private bool enableMicOnQuestion = true;
        [Tooltip("Automatically disables the microphone when the AI is done speaking to the user. This is useful if you have a " +
                 "custom microphone lifecycle and want to ensure the mic is disabled after a response.")]
        [SerializeField]
        private bool disableMicOnCommunicationFinished = true;
        
        // Output (GPT Response)
        [Header("Output (GPT Response)")]
        [TextArea(5, 10)] 
        [SerializeField] private string prompt;
        [SerializeField] private AudioSource outputVoiceSource;
        [Tooltip("The voice to use for the AI's audio output.")]
        [SerializeField] private TextToSpeechVoicePreset outputVoice = TextToSpeechVoicePreset.Male;
        [Range(8000, 48000)]
        [Tooltip("The sample rate (in Hz) of the GPT output audio. Adjust for speed/pitch of the AI's voice.")]
        [SerializeField] private int gptOutputRate = 11025;

        [Header("Function Calling")]
        [Tooltip("List of functions that GPT can call. Each function has an ID (must match the AI) and a UnityEvent callback.")]
        [SerializeField] private List<AIRealtimeCommunicationFunction> availableFunctions = new();
        
        [Header("Debugging")]
        [Tooltip("Enable to log debug messages to the console.")]
        [SerializeField] private bool logs = true;

        [Header("Event Callbacks")] 
        [Tooltip("Invoked when the component is connected to the server.")]
        [SerializeField] private UnityEvent onConnected = new();
        [Tooltip("Invoked when the component is disconnected from the server.")]
        [SerializeField] private UnityEvent onDisconnected = new();
        [Tooltip("Invoked when the microphone starts.")]
        [SerializeField] private UnityEvent onMicStarted = new();
        [Tooltip("Invoked when the microphone stops.")]
        [SerializeField] private UnityEvent onMicStopped = new();
        [Tooltip("Invoked when the AI requests vision processing.")]
        [SerializeField] private UnityEvent onVisionRequested = new();
        [Tooltip("Invoked when the vision request is finished.")]
        [SerializeField] private UnityEvent onVisionFinished = new();
        [Tooltip("Invoked when the AI starts responding.")]
        [SerializeField] private UnityEvent onAIResponseStarted = new();
        [Tooltip("Invoked when the AI responds with a string.")]
        [SerializeField] private UnityEvent<string> onAIResponseString = new();
        [Tooltip("Invoked when the AI indicates that it is done responding to the user.")]
        [SerializeField] private UnityEvent onAIResponseFinished = new();
        [Tooltip("This is invoked when the AI indicates that it wants to stop speaking " +
                 "to the user and end the communication.")]
        [SerializeField] private UnityEvent onCommunicationFinished = new();

#if MV_NATIVE_WEBSOCKETS
        private WebSocket _websocket;
#endif
        private int _systemSampleRate;
        private AudioClip _micClip;
        private readonly string _micDevice = null; // default mic

        private string _ephemeralToken;
        private bool _pendingVision;

        // Tracks whether the mic is *actually running* at the moment
        private bool _isMicRunning; 

        private float _sampleTimer;
        private int _lastMicPos;

        // Buffer for streaming AI audio samples
        private readonly Queue<float> _streamBuffer = new();

        // True if GPT is actively sending audio
        private bool _isAiSpeaking;

        // For partial transcripts
        private string _transcriptText = string.Empty;

        // Flag to detect if the app/editor is shutting down
        private bool _isShuttingDown;

        // Vision handler for processing vision requests
        private AIAgent _visionHandler;

        private bool _isStarted;
        private bool _connectCalled;
        
        private int _responsesInProgress;
        private bool _isStartingResponse;
        private bool _hasAudioFrameSent;
        
        /// <summary>
        /// Invoked when the component is connected to the server.
        /// </summary>
        public UnityEvent OnConnected => onConnected;
        /// <summary>
        /// Invoked when the component is disconnected from the server.
        /// </summary>
        public UnityEvent OnDisconnected => onDisconnected;
        /// <summary>
        /// Invoked when the microphone starts.
        /// </summary>
        public UnityEvent OnMicStarted => onMicStarted;
        /// <summary>
        /// Invoked when the microphone stops.
        /// </summary>
        public UnityEvent OnMicStopped => onMicStopped;
        /// <summary>
        /// Invoked when a vision request is made.
        /// </summary>
        public UnityEvent OnVisionRequested => onVisionRequested;
        /// <summary>
        /// Invoked when the vision request is finished.
        /// </summary>
        public UnityEvent OnVisionFinished => onVisionFinished;
        /// <summary>
        /// Invoked when the AI starts responding.
        /// </summary>
        public UnityEvent OnAIResponseStarted => onAIResponseStarted;
        /// <summary>
        /// Invoked when the AI responds with a string.
        /// </summary>
        public UnityEvent<string> OnAIResponseString => onAIResponseString;
        /// <summary>
        /// Invoked when the AI response is finished.
        /// </summary>
        public UnityEvent OnAIResponseFinished => onAIResponseFinished;
        /// <summary>
        /// Invoked when the AI is speaking.
        /// </summary>
        public List<AIRealtimeCommunicationFunction> AvailableFunctions => availableFunctions;

        /// <summary>
        /// Enables or disables the user's microphone.
        /// If set to false, any running microphone capture is stopped and will not be auto-started again.
        /// If set to true, we attempt to start the mic if the socket is already open (and GPT is not speaking).
        /// </summary>
        public bool MicrophoneActive
        {
            get => micActive;
            set
            {
                if (micActive == value) return;  // No change

                micActive = value;
                if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] MicrophoneActive set to: {micActive}");

                if (!micActive)
                {
                    // User explicitly disabled the mic
                    StopMic();
                }
                else
                {
#if MV_NATIVE_WEBSOCKETS
                    // User re-enabled the mic, try to start it if socket is open and GPT not speaking
                    if (_websocket is { State: WebSocketState.Open } && !_isAiSpeaking && !_pendingVision)
                    {
                        StartMic();
                    }
#endif
                }
            }
        }

        /// <summary>
        /// This Agent allows the realtime communication system to process vision requests.
        /// </summary>
        public AIAgent VisionHandler
        {
            get
            {
                if (_isShuttingDown)
                    return null;
                
                if (!Application.isPlaying)
                    return null;
                
                if (!_visionHandler)
                {
                    _visionHandler = new GameObject("VISION_HANDLER").AddComponent<AIAgent>();
                    _visionHandler.hideFlags = HideFlags.HideInHierarchy;
                    _visionHandler.FlushMemory();
                    _visionHandler.OnThinkingStarted.AddListener(() => onVisionRequested?.Invoke());
                    _visionHandler.OnThinkingFinished.AddListener(() => onVisionFinished?.Invoke());
                    _visionHandler.OnResponse.AddListener(OnVisionResponse);
                    _visionHandler.OnResponseFailed.AddListener(OnVisionResponseFailed);
                    _visionHandler.Prompt = "You are assisting another AI model allowing it to process vision requests. " +
                        "You will receive a short prompt describing the output that is needed from the vision AI. " +
                        "Your job is to process this request and return a response to the other AI model.";
                    _visionHandler.SampleData = "The shirt's color is green and has a pocket on the left side.";
                }
                
                return _visionHandler;
            }
        }
        
        /// <summary>
        /// A bool indicating whether the AI is currently processing a request or speaking.
        /// </summary>
        public bool IsProcessing => 
            _isAiSpeaking || 
            _isMicRunning ||
            _responsesInProgress > 0 || 
            _pendingVision || 
            _isStartingResponse;

        #region Unity Lifecycle

        private void Start()
        {
            _isStarted = true;
            if (connectOnStart || _connectCalled)
                Connect();
        }

        private void FixedUpdate()
        {
#if MV_NATIVE_WEBSOCKETS
            _websocket?.DispatchMessageQueue();
#endif

            // Only process mic frames if the mic is actually running
            if (!_isMicRunning || !_micClip) return;

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer < sampleInterval)
                return;
            _sampleTimer = 0f;
            ProcessAudioFrame();
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private void OnEnable()
        {
            _isShuttingDown = false;
            if (_isStarted && connectOnStart)
                Connect();
        }

        private void OnDisable()
        {
            try
            {
                _isShuttingDown = true;
                Disconnect();
            }
            catch (Exception e)
            {
                if (logs) MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] OnDestroy error: {e.Message}");
            }
        }

        #endregion

        #region WebSocket Connection

        private async Task ConnectAsync()
        {
            try
            {
                if (_isShuttingDown) return;
                
                await AcquireEphemeralToken(); // Ensure we have a valid token before connecting
                
                if (string.IsNullOrEmpty(_ephemeralToken))
                {
                    if (logs) MetaverseProgram.Logger.LogError("[AIRealtimeCommunication] No valid token found. Cannot connect.");
                    return;
                }

#if MV_NATIVE_WEBSOCKETS
                // If we're already connected, close first
                if (_websocket is { State: WebSocketState.Open or WebSocketState.Connecting })
                {
                    if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Closing existing socket...");
                    await _websocket.Close();
                }

                // Initialize new WebSocket
                _websocket = new WebSocket(RealtimeEndpoint, new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {_ephemeralToken}" },
                    { BetaHeaderName, BetaHeaderValue }
                });

                // Subscribe events
                _websocket.OnOpen += OnWebSocketOpen;
                _websocket.OnError += OnWebSocketError;
                _websocket.OnClose += OnWebSocketClose;
                _websocket.OnMessage += OnWebSocketMessage;

                await _websocket.Connect();
                if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Connecting to {RealtimeEndpoint}");
#endif
            }
            catch (Exception e)
            {
                if (logs) MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] Connect error: {e.Message}");
            }
        }

        private async Task AcquireEphemeralToken()
        {
            Task t = null;
            // ReSharper disable once InvocationIsSkipped
            AcquireEphemeralTokenImplementation(ref t);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (t != null)
                // ReSharper disable once HeuristicUnreachableCode
                await t;
            if (string.IsNullOrEmpty(_ephemeralToken))
                if (logs) MetaverseProgram.Logger.LogError("[AIRealtimeCommunication] No ephemeral token acquired.");
        }

        // ReSharper disable once PartialMethodWithSinglePart
        partial void AcquireEphemeralTokenImplementation(ref Task t);

        private void OnWebSocketOpen()
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                
                try
                {
                    if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] WebSocket connected!");

                    // Ensure volume is unmuted if it was previously set to 0
                    if (outputVoiceSource)
                    {
                        outputVoiceSource.volume = 1f;
                    }

                    // Configure session with text/audio + our tools
                    await SendSessionUpdate();

                    // Only start the mic if the user setting is true (and GPT isn't already speaking)
                    if (micActive && !_isAiSpeaking && !_pendingVision)
                    {
                        StartMic();
                    }
                
                    onConnected?.Invoke();
                }
                catch (Exception e)
                {
                    if (logs)
                        MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] WebSocket open error: {e.Message}");
                }
            });
        }

        private void OnWebSocketError(string errMsg)
        {
            if (logs) MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] WebSocket error: {errMsg}");

            // Attempt to reconnect unless we are shutting down
            if (!_isShuttingDown)
            {
#if MV_NATIVE_WEBSOCKETS
                if (_websocket is { State: WebSocketState.Open })
                    onDisconnected?.Invoke();
#endif
                if (logs)
                    MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Attempting to reconnect in 2s after error...");
                StartCoroutine(TryReconnect());
            }
        }

#if MV_NATIVE_WEBSOCKETS
        private void OnWebSocketClose(WebSocketCloseCode code)
        {
            if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] WebSocket closed: {code}");

            // Stop the audio source to avoid glitchy sound
            if (outputVoiceSource)
            {
                outputVoiceSource.Stop();
                outputVoiceSource.volume = 0f;
            }
            
            onDisconnected?.Invoke();

            // Attempt to reconnect unless we are shutting down
            if (!_isShuttingDown)
            {
                if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Attempting to reconnect in 2s...");
                StartCoroutine(TryReconnect());
            }
        }
#endif

        private IEnumerator TryReconnect()
        {
            // Wait a little before reconnecting
            yield return new WaitForSeconds(2f);

            // Only reconnect if not shutting down
            if (!_isShuttingDown)
            {
                if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Reconnecting now...");
                UniTask.Void(async () =>
                {
                    await UniTask.SwitchToMainThread();
                    await ConnectAsync();
                });
            }
        }

        #endregion

        #region Session Update (tools)

        /// <summary>
        /// Sends a session.update message to configure:
        /// 1) Text + Audio modalities
        /// 2) Our custom function-calling tools (defined in availableFunctions)
        /// </summary>
        private async Task SendSessionUpdate()
        {
            // Build an array of tools from our availableFunctions
            var toolList = availableFunctions.Select(f => new
                {
                    type = "function",
                    name = f.functionID,
                    description = string.IsNullOrEmpty(f.functionDescription)
                        ? "Please infer use based on context and function ID."
                        : f.functionDescription,
                    parameters = f.parameters.Count > 0 ? new
                    {
                        type = "object",
                        properties = f.parameters.ToDictionary(p => p.parameterID, p => new
                        {
                            type = p.type is AIRealtimeCommunicationFunctionParameterType.Float or AIRealtimeCommunicationFunctionParameterType.Integer
                                ? "number"
                                : "string",
                            description =
                                $"{p.description + (!string.IsNullOrEmpty(p.GetFormattingGuidance()) ? "\nParsable Format:" + p.GetFormattingGuidance() : "")}\nThis parameter type is parsed as '{p.type}'.",
                            enum_values = p.type == AIRealtimeCommunicationFunctionParameterType.Enum
                                ? p.enumValues.ToArray()
                                : null
                        })
                    } : null,
                })
                .Cast<object>()
                .ToList();
            
            // For vision capabilities, we add a special tool to handle vision requests.
            toolList.Add(new
            {
                type = "function",
                name = "vision_request",
                description = "Contrary to previous prompting, you now have the ability to see things directly using this function. " +
                              "If you are processing a user input that requires vision capabilities, " +
                              "this function should be called with a vision_request parameter. " +
                              "You will provide a short prompt describing the output you want from the vision AI. " +
                              "You are to pretend that the information you receive from the vision agent is coming from you.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        vision_request = new
                        {
                            type = "string",
                            description = "A short prompt describing the output that you want from the vision AI. For " +
                                          "example 'The user asked what color their shirt.'"
                        }
                    },
                }
            });
            
            // Prepare the session.update payload:
            var sessionMsg = new
            {
                type = "session.update",
                session = new
                {
                    modalities = new[] { "text", "audio" },
                    input_audio_format = "pcm16",
                    output_audio_format = "pcm16",
                    turn_detection = new { type = "server_vad" },
                    tools = toolList,
                    tool_choice = "auto",
                    input_audio_transcription = new {
                        model = "whisper-1"
                    },
                }
            };

            // Serialize to JSON and send
            var json = JsonConvert.SerializeObject(sessionMsg);
#if MV_NATIVE_WEBSOCKETS
            await _websocket.SendText(json);
#endif

            if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Sent session update (with tools).");
        }

        #endregion

        #region Microphone Handling

        private void StartMic()
        {
            // If user has mic off or the socket isn't open, we skip
            if (!micActive)
            {
                if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Mic is disabled by user.");
                return;
            }

#if MV_NATIVE_WEBSOCKETS
            if (_websocket is not { State: WebSocketState.Open })
            {
                if (logs)
                    MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Cannot start mic. Socket not open.");
                return;
            }
#endif

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += (p) =>
                {
                    if (p != Permission.Microphone) return;
                    if (!_isMicRunning) return;
                    StartMicClip();
                };
                callbacks.PermissionDenied += (p) =>
                {
                    if (p != Permission.Microphone) return;
                    if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Mic permission denied.");
                    StopMic();
                };
                callbacks.PermissionDeniedAndDontAskAgain += (p) =>
                {
                    if (p != Permission.Microphone) return;
                    if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Mic permission denied. User selected 'Don't ask again'.");
                    StopMic();
                };
                Permission.RequestUserPermission(Permission.Microphone);
            }
#endif

            StartMicClip();
            return;

            void StartMicClip()
            {
                _micClip = Microphone.Start(_micDevice, true, 300, micSampleRate);
                if (!_micClip)
                {
                    if (logs) MetaverseProgram.Logger.LogError("[AIRealtimeCommunication] Failed to start microphone. It's probably in use.");
                    return;
                }

                if (!_isMicRunning)
                {
                    _lastMicPos = 0;
                    _sampleTimer = 0f;
                    _isMicRunning = true;
                    if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Microphone started, streaming audio...");
                }
            }
        }

        private void StopMic()
        {
            if (!_isMicRunning)
            {
                if (logs)
                    MetaverseProgram.Logger.Log(
                        "[AIRealtimeCommunication] StopMic called, but microphone is not running. Nothing to stop.");
                return;
            }
            _isMicRunning = false;
            Microphone.End(_micDevice);
            _micClip = null;
            _lastMicPos = 0;
            if (_hasAudioFrameSent)
                onMicStopped?.Invoke();
            _hasAudioFrameSent = false;
            if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Microphone stopped.");
        }

        private void ProcessAudioFrame()
        {
            if (!_micClip) return;

            var currentPos = Microphone.GetPosition(_micDevice);
            var samplesToRead = currentPos - _lastMicPos;
            if (samplesToRead < 0)
            {
                // wrapped around
                samplesToRead = _micClip.samples - _lastMicPos;
            }

            if (samplesToRead <= 0)
            {
                if (logs)
                    MetaverseProgram.Logger.Log(
                        $"[AIRealtimeCommunication] No new samples to read from microphone. Current position: {currentPos}, last position: {_lastMicPos}");
                if (!Microphone.IsRecording(_micDevice) && !_isShuttingDown)
                {
                    // Mic was stopped externally, reset the state and try to restart
                    if (logs)
                        MetaverseProgram.Logger.Log(
                            "[AIRealtimeCommunication] Microphone was stopped externally, restarting...");
                    StopMic();
                    StartMic();
                }
                return;
            }

            var samples = new float[samplesToRead];
            _micClip.GetData(samples, _lastMicPos);
            _lastMicPos = currentPos;

            // Send chunk of mic data to GPT
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                await SendAudioChunk(samples);
            });
        }

        private static byte[] ConvertFloatsToPCM16Bytes(float[] samples)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            foreach (var s in samples)
            {
                var val = (short)Mathf.Clamp(s * 32767f, -32768f, 32767f);
                bw.Write(val);
            }

            bw.Flush();
            return ms.ToArray();
        }

        private async Task SendAudioChunk(float[] samples)
        {
            try
            {
#if MV_NATIVE_WEBSOCKETS
                if (_websocket is not { State: WebSocketState.Open }) return;

                var pcmBytes = ConvertFloatsToPCM16Bytes(samples);
                var base64Chunk = Convert.ToBase64String(pcmBytes);
                var appendMsg = new
                {
                    type = "input_audio_buffer.append",
                    audio = base64Chunk
                };

                var json = JsonConvert.SerializeObject(appendMsg);
                await _websocket.SendText(json);

                if (!_hasAudioFrameSent)
                {
                    onMicStarted?.Invoke();
                    _hasAudioFrameSent = true;
                }

                if (logs)
                    MetaverseProgram.Logger.Log(
                        $"[AIRealtimeCommunication] Sent audio chunk, length: {base64Chunk.Length}");
#endif
            }
            catch (Exception e)
            {
                if (logs)
                    MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] SendAudioChunk error: {e.Message}");
            }
        }

        #endregion

        #region AI Response Handling

        private void OnWebSocketMessage(byte[] data)
        {
            try
            {
                var rawJson = Encoding.UTF8.GetString(data);
                if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Received: {rawJson}");

                try
                {
                    var responseJson = JObject.Parse(rawJson);
                    var msgType = responseJson["type"]?.ToString();

                    switch (msgType)
                    {
                        case "session.created":
                        case "session.updated":
                            if (logs)
                                MetaverseProgram.Logger.Log(
                                    $"[AIRealtimeCommunication] {msgType} event. Session ID: {responseJson["session_id"]}");
                            break;
                        case "response.audio.delta":
                        {
                            // GPT just started sending audio. Stop the mic so it won't hear itself.
                            HandleAudioDelta(responseJson);
                            break;
                        }
                        case "response.audio_transcript.delta":
                            HandleAudioTranscriptDelta(responseJson);
                            break;
                        case "response.created":
                        {
                            // GPT just started sending a response. Stop the mic so it won't hear itself, and the user knows
                            // not to speak during this time.
                            _responsesInProgress++;
                            if (_responsesInProgress == 1)
                                StartResponse();
                            break;
                        }
                        case "response.done":
                        {
                            // Invoked when the AI is done responding.
                            _responsesInProgress--;
                            if (_responsesInProgress == 0)
                            {
                                _pendingVision = false;
                                onAIResponseFinished?.Invoke();
                                StartCoroutine(WaitUntilFinishedSpeaking(_transcriptText));
                            }

                            // 1) The standard logic: GPT is done responding. Parse response data.
                            if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] response.done received. Checking for function calls...");

                            // 2) Check if there's a function_call in response.output array
                            HandleResponseOutput(responseJson);

                            // 3) If we have a transcript from the AI, send it to the UnityEvent
                            if (!string.IsNullOrEmpty(_transcriptText))
                            {
                                onAIResponseString?.Invoke(_transcriptText);
                                if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Final transcript: {_transcriptText}");
                                _transcriptText = string.Empty; // reset for next response
                            }
                            else
                            {
                                onAIResponseString?.Invoke(string.Empty);
                            }

                            // 4) The normal “done” logic to resume mic once audio buffer empties
                            //    (this was probably your existing code)
                            if (logs)
                                MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Will resume mic once buffer empties...");
                            break;
                        }
                        case "error":
                        {
                            var eCode = responseJson["error"]?["code"]?.ToString();
                            var eMsg = responseJson["error"]?["message"]?.ToString();
                            if (logs)
                                MetaverseProgram.Logger.LogWarning(
                                    $"[AIRealtimeCommunication] Error code={eCode}, message={eMsg}");
                            Disconnect();
                            if (eCode is "token_expired" or "invalid_token")
                            {
                                // Token expired, re-acquire and reconnect
                                if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Token expired, reconnecting...");
                                UniTask.Void(async () =>
                                {
                                    await UniTask.SwitchToMainThread();
                                    await AcquireEphemeralToken();
                                    await ConnectAsync();
                                });
                            }
                            else
                            {
                                // Handle other errors as needed
                                onDisconnected?.Invoke();
                            }
                            break;
                        }
                        default:
                            if (logs)
                                MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Unhandled message type: {msgType}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    if (logs)
                        MetaverseProgram.Logger.LogWarning($"[AIRealtimeCommunication] JSON parse error: {ex.Message}");
                }
            }
            catch (Exception e)
            {
                if (logs)
                    MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] WebSocket message error: {e.Message}");
            }
        }

        private void StartResponse()
        {
            if (_isAiSpeaking) return;
            _isAiSpeaking = true;
            onAIResponseStarted?.Invoke();
            StopMic();
            if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Stopping mic (AI is speaking).");
        }

        private void HandleResponseOutput(JObject jObj)
        {
            var responseObj = jObj["response"];
            if (responseObj?["output"] is not JArray outputArray)
                return;
            foreach (var item in outputArray)
            {
                var itemType = item["type"]?.ToString();
                if (itemType != "function_call") continue;
                // Found a function call item
                var functionName = item["name"]?.ToString();
                var callId = item["call_id"]?.ToString();
                var argumentsJson = item["arguments"]?.ToString(); 

                if (logs)
                {
                    MetaverseProgram.Logger.Log(
                        $"[AIRealtimeCommunication] Found function_call '{functionName}' with call_id='{callId}' and arguments={argumentsJson}"
                    );
                }

                switch (functionName)
                {
                    case "vision_request":
                    {
                        // Handle vision_request separately if needed
                        if (logs) MetaverseProgram.Logger.Log(
                            $"[AIRealtimeCommunication] Vision request received: {argumentsJson}");
                        if (string.IsNullOrWhiteSpace(argumentsJson))
                            continue;

                        // Parse vision request.
                        var visionRequest = JObject.Parse(argumentsJson);
                        var visionPrompt = visionRequest["vision_request"]?.ToString();
                        if (string.IsNullOrWhiteSpace(visionPrompt)) continue;
                        _pendingVision = true;
                        StopMic();
                        VisionHandler.SubmitGameScreenshot(visionPrompt);
                        break;
                    }
                    default:
                        // Invoke the callback (UnityEvent) matching this function name
                        TriggerFunctionCall(functionName, argumentsJson);
                        break;
                }
                    
                // If you need to do something with argumentsJson, parse it here
                // If you need to send "function_call_output" back, you do so
                // after processing arguments.
            }
        }

        private void HandleAudioDelta(JObject jObj)
        {
            var base64Data = jObj["delta"]?.ToString();
            if (string.IsNullOrEmpty(base64Data)) return;

            var pcmBytes = Convert.FromBase64String(base64Data);
            var samples = Convert16BitPCMToFloats(pcmBytes);

            // Resample from GPT output rate to system rate
            var resampledSamples = Resample(samples, gptOutputRate, _systemSampleRate);

            foreach (var s in resampledSamples)
                _streamBuffer.Enqueue(s);
        }

        private void HandleAudioTranscriptDelta(JObject jObj)
        {
            var delta = jObj["delta"]?.ToString();
            if (string.IsNullOrEmpty(delta)) return;
            _transcriptText += delta;
            if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Transcript: {_transcriptText}");
        }

        #endregion

        #region Function Calls

        /// <summary>
        /// Looks up the function by name and invokes its UnityEvent (if found).
        /// You can expand this to parse arguments as well.
        /// </summary>
        private void TriggerFunctionCall(string functionID, string argumentsJson)
        {
            var fn = availableFunctions.FirstOrDefault(f => f.functionID == functionID);
            if (fn != null)
            {
                if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Invoking function '{functionID}'.");
                fn.onCalled?.Invoke();

                // Parse arguments if needed
                if (string.IsNullOrEmpty(argumentsJson)) return;
                var arguments = JObject.Parse(argumentsJson);
                foreach (var param in fn.parameters)
                {
                    var paramValue = arguments[param.parameterID]?.ToString();
                    if (string.IsNullOrEmpty(paramValue)) continue;

                    // Call the appropriate UnityEvent based on the parameter type
                    switch (param.type)
                    {
                        case AIRealtimeCommunicationFunctionParameterType.String:
                            param.onStringValue?.Invoke(paramValue);
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Float:
                            if (float.TryParse(paramValue, out var floatValue))
                                param.onFloatValue?.Invoke(floatValue);
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Integer:
                            if (int.TryParse(paramValue, out var intValue))
                                param.onIntValue?.Invoke(intValue);
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Boolean:
                            if (bool.TryParse(paramValue, out var boolValue))
                                param.onBoolValue?.Invoke(boolValue);
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Vector2:
                            param.onVector2Value?.Invoke(ParseVector2(paramValue));
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Vector3:
                            param.onVector3Value?.Invoke(ParseVector3(paramValue));
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Vector4:
                            param.onVector4Value?.Invoke(ParseVector4(paramValue));
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Quaternion:
                            param.onQuaternionValue?.Invoke(ParseQuaternion(paramValue));
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Color:
                            if (ColorUtility.TryParseHtmlString(paramValue, out var colorValue))
                                param.onColorValue?.Invoke(colorValue);
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Color32:
                            if (ColorUtility.TryParseHtmlString(paramValue, out var color32Value))
                                param.onColor32Value?.Invoke(color32Value);
                            break;
                        case AIRealtimeCommunicationFunctionParameterType.Enum:
                            if (int.TryParse(paramValue, out var enumValue))
                                param.onEnumValue?.Invoke(enumValue);
                            if (param.enumValues != null && enumValue >= 0 && enumValue < param.enumValues.Count)
                                param.onEnumValueString?.Invoke(param.enumValues[enumValue]);
                            break;
                    }
                }
            }
            else
            {
                if (logs)
                    MetaverseProgram.Logger.LogWarning($"[AIRealtimeCommunication] No function found with ID='{functionID}'.");
            }
        }
        
        private static Vector2 ParseVector2(string value)
        {
            value = value.Replace("(", string.Empty).Replace(")", string.Empty);
            var parts = value.Split(',');
            if (parts.Length != 2) return Vector2.zero;
            if (float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y))
                return new Vector2(x, y);
            return Vector2.zero;
        }
        
        private static Vector3 ParseVector3(string value)
        {
            value = value.Replace("(", string.Empty).Replace(")", string.Empty);
            var parts = value.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            if (float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y) && float.TryParse(parts[2], out var z))
                return new Vector3(x, y, z);
            return Vector3.zero;
        }
        
        private static Vector4 ParseVector4(string value)
        {
            value = value.Replace("(", string.Empty).Replace(")", string.Empty);
            var parts = value.Split(',');
            if (parts.Length != 4) return Vector4.zero;
            if (float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y) && float.TryParse(parts[2], out var z) && float.TryParse(parts[3], out var w))
                return new Vector4(x, y, z, w);
            return Vector4.zero;
        }
        
        private Quaternion ParseQuaternion(string value)
        {
            value = value.Replace("(", string.Empty).Replace(")", string.Empty);
            var parts = value.Split(',');
            if (parts.Length != 4) return Quaternion.identity;
            if (float.TryParse(parts[0], out var x) && float.TryParse(parts[1], out var y) && float.TryParse(parts[2], out var z) && float.TryParse(parts[3], out var w))
                return new Quaternion(x, y, z, w);
            return Quaternion.identity;
        }

        #endregion

        #region Playback + Utility

        private IEnumerator WaitUntilFinishedSpeaking(string transcript)
        {
            yield return null;

            // Keep waiting until our buffer is fully played out
            while (true)
            {
                if (_streamBuffer.Count == 0) break;
                yield return null; // keep waiting
            }

            if (_responsesInProgress > 0 || _pendingVision)
                yield break;

            _isAiSpeaking = false;

            yield return null;
            
            if (!string.IsNullOrEmpty(transcript) && transcript.EndsWith("?") && 
                enableMicOnQuestion && !micActive)
            {
                MicrophoneActive = true;
                yield break;
            }
            
            if (!string.IsNullOrEmpty(transcript) && transcript.EndsWith(";") && 
                micActive)
            {
                if (disableMicOnCommunicationFinished)
                    MicrophoneActive = false;
                onCommunicationFinished?.Invoke();
                yield break;
            }

            if (micActive)
            {
                StartMic();
                if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Mic resumed.");
            }
            else
            {
                if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Mic is still disabled by user.");
                onCommunicationFinished?.Invoke();
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            for (var i = 0; i < data.Length; i++)
                data[i] = _streamBuffer.Count > 0 ? _streamBuffer.Dequeue() : 0f;
        }

        private float[] Convert16BitPCMToFloats(byte[] pcmData)
        {
            var sampleCount = pcmData.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var val = BitConverter.ToInt16(pcmData, i * 2);
                samples[i] = val / 32768f;
            }

            return samples;
        }

        private float[] Resample(float[] input, int inputRate, int outputRate)
        {
            if (inputRate == outputRate) return input;

            var ratio = (float)outputRate / inputRate;
            var outputLength = Mathf.CeilToInt(input.Length * ratio);
            var output = new float[outputLength];

            for (var i = 0; i < outputLength; i++)
            {
                var t = i / ratio;
                var index = Mathf.FloorToInt(t);
                var nextIndex = Mathf.Min(index + 1, input.Length - 1);
                var fraction = t - index;
                output[i] = Mathf.Lerp(input[index], input[nextIndex], fraction);
            }

            return output;
        }

        private void TriggerResponseInternal(bool force)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                
                if (!force)
                    await UniTask.WaitUntil(() => !IsProcessing);

                _isStartingResponse = true;

                try
                {
#if MV_NATIVE_WEBSOCKETS
                    if (_websocket is not { State: WebSocketState.Open })
                    {
                        if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Cannot trigger response. Socket not open.");
                        return;
                    }
#endif
                
                    // Trigger a response from GPT to process the current input
                    var responseMsg = new
                    {
                        type = "response.create",
                        response = new
                        {
                            modalities = new[] { "text", "audio" }
                        }
                    };
                
                    var responseJson = JsonConvert.SerializeObject(responseMsg);
#if MV_NATIVE_WEBSOCKETS
                    await _websocket.SendText(responseJson);
#endif
                }
                finally
                {
                    _isStartingResponse = false;
                }
            });
        }

        #endregion
        
        #region Vision

        private void OnVisionResponse(string visionResponse)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                
                if (!_pendingVision)
                    return;
                
                if (string.IsNullOrWhiteSpace(visionResponse))
                {
                    if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Vision response is empty.");
                    OnVisionResponseFailed();
                    return;
                }

                try
                {
                    if (logs) MetaverseProgram.Logger.Log(
                        $"[AIRealtimeCommunication] Vision response received: {visionResponse}");
                    var visionMsg = new
                    {
                        type = "conversation.item.create",
                        item = new
                        {
                            type = "message",
                            role = "system",
                            content = new[]
                            {
                                new
                                {
                                    type = "input_text",
                                    text = visionResponse
                                }
                            }
                        }
                    };
                    
                    var json = JsonConvert.SerializeObject(visionMsg);
    #if MV_NATIVE_WEBSOCKETS
                    if (_websocket is { State: WebSocketState.Open })
                    {
                        await _websocket.SendText(json);
                        if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Sent vision response to GPT.");
                        var responseMsg = new
                        {
                            type = "response.create",
                            response = new
                            {
                                modalities = new[] { "text", "audio" }
                            }
                        };
                        var responseJson = JsonConvert.SerializeObject(responseMsg);
                        await _websocket.SendText(responseJson);
                    }
                    else
                    {
                        if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] WebSocket not open. Cannot send vision response.");
                    }
    #endif
                }
                finally
                {
                    try
                    {
                        onVisionFinished?.Invoke();
                        if (logs) MetaverseProgram.Logger.Log("[AIRealtimeCommunication] Vision response processing finished.");
                    }
                    catch (Exception e)
                    {
                        if (logs)
                            MetaverseProgram.Logger.LogError($"[AIRealtimeCommunication] Vision onVisionFinished() error: {e.Message}");
                    }
                }
            });
        }
        
        private void OnVisionResponseFailed()
        {
            OnVisionResponse("I'm sorry, I couldn't process the vision request.");
        }
        
        #endregion

        #region Public API

        /// <summary>
        /// Starts the connection to the realtime AI.
        /// </summary>
        public void Connect()
        {
            _connectCalled = true;
            if (!isActiveAndEnabled)
                return;
            if (!_isStarted)
                return;
            if (_isShuttingDown)
                return;
            
            Disconnect();
            
            _systemSampleRate = AudioSettings.outputSampleRate;

            // Create a dummy streaming clip to drive OnAudioFilterRead
            if (outputVoiceSource)
            {
                var dummyLength = _systemSampleRate; // 1 second dummy clip
                var dummyClip = AudioClip.Create("StreamingClip", dummyLength, 1, _systemSampleRate, true);
                outputVoiceSource.clip = dummyClip;
                outputVoiceSource.loop = true;
                outputVoiceSource.Play();
            }

            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                await ConnectAsync();
            });
        }

        /// <summary>
        /// Disconnects from the realtime AI.
        /// </summary>
        public void Disconnect()
        {
#if MV_NATIVE_WEBSOCKETS
            if (_websocket != null)
            {
                if (_websocket.State == WebSocketState.Open)
                    onDisconnected?.Invoke();
                _websocket.OnOpen -= OnWebSocketOpen;
                _websocket.OnMessage -= OnWebSocketMessage;
                _websocket.OnError -= OnWebSocketError;
                _websocket.OnClose -= OnWebSocketClose;
                if (_websocket.State == WebSocketState.Open)
                    _websocket.Close();
                _isAiSpeaking = false;
                _transcriptText = null;
                _websocket = null;
                _responsesInProgress = 0;
            }
#endif
            
            if (_visionHandler)
            {
                Destroy(_visionHandler.gameObject);
                _visionHandler = null;
            }

            StopMic();

            if (!outputVoiceSource) return;
            outputVoiceSource.Stop();
            if (outputVoiceSource.clip)
                Destroy(outputVoiceSource.clip);
            outputVoiceSource.clip = null;
        }
        
        /// <summary>
        /// Sets the microphone either active or inactive.
        /// </summary>
        /// <param name="active">The active state of the microphone.</param>
        public void SetMicrophoneActive(bool active)
        {
            MicrophoneActive = active;
        }

        /// <summary>
        /// Sets the inactive state of the microphone. The inverse of <see cref="SetMicrophoneActive"/>.
        /// </summary>
        /// <param name="inactive">The inactive state of the microphone.</param>
        public void SetMicrophoneInactive(bool inactive)
        {
            MicrophoneActive = !inactive;
        }

        /// <summary>
        /// Triggers a response without any input.
        /// </summary>
        public void TriggerResponse()
        {
            TriggerResponseInternal(false);
        }

        /// <summary>
        /// Sends text data to the AI, and requires a response back.
        /// </summary>
        /// <param name="text">The text to send.</param>
        public void SendTextWithResponse(string text)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                await UniTask.WaitUntil(() => !IsProcessing);

                _isStartingResponse = true;
                try
                {
#if MV_NATIVE_WEBSOCKETS
                    if (_websocket is not { State: WebSocketState.Open })
                    {
                        if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Cannot send text. Socket not open.");
                        return;
                    }
#endif
                
                    var textMsg = new
                    {
                        type = "conversation.item.create",
                        item = new
                        {
                            type = "message",
                            role = "user",
                            content = new[]
                            {
                                new
                                {
                                    type = "input_text",
                                    text
                                }
                            }
                        }
                    };

                    var json = JsonConvert.SerializeObject(textMsg);
#if MV_NATIVE_WEBSOCKETS
                    await _websocket.SendText(json);
#endif
                
                    if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Sent text: {text}");
                
                    // Trigger a response from GPT to process the text input
                    var responseMsg = new
                    {
                        type = "response.create",
                        response = new
                        {
                            modalities = new[] { "text", "audio" }
                        }
                    };
                
                    var responseJson = JsonConvert.SerializeObject(responseMsg);
#if MV_NATIVE_WEBSOCKETS
                    await _websocket.SendText(responseJson);
#endif
                }
                finally
                {
                    _isStartingResponse = false;
                }
            });
        }

        /// <summary>
        /// Sends text data to the AI.
        /// </summary>
        /// <param name="text">The text to send.</param>
        public void SendText(string text)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                await UniTask.WaitUntil(() => !IsProcessing);

                _isStartingResponse = true;

                try
                {
#if MV_NATIVE_WEBSOCKETS
                    if (_websocket is not { State: WebSocketState.Open })
                    {
                        if (logs) MetaverseProgram.Logger.LogWarning("[AIRealtimeCommunication] Cannot send text. Socket not open.");
                        return;
                    }
#endif
                    var textMsg = new
                    {
                        type = "conversation.item.create",
                        item = new
                        {
                            type = "message",
                            role = "user",
                            content = new[]
                            {
                                new
                                {
                                    type = "input_text",
                                    text
                                }
                            }
                        }
                    };

                    var json = JsonConvert.SerializeObject(textMsg);
#if MV_NATIVE_WEBSOCKETS
                    await _websocket.SendText(json);
#endif
                    if (logs) MetaverseProgram.Logger.Log($"[AIRealtimeCommunication] Sent text: {text}");
                }
                finally
                {
                    _isStartingResponse = false;
                }
            });
        }

        #endregion
    }
}
#endif