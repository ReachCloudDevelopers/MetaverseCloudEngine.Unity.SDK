#if MV_NATIVE_WEBSOCKETS
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
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TriInspectorMVCE;

namespace MetaverseCloudEngine.Unity.AI.Components
{
    /// <summary>
    /// A Unity MonoBehaviour to stream audio from the microphone to OpenAI's GPT-4o Realtime API,
    /// including function-calling support.
    /// </summary>
    [HideMonoScript]
    public partial class AIRealtimeCommunication : TriInspectorMonoBehaviour
    {
        #region Function Definition Class

        [Serializable]
        public class Function
        {   
            [Tooltip("Identifier/name of the function as recognized by the AI.")]
            public string functionID;

            [TextArea]
            [Tooltip("Description to help the AI decide when to call this function.")]
            public string functionDescription;

            [Tooltip("Event invoked when the AI calls this functionID.")]
            public UnityEvent onCalled;
        }

        #endregion

        private const string BetaHeaderName = "OpenAI-Beta";
        private const string BetaHeaderValue = "realtime=v1";

        [Header("OpenAI Realtime Settings")]
        [SerializeField]
        private string realtimeEndpoint = "wss://api.openai.com/v1/realtime?model=gpt-4o-realtime-preview";

        // Microphone Streaming
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

        // Audio Output (GPT Response)
        [Header("Output (GPT Response)")]
        [SerializeField]
        [Required]
        private AudioSource outputVoiceSource;
        [Tooltip("The voice to use for the AI's audio output.")]
        [SerializeField] 
        private TextToSpeechVoicePreset outputVoice = TextToSpeechVoicePreset.Male;
        [SerializeField]
        [TextArea(5, 10)]
        private string prompt;

        [Tooltip("Enable to log debug messages to the console.")]
        [SerializeField] 
        private bool logs;

        [Range(8000, 48000)]
        [Tooltip("The sample rate (in Hz) of the GPT output audio. Adjust for speed/pitch of the AI's voice.")]
        [SerializeField] 
        private int gptOutputRate = 11025;

        [Header("Function Calling")]
        [Tooltip("List of functions that GPT can call. Each function has an ID (must match the AI) and a UnityEvent callback.")]
        [SerializeField]
        private List<Function> availableFunctions = new();

        private WebSocket _websocket;
        private int _systemSampleRate;
        private AudioClip _micClip;
        private readonly string _micDevice = null; // default mic

        private string _ephemeralToken;

        // Tracks whether the mic is *actually running* at the moment
        private bool _isMicRunning; 

        private float _sampleTimer;
        private int _lastMicPos;

        // Buffer for streaming AI audio samples
        private readonly object _bufferLock = new();
        private readonly Queue<float> _streamBuffer = new();

        // True if GPT is actively sending audio
        private bool _isSpeaking;

        // For partial transcripts
        private string _transcriptText = string.Empty;

        // Flag to detect if the app/editor is shutting down
        private bool _isShuttingDown;

        private AIAgent _visionHandler;

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
                if (logs) MetaverseProgram.Logger.Log($"[GPTRealtimeAudioClient] MicrophoneActive set to: {micActive}");

                if (!micActive)
                {
                    // User explicitly disabled the mic
                    StopMic();
                }
                else
                {
                    // User re-enabled the mic, try to start it if socket is open and GPT not speaking
                    if (_websocket is { State: WebSocketState.Open } && !_isSpeaking)
                    {
                        StartMic();
                    }
                }
            }
        }

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

        #region Unity Lifecycle

        private void Start()
        {
            _systemSampleRate = AudioSettings.outputSampleRate;

            // Create a dummy streaming clip to drive OnAudioFilterRead
            if (outputVoiceSource != null)
            {
                var dummyLength = _systemSampleRate; // 1 second dummy clip
                var dummyClip = AudioClip.Create("StreamingClip", dummyLength, 1, _systemSampleRate, true);
                outputVoiceSource.clip = dummyClip;
                outputVoiceSource.loop = true;
                outputVoiceSource.Play();
            }

            Connect();
        }

        private void Update()
        {
            _websocket?.DispatchMessageQueue();

            // Only process mic frames if the mic is actually running
            if (!_isMicRunning || _micClip == null) return;

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= sampleInterval)
            {
                _sampleTimer = 0f;
                ProcessAudioFrame();
            }
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private async void OnDestroy()
        {
            try
            {
                _isShuttingDown = true; // block any further reconnect attempts

                if (_visionHandler)
                {
                    Destroy(_visionHandler.gameObject);
                    _visionHandler = null;
                }

                if (_websocket != null)
                {
                    await _websocket.Close();
                }

                StopMic();
            }
            catch (Exception e)
            {
                if (logs) MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] OnDestroy error: " + e.Message);
            }
        }

        #endregion

        #region WebSocket Connection

        private async void Connect()
        {
            try
            {
                if (_isShuttingDown) return;
                
                await AcquireEphemeralToken(); // Ensure we have a valid token before connecting
                
                if (string.IsNullOrEmpty(_ephemeralToken))
                {
                    if (logs) MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] No valid token found. Cannot connect.");
                    return;
                }

                // If we're already connected, close first
                if (_websocket is { State: WebSocketState.Open or WebSocketState.Connecting })
                {
                    if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Closing existing socket...");
                    await _websocket.Close();
                }

                // Initialize new WebSocket
                _websocket = new WebSocket(realtimeEndpoint, new Dictionary<string, string>
                {
                    { "Authorization", "Bearer " + _ephemeralToken },
                    { BetaHeaderName, BetaHeaderValue }
                });

                // Subscribe events
                _websocket.OnOpen += OnWebSocketOpen;
                _websocket.OnError += OnWebSocketError;
                _websocket.OnClose += OnWebSocketClose;
                _websocket.OnMessage += OnWebSocketMessage;

                await _websocket.Connect();
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Connecting to " + realtimeEndpoint);
            }
            catch (Exception e)
            {
                if (logs) MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] Connect error: " + e.Message);
            }
        }

        private async Task AcquireEphemeralToken()
        {
            Task t = null;
            AcquireEphemeralTokenImplementation(ref t);
            if (t != null)
                await t;
            if (string.IsNullOrEmpty(_ephemeralToken))
                if (logs) MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] No ephemeral token acquired.");
        }

        partial void AcquireEphemeralTokenImplementation(ref Task t);

        private async void OnWebSocketOpen()
        {
            try
            {
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] WebSocket connected!");

                // Ensure volume is unmuted if it was previously set to 0
                if (outputVoiceSource)
                {
                    outputVoiceSource.volume = 1f;
                }

                // Configure session with text/audio + our tools
                await SendSessionUpdate();

                // Only start the mic if the user setting is true (and GPT isn't already speaking)
                if (micActive && !_isSpeaking)
                {
                    StartMic();
                }
            }
            catch (Exception e)
            {
                if (logs)
                    MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] WebSocket open error: " + e.Message);
            }
        }

        private void OnWebSocketError(string errMsg)
        {
            if (logs) MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] WebSocket error: " + errMsg);

            // Attempt reconnect unless we are shutting down
            if (!_isShuttingDown)
            {
                if (logs)
                    MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Attempting to reconnect in 2s after error...");
                StartCoroutine(TryReconnect());
            }
        }

        private void OnWebSocketClose(WebSocketCloseCode code)
        {
            if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] WebSocket closed: " + code);

            // Stop the audio source to avoid glitchy sound
            if (outputVoiceSource != null)
            {
                outputVoiceSource.Stop();
                outputVoiceSource.volume = 0f;
            }

            // Attempt to reconnect unless we are shutting down
            if (!_isShuttingDown)
            {
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Attempting to reconnect in 2s...");
                StartCoroutine(TryReconnect());
            }
        }

        private IEnumerator TryReconnect()
        {
            // Wait a little before reconnecting
            yield return new WaitForSeconds(2f);

            // Only reconnect if not shutting down
            if (!_isShuttingDown)
            {
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Reconnecting now...");
                Connect();
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
                        ? "No description provided"
                        : f.functionDescription,
                })
                .Cast<object>()
                .ToList();
            
            // For vision capabilities, we add a special tool to handle vision requests.
            toolList.Add(new
            {
                type = "function",
                name = "vision_request",
                description = "If you are processing a user input that requires vision capabilities, " +
                             "this function should be called with a vision_request parameter. " +
                             "You will provide a short prompt describing the output you want from the vision AI.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        vision_request = new
                        {
                            type = "string",
                            description = "A short prompt describing the output that you want from the vision AI. For example 'The user asked what color their shirt.'"
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
            await _websocket.SendText(json);

            if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Sent session update (with tools).");
        }

        #endregion

        #region Microphone Handling

        private void StartMic()
        {
            // If user has mic off or the socket isn't open, we skip
            if (!micActive)
            {
                if (logs) MetaverseProgram.Logger.LogWarning("[GPTRealtimeAudioClient] Mic is disabled by user.");
                return;
            }

            if (_websocket is not { State: WebSocketState.Open })
            {
                if (logs)
                    MetaverseProgram.Logger.LogWarning("[GPTRealtimeAudioClient] Cannot start mic. Socket not open.");
                return;
            }

            _micClip = Microphone.Start(_micDevice, true, 300, micSampleRate);
            _isMicRunning = true;
            _lastMicPos = 0;
            _sampleTimer = 0f;
            if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Microphone started, streaming audio...");
        }

        private void StopMic()
        {
            if (!_isMicRunning) return;

            _isMicRunning = false;
            Microphone.End(_micDevice);
            _micClip = null;

            if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Microphone stopped.");
        }

        private void ProcessAudioFrame()
        {
            if (_micClip == null) return;

            var currentPos = Microphone.GetPosition(_micDevice);
            var samplesToRead = currentPos - _lastMicPos;
            if (samplesToRead < 0)
            {
                // wrapped around
                samplesToRead = _micClip.samples - _lastMicPos;
            }

            if (samplesToRead <= 0) return;

            var samples = new float[samplesToRead];
            _micClip.GetData(samples, _lastMicPos);
            _lastMicPos = currentPos;

            // Send chunk of mic data to GPT
            SendAudioChunk(samples);
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

        private async void SendAudioChunk(float[] samples)
        {
            try
            {
                if (_websocket == null || _websocket.State != WebSocketState.Open) return;

                var pcmBytes = ConvertFloatsToPCM16Bytes(samples);
                var base64Chunk = Convert.ToBase64String(pcmBytes);

                var appendMsg = new
                {
                    type = "input_audio_buffer.append",
                    audio = base64Chunk
                };

                var json = JsonConvert.SerializeObject(appendMsg);
                await _websocket.SendText(json);

                if (logs)
                    MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Sent audio chunk, length: " + base64Chunk.Length);
            }
            catch (Exception e)
            {
                if (logs)
                    MetaverseProgram.Logger.LogError("[GPTRealtimeAudioClient] SendAudioChunk error: " + e.Message);
            }
        }

        #endregion

        #region AI Response Handling

        private void OnWebSocketMessage(byte[] data)
        {
            var rawJson = Encoding.UTF8.GetString(data);
            if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Received: " + rawJson);

            try
            {
                var jObj = JObject.Parse(rawJson);
                var msgType = jObj["type"]?.ToString();

                switch (msgType)
                {
                    case "session.created":
                    case "session.updated":
                        if (logs)
                            MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] " + msgType + " event. " +
                                                        "Session ID: " + jObj["session_id"]);
                        break;

                    case "response.audio.delta":
                        // GPT just started sending audio. Stop the mic so it won't hear itself.
                        if (!_isSpeaking)
                        {
                            _isSpeaking = true;
                            StopMic();
                            if (logs)
                                MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Stopping mic (AI is speaking).");
                        }

                        HandleAudioDelta(jObj);
                        break;

                    case "response.audio_transcript.delta":
                        HandleAudioTranscriptDelta(jObj);
                        break;

                    case "response.done":
                    {
                        // 1) The standard logic: GPT is done streaming audio. We'll eventually resume the mic.
                        if (logs)
                            MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] response.done received. Checking for function calls...");

                        // 2) Check if there's a function_call in response.output array
                        var responseObj = jObj["response"];
                        if (responseObj?["output"] is JArray outputArray)
                        {
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
                                        $"[GPTRealtimeAudioClient] Found function_call '{functionName}' with call_id='{callId}' and arguments={argumentsJson}"
                                    );
                                }
                                
                                if (functionName == "vision_request")
                                {
                                    // Handle vision_request separately if needed
                                    if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Vision request received: " + argumentsJson);
                                    if (string.IsNullOrWhiteSpace(argumentsJson))
                                        continue;
                                    
                                    // Parse vision request.
                                    var visionRequest = JObject.Parse(argumentsJson);
                                    var visionPrompt = visionRequest["vision_request"]?.ToString();
                                    if (!string.IsNullOrWhiteSpace(visionPrompt))
                                    {
                                        VisionHandler.SubmitGameScreenshot(visionPrompt);
                                    }
                                }
                                else
                                {
                                    // Invoke the callback (UnityEvent) matching this function name
                                    TriggerFunctionCall(functionName);
                                }
                    
                                // If you need to do something with argumentsJson, parse it here
                                // If you need to send "function_call_output" back, you do so
                                // after processing arguments.
                            }
                        }

                        // 3) The normal “done” logic to resume mic once audio buffer empties
                        //    (this was probably your existing code)
                        if (logs)
                            MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Will resume mic once buffer empties...");
                        StartCoroutine(ResumeMicAfterPlayback());
                        break;
                    }

                    /*
                     * Detect if the model is calling a custom function we defined in session.tools.
                     *  Example server JSON:
                     *  {
                     *    "type": "function_call",
                     *    "event_id": "...",
                     *    "function": {
                     *      "name": "generate_horoscope"
                     *    },
                     *    "arguments": "{\"sign\":\"Aquarius\"}",
                     *    "call_id": "call_sHlR7iaFwQ2YQOqm"
                     *  }
                     */
                    case "function_call":
                    {
                        // The GPT model decided to call one of our tools
                        var functionName = jObj["function"]?["name"]?.ToString();
                        if (!string.IsNullOrEmpty(functionName))
                        {
                            TriggerFunctionCall(functionName);
                            // If you need to pass function call OUTPUT back to the model, you'll
                            // parse "arguments" and do your custom logic. Then you'd do something like:
                            // conversation.item.create => function_call_output => ...
                        }
                        break;
                    }

                    case "error":
                    {
                        var eCode = jObj["error"]?["code"]?.ToString();
                        var eMsg = jObj["error"]?["message"]?.ToString();
                        if (logs)
                            MetaverseProgram.Logger.LogWarning("[GPTRealtimeAudioClient] Error code=" + eCode + ", message=" + eMsg);
                        break;
                    }

                    default:
                        if (logs)
                            MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Unhandled message type: " + msgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (logs)
                    MetaverseProgram.Logger.LogWarning("[GPTRealtimeAudioClient] JSON parse error: " + ex.Message);
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

            lock (_bufferLock)
            {
                foreach (var s in resampledSamples)
                {
                    _streamBuffer.Enqueue(s);
                }
            }
        }

        private void HandleAudioTranscriptDelta(JObject jObj)
        {
            var delta = jObj["delta"]?.ToString();
            if (!string.IsNullOrEmpty(delta))
            {
                _transcriptText += delta;
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Transcript: " + _transcriptText);
            }
        }

        #endregion

        #region Function Calls

        /// <summary>
        /// Looks up the function by name and invokes its UnityEvent (if found).
        /// You can expand this to parse arguments as well.
        /// </summary>
        private void TriggerFunctionCall(string functionID)
        {
            var fn = availableFunctions.FirstOrDefault(f => f.functionID == functionID);
            if (fn != null)
            {
                if (logs) MetaverseProgram.Logger.Log($"[GPTRealtimeAudioClient] Invoking function '{functionID}'.");
                fn.onCalled?.Invoke();
            }
            else
            {
                if (logs)
                    MetaverseProgram.Logger.LogWarning($"[GPTRealtimeAudioClient] No function found with ID='{functionID}'.");
            }
        }

        #endregion

        #region Playback + Utility

        private IEnumerator ResumeMicAfterPlayback()
        {
            // Keep waiting until our buffer is fully played out
            while (true)
            {
                lock (_bufferLock)
                {
                    if (_streamBuffer.Count == 0) break;
                }
                yield return null; // keep waiting
            }

            _isSpeaking = false;

            // Only restart the mic if user has not disabled it
            if (micActive)
            {
                StartMic();
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Mic resumed.");
            }
            else
            {
                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Mic is still disabled by user.");
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_bufferLock)
            {
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] = (_streamBuffer.Count > 0) ? _streamBuffer.Dequeue() : 0f;
                }
            }
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

        #endregion
        
        #region Vision

        private void OnVisionResponse(string visionResponse)
        {
            UniTask.Void(async () =>
            {
                if (string.IsNullOrWhiteSpace(visionResponse))
                {
                    if (logs) MetaverseProgram.Logger.LogWarning("[GPTRealtimeAudioClient] Vision response is empty.");
                    OnVisionResponseFailed();
                    return;
                }

                if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Vision response received: " + visionResponse);
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
                if (_websocket is { State: WebSocketState.Open })
                {
                    await _websocket.SendText(json);
                    if (logs) MetaverseProgram.Logger.Log("[GPTRealtimeAudioClient] Sent vision response to GPT.");
                    
                    // Trigger a response from GPT to process the vision response
                    /*
                     * const event = {
                         type: "response.create",
                         response: {
                           modalities: [ "text", "audio" ]
                         },
                       };
                     */

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
                    if (logs) MetaverseProgram.Logger.LogWarning("[GPTRealtimeAudioClient] WebSocket not open. Cannot send vision response.");
                }
            });
        }
        
        private void OnVisionResponseFailed()
        {
            OnVisionResponse("I'm sorry, I couldn't process the vision request.");
        }
        
        #endregion
    }
}
#endif