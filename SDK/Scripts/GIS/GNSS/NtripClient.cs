#if METAVERSE_CLOUD_ENGINE && METAVERSE_CLOUD_ENGINE_INITIALIZED // <GENERATED>
#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using MetaverseCloudEngine.Unity.Async;
using MetaverseCloudEngine.Unity.Attributes;
using TriInspectorMVCE;
using UnityEngine;
using UnityEngine.Events;

// ReSharper disable once CheckNamespace
namespace MetaverseCloudEngine.Unity.GIS.GNSS
{
    [AddComponentMenu(MetaverseConstants.ProductName + "/GIS/GNSS/NTRIP Client")]
    [HideMonoScript]
    public class NtripClient : TriInspectorMonoBehaviour
    {
        #region Serialized Fields

        [Header("NTRIP Server Settings")]
        [Tooltip("NTRIP Caster host address.")]
        [SerializeField]
        private string host = "ntrip.myfloridagps.com";
        [SerializeField]
        [Min(0)]
        private int port = 10000;
        [Tooltip("Mountpoint (case‑sensitive).")]
        [Required]
        [SerializeField]
        private string mountPoint = "RTCM3_VRS";

        [Header("Credentials")]
        [SerializeField]
        [Required]
        private string username = "";
        [ProtectedField]
        [SerializeField]
        [Required]
        private string password = "";

        [Header("Auto‑Reconnect")]
        [SerializeField]
        private bool connectOnStart = true;
        [SerializeField]
        [Tooltip("Maximum seconds between retries – exponential back‑off")]
        [Min(1f)]
        private float maxBackoff = 60f;

        [Header("Debug Options")]
        [SerializeField]
        private bool enableLogs;
#if UNITY_EDITOR && METAVERSE_CLOUD_ENGINE_INTERNAL
        [DisableInPlayMode]
        [SerializeField]
        private bool debugMode;
        [SerializeField]
        [Tooltip("Interval (seconds) to send GGA message if Debug Mode is enabled.")]
        [Min(2f)]
        private float debugGgaInterval = 5.0f;
#endif

        #endregion

        #region Public Events

        [Header("Events")]
        [Tooltip("Event invoked when RTCM correction data is received from the NTRIP caster. " +
                 "Subscribe with your GNSS receiver handler.")]
        [SerializeField] private UnityEvent<byte[]> onReceiveCorrections = new();
        [Tooltip("Event invoked when the client successfully connects and authenticates with the NTRIP caster.")]
        [SerializeField] private  UnityEvent onConnected = new();
        [Tooltip("Event invoked when the client disconnects (either intentionally or due to an error).")]
        [SerializeField] private  UnityEvent onDisconnected = new();

        #endregion

        #region Constants

        // Template for the GPGGA sentence body (content between $ and *)
        // {0}: UTC Time (HHMMSS.ss)
        // Fields based on St. Petersburg: Lat 2746.3200 N, Lon 08238.4000 W, Fix 1, Sats 09, HDOP 1.1, Alt 5.0M, Geoid 0.0 M
        private const string GpggaTemplateBody =
            "GPGGA,{0},2746.3200,N,08238.4000,W,1,09,1.1,5.0,M,0.0,M,,";
        private const int HeaderReadTimeoutSeconds = 10;
        private const int DataReadTimeoutSeconds = 20; // Timeout for waiting for correction data
        private const int ConnectTimeoutSeconds = 10; // Timeout for ConnectAsync overload used

        #endregion

        #region Private Members

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private float _reconnectDelay = 2f;
        private float _lastReconnectDelay;
        private bool _needsReconnect;
        private volatile bool _shuttingDown; // Use volatile for thread safety visibility
        private Task _debugGgaSenderTask; // Keep track of the GGA sender task
        private Task _ggaSenderTask; // Task for sending GGA messages in debug mode
        private readonly ConcurrentQueue<string> _gnssInputDataQueue = new(); // Queue for GNSS data

        #endregion

        #region Public Properties
        
        /// <summary>
        /// The event invoked when RTCM correction data is received from the NTRIP caster.
        /// </summary>
        public UnityEvent <byte[]> OnReceiveCorrections => onReceiveCorrections;
        /// <summary>
        /// The event invoked when the client successfully connects and authenticates with the NTRIP caster.
        /// </summary>
        public UnityEvent OnConnected => onConnected;
        /// <summary>
        /// The event invoked when the client disconnects (either intentionally or due to an error).
        /// </summary>
        public UnityEvent OnDisconnected => onDisconnected;

        /// <summary>
        /// The NTRIP caster host address.
        /// </summary>
        public string Host
        {
            get => host;
            set
            {
                if (value == host) return;
                host = value;
                if (_client is not { Connected: true }) return;
                Log($"[NTRIP] Host changed to {host}. Reconnecting...");
                Reconnect();
            }
        }
        
        /// <summary>
        /// The NTRIP caster port.
        /// </summary>
        public int Port
        {
            get => port;
            set
            {
                if (value == port) return;
                port = value;
                if (_client is not { Connected: true }) return;
                Log($"[NTRIP] Port changed to {port}. Reconnecting...");
                Reconnect();
            }
        }
        
        /// <summary>
        /// The mountpoint (case‑sensitive).
        /// </summary>
        public string MountPoint
        {
            get => mountPoint;
            set
            {
                if (value == mountPoint) return;
                mountPoint = value;
                if (_client is not { Connected: true }) return;
                Log($"[NTRIP] MountPoint changed to {mountPoint}. Reconnecting...");
                Reconnect();
            }
        }
        
        /// <summary>
        /// The username for authentication.
        /// </summary>
        public string Username
        {
            get => username;
            set
            {
                if (value == username) return;
                username = value;
                if (_client is not { Connected: true }) return;
                Log("[NTRIP] Username changed. Reconnecting...");
                Reconnect();
            }
        }
        
        /// <summary>
        /// The password for authentication.
        /// </summary>
        public string Password
        {
            get => password;
            set
            {
                if (value == password) return;
                password = value;
                if (_client is not { Connected: true }) return;
                Log("[NTRIP] Password changed. Reconnecting...");
                Reconnect();
            }
        }

        /// <summary>
        /// Indicates whether the client is currently connected to the NTRIP caster.
        /// </summary>
        public bool IsConnected => _client is { Connected: true };
        
        /// <summary>
        /// Indicates whether the client is currently shutting down.
        /// </summary>
        public bool IsShuttingDown => _shuttingDown;

        #endregion

        #region Unity Events

        private void Start()
        {
            if (connectOnStart)
                Connect();
        }

        private void Update()
        {
            // Handle reconnection scheduling on the main thread
            if (!_needsReconnect || _shuttingDown) return;

            _reconnectDelay -= Time.deltaTime;
            if (_reconnectDelay > 0) return;

            Log("[NTRIP] Reconnect delay expired. Initiating reconnect attempt."); // Add log
            _needsReconnect = false; // Reset a flag *before* calling Connect
            _lastReconnectDelay =
                _reconnectDelay; // Store the delay that just expired for backoff calculation next time
            Connect(); // Attempt reconnection
        }

        private void OnDisable()
        {
            Log("[NTRIP] OnDisable called.");
            _shuttingDown = true;
            Disconnect(); // Ensure cleanup when a component is disabled or destroyed
        }

        private void OnApplicationQuit()
        {
            Log("[NTRIP] OnApplicationQuit called.");
            _shuttingDown = true;
            Disconnect(); // Ensure cleanup on application quit
        }

        #endregion

        #region Public API

        public void Reconnect()
        {
            CleanupSocketResources();
            ScheduleReconnect();
        }

        /// <summary>
        /// Initiates connection to the NTRIP caster.
        /// </summary>
        public void Connect()
        {
            if (_shuttingDown)
            {
                Log("[NTRIP] Connect called but shutting down.");
                return;
            }

            if (_client is { Connected: true })
            {
                Log("[NTRIP] Connect called but already connected.");
                return;
            }

            CleanupSocketResources();

            _cts = new CancellationTokenSource();
            Log($"[NTRIP] Starting connection attempt to {host}:{port}/{mountPoint}");
            TcpReaderLoop(_cts.Token).Forget(ex =>
            {
                LogError(
                    $"[NTRIP] Unhandled exception reaching Forget() in Connect(): {ex}");
            });
        }

        /// <summary>
        /// Disconnects from the NTRIP caster and cleans up resources.
        /// </summary>
        public void Disconnect()
        {
            Log($"[NTRIP] Disconnect requested. Shutting down flag: {_shuttingDown}");
            var previouslyShuttingDown = _shuttingDown;
            _shuttingDown = true; // Mark the connection process as stopping

            CleanupSocketResources(); // Cancel CTS, close sockets etc.

            if (Application.isPlaying && !previouslyShuttingDown)
            {
                try
                {
                    onDisconnected?.Invoke();
                }
                catch (Exception ex)
                {
                    LogError($"[NTRIP] Exception in onDisconnected subscriber: {ex}");
                }
            }
            // Note: _shuttingDown remains true if set by OnDisable/OnApplicationQuit
        }
        
        /// <summary>
        /// Inputs GNSS data (e.g., RTCM sentences) into the client.
        /// </summary>
        /// <param name="sentence">The GNSS data sentence to input.</param>
        [UsedImplicitly]
        public void InputGnssData(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return;
            
            if (!sentence.StartsWith("$"))
            {
                LogWarning(
                    @"[NTRIP] InputGnssData called with invalid sentence format. Must start with '$' and end with '\r\n'. Ignoring input.");
                return;
            }
            
            if (!IsConnected || IsShuttingDown)
            {
                LogWarning(
                    "[NTRIP] InputGnssData called but not connected or shutting down. Ignoring input.");
                return;
            }
            
            _gnssInputDataQueue.Enqueue(sentence);

#if UNITY_EDITOR && METAVERSE_CLOUD_ENGINE_INTERNAL
            if (debugMode)
            {
                MetaverseProgram.Logger.Log("[NTRIP] Automatically disabling debug mode after inputting GNSS data.");
                debugMode = false; // Disable debug mode after inputting GNSS data
            }
#endif
        }

        #endregion

        #region NTRIP Logic

        /// <summary>
        /// Cleans up TCP client, network stream, and cancellation token source.
        /// </summary>
        private void CleanupSocketResources()
        {
            Log("[NTRIP] Cleaning up socket resources...");
            if (_cts is { IsCancellationRequested: false })
            {
                try
                {
                    Log("[NTRIP] Cancelling CancellationTokenSource...");
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    /* ignored */
                }
                catch (Exception ex)
                {
                    LogWarning(
                        $"[NTRIP] Exception cancelling CancellationTokenSource: {ex.Message}");
                }
            }

            if (_debugGgaSenderTask != null)
            {
                Log("[NTRIP] Clearing Debug GGA sender task reference.");
                _debugGgaSenderTask = null;
            }
            
            if (_ggaSenderTask != null)
            {
                Log("[NTRIP] Clearing GGA sender task reference.");
                _ggaSenderTask = null;
            }
            
            _gnssInputDataQueue.Clear();

            var streamToClose = _stream;
            _stream = null;
            if (streamToClose != null)
            {
                try
                {
                    Log("[NTRIP] Closing NetworkStream...");
                    streamToClose.Close();
                }
                catch (Exception ex)
                {
                    LogWarning($"[NTRIP] Exception closing NetworkStream: {ex.Message}");
                }
            }

            var clientToClose = _client;
            _client = null;
            if (clientToClose != null)
            {
                try
                {
                    Log("[NTRIP] Closing TcpClient...");
                    clientToClose.Close();
                }
                catch (Exception ex)
                {
                    LogWarning($"[NTRIP] Exception closing TcpClient: {ex.Message}");
                }
            }

            var ctsToDispose = _cts;
            _cts = null;
            if (ctsToDispose != null)
            {
                try
                {
                    ctsToDispose.Dispose();
                }
                catch (Exception ex)
                {
                    LogWarning(
                        $"[NTRIP] Exception disposing CancellationTokenSource: {ex.Message}");
                }
            }

            _needsReconnect = false; // Stop needing reconnecting after cleanup
            Log("[NTRIP] Socket resources cleaned up.");
        }

        /// <summary>
        /// Main asynchronous loop for connecting, handling HTTP request/response,
        /// sending GGA (if debug), and reading RTCM data.
        /// </summary>
        [SuppressMessage("ReSharper", "PossiblyMistakenUseOfCancellationToken")]
        private async UniTask TcpReaderLoop(CancellationToken token)
        {
            CancellationTokenSource ggaSenderCts = null;
            var connectionSuccessful = false; // Track if we got past the handshake

            try
            {
                // --- Connection Phase ---
                await UniTask.SwitchToThreadPool();
                Log($"[NTRIP] TCP connecting to {host}:{port}...");
                _client = new TcpClient();

                // Attempt connection - User version uses overload without explicit token/timeout support here
                try
                {
                    // Add a timeout manually using UniTask.Timeout if needed
                    await _client.ConnectAsync(host, port);
                }
                catch (TimeoutException)
                {
                    LogWarning(
                        $"[NTRIP] Connection attempt timed out after {ConnectTimeoutSeconds} seconds to {host}:{port}.");
                    // Need to ensure the potentially partially opened client is cleaned up
                    _client?.Close(); // Close the client
                    _client = null;
                    throw new OperationCanceledException(); // Throw cancellation to signal failure to connect
                }
                catch (SocketException ex)
                {
                    LogWarning(
                        $"[NTRIP] Socket exception during connection: {ex.Message} (SocketError: {ex.SocketErrorCode})");
                    throw; // Rethrow to trigger reconnect scheduling
                }
                catch (OperationCanceledException)
                {
                    // Catch if 'token' was canceled during connecting
                    Log("[NTRIP] Connection attempt cancelled externally.");
                    throw; // Rethrow cancellation
                }

                // Check token cancellation *after* potential connect timeout/exception
                token.ThrowIfCancellationRequested();

                if (!_client.Connected)
                {
                    // Should not happen if ConnectAsync succeeded, but check anyway
                    LogWarning(
                        "[NTRIP] Connection failed (client not connected after ConnectAsync).");
                    return; // Let's finally handle cleanup
                }

                _stream = _client.GetStream();
                _stream.ReadTimeout = DataReadTimeoutSeconds * 1000;
                Log("[NTRIP] TCP connection established. Sending NTRIP request...");

                // --- NTRIP Request Phase ---
                var sb = new StringBuilder();
                sb.Append($"GET /{mountPoint} HTTP/1.1\r\n");
                sb.Append($"Host: {host}:{port}\r\n");
                sb.Append("NTRIP-Version: Ntrip/2.0\r\n");
                sb.Append($"User-Agent: {MetaverseConstants.ProductName.Replace(" ", "_")}-UnityClient/{Application.version}\r\n");
                sb.Append("Connection: keep-alive\r\n");
                sb.Append("Accept: */*\r\n");
                if (!string.IsNullOrEmpty(username))
                {
                    var credentials = $"{username}:{password}";
                    var tokenB64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                    sb.Append($"Authorization: Basic {tokenB64}\r\n");
                    Log("[NTRIP] Including Basic Authentication header.");
                }

                sb.Append("\r\n");
                var requestBytes = Encoding.ASCII.GetBytes(sb.ToString());
                await _stream.WriteAsync(requestBytes, 0, requestBytes.Length, token);
                await _stream.FlushAsync(token);
                Log(
                    $"[NTRIP] NTRIP request sent for mountpoint '{mountPoint}'. Waiting for response header...");

                // --- NTRIP Response Header Reading Phase ---
                var headerBuffer = new byte[2048];
                var headerLen = 0;
                var headerString = new StringBuilder();
                var headerStartTime = DateTime.UtcNow;
                var headerReceived = false;
                const string endOfHeaderMarker = "\r\n\r\n";
                while (!token.IsCancellationRequested &&
                       (DateTime.UtcNow - headerStartTime).TotalSeconds < HeaderReadTimeoutSeconds)
                {
                    if (_stream is not { CanRead: true })
                        throw new ObjectDisposedException("NetworkStream closed prematurely while reading header.");
                    int bytesRead;
                    try
                    {
                        if (_client.Available > 0)
                        {
                            bytesRead = await _stream.ReadAsync(headerBuffer, headerLen,
                                headerBuffer.Length - headerLen, token);
                        }
                        else
                        {
                            await UniTask.Delay(20, DelayType.Realtime, PlayerLoopTiming.Update, token);
                            continue;
                        }
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException
                                                 {
                                                     SocketErrorCode: SocketError.TimedOut
                                                 })
                    {
                        LogWarning(
                            "[NTRIP] Read timeout while waiting for header data byte. Still trying...");
                        await UniTask.Delay(100, cancellationToken: token);
                        continue;
                    }
                    catch (ObjectDisposedException)
                    {
                        LogWarning("[NTRIP] Stream disposed while reading header.");
                        throw;
                    }

                    if (bytesRead == 0)
                    {
                        LogWarning(
                            "[NTRIP] Connection closed by server while reading response header (0 bytes received).");
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }

                    headerString.Append(Encoding.ASCII.GetString(headerBuffer, headerLen, bytesRead));
                    headerLen += bytesRead;
                    if (headerString.ToString().Contains(endOfHeaderMarker))
                    {
                        headerReceived = true;
                        break;
                    }
                }

                token.ThrowIfCancellationRequested();
                if (!headerReceived)
                {
                    LogWarning(
                        $"[NTRIP] Failed to receive complete header within {HeaderReadTimeoutSeconds} seconds. Header so far:\n---\n{headerString}\n---");
                    throw new TimeoutException("NTRIP header timeout.");
                }

                var fullHeader = headerString.ToString();
                var headerEndIndex = fullHeader.IndexOf(endOfHeaderMarker, StringComparison.Ordinal);
                var headerToLog = headerEndIndex >= 0 ? fullHeader.Substring(0, headerEndIndex) : fullHeader;
                if (headerToLog.Length > 512) headerToLog = headerToLog.Substring(0, 512) + "... (trimmed)";
                Log($"[NTRIP] Received Header:\n---\n{headerToLog.TrimEnd()}\n---");
                var headerLines = fullHeader.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var statusLine = headerLines.Length > 0 ? headerLines[0] : string.Empty;
                if (statusLine.StartsWith("SOURCE "))
                {
                    LogWarning(
                        $"[NTRIP] Received SOURCE table directly. Ensure mountpoint '{mountPoint}' is correct.");
                    throw new Exception(
                        $"NTRIP Caster returned SOURCE table instead of mountpoint connection. Status: {statusLine}");
                }

                if (!statusLine.StartsWith("ICY 200 OK") && !statusLine.Contains(" 200 OK"))
                {
                    LogWarning(
                        $"[NTRIP] Server returned non-OK status. Check credentials, mountpoint ({mountPoint}), and server status.");
                    if (statusLine.Contains(" 401"))
                    {
                        LogError("[NTRIP] Authentication Failed (401 Unauthorized).");
                    }
                    else if (statusLine.Contains(" 403"))
                    {
                        LogError(
                            $"[NTRIP] Access Forbidden (403) for mountpoint '{mountPoint}'.");
                    }
                    else if (statusLine.Contains(" 404"))
                    {
                        LogError($"[NTRIP] Mountpoint '{mountPoint}' Not Found (404).");
                    }

                    throw new Exception($"NTRIP Caster returned non-OK status. Status line: {statusLine}");
                }

                Log(
                    "[NTRIP] Connection successful and header OK. Ready to receive corrections.");
                connectionSuccessful = true;

                // --- Switch to Main Thread for Connected Event ---
                await UniTask.SwitchToMainThread(PlayerLoopTiming.Update, token);
                onConnected?.Invoke();
                await UniTask.SwitchToThreadPool();

                ggaSenderCts = CancellationTokenSource.CreateLinkedTokenSource(token);

#if UNITY_EDITOR && METAVERSE_CLOUD_ENGINE_INTERNAL
                // --- Start Debug GGA Sender (if enabled) ---
                if (debugMode)
                {
                    _debugGgaSenderTask =
                        Task.Run(() => SendDebugGgaLoopAsync(ggaSenderCts.Token),
                            ggaSenderCts.Token); // Use linked token
                    Log(
                        "[NTRIP] Debug Mode enabled: Starting periodic dynamic GGA sender."); // Modified log
                }
#endif
                
                _ggaSenderTask =
                    Task.Run(() => SendGgaInputLoopAsync(ggaSenderCts.Token), ggaSenderCts.Token); // Start sending GGA immediately if debug mode

                // --- RTCM Data Reading Loop ---
                var dataBuffer = new byte[4096];
                while (!token.IsCancellationRequested)
                {
                    if (_stream is not { CanRead: true })
                    {
                        LogWarning(
                            "[NTRIP] NetworkStream became null or unreadable in data loop.");
                        throw new ObjectDisposedException("NetworkStream closed unexpectedly.");
                    }

                    Log("[NTRIP] Waiting to read correction data...");
                    int len;
                    try
                    {
                        len = await _stream.ReadAsync(dataBuffer, 0, dataBuffer.Length, token);
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException
                                                 {
                                                     SocketErrorCode: SocketError.TimedOut
                                                 })
                    {
#if UNITY_EDITOR && METAVERSE_CLOUD_ENGINE_INTERNAL
                        Log(
                            $"[NTRIP] No data received for {DataReadTimeoutSeconds} seconds (Read Timeout). Still connected, continuing wait...");
                        if (debugMode && _debugGgaSenderTask is { IsCompleted: false })
                        {
                            Log("[NTRIP] Read timed out, sending another debug GGA.");
                            SendGgaAsync(null).Forget(); // Pass null to trigger dynamic generation // MODIFIED
                        }
#endif

                        continue;
                    }
                    catch (ObjectDisposedException)
                    {
                        LogWarning("[NTRIP] Stream disposed while reading data.");
                        if (!_shuttingDown) throw;
                        break;
                    }
                    catch (IOException ex) when (ex.InnerException is SocketException sockEx &&
                                                 (sockEx.SocketErrorCode == SocketError.ConnectionReset ||
                                                  sockEx.SocketErrorCode == SocketError.ConnectionAborted ||
                                                  sockEx.SocketErrorCode == SocketError.Shutdown))
                    {
                        LogWarning(
                            $"[NTRIP] Socket connection lost while reading data ({sockEx.SocketErrorCode}).");
                        throw;
                    }

                    Log($"[NTRIP] ReadAsync returned with length: {len}");
                    if (len == 0)
                    {
                        LogWarning(
                            "[NTRIP] ReadAsync returned 0 bytes after successful connection. Server closed stream gracefully.");
                        throw new SocketException((int)SocketError.Shutdown);
                    }

                    // --- Process Received Data ---
                    var payload = new byte[len];
                    Array.Copy(dataBuffer, 0, payload, 0, len);
                    Log($"[NTRIP] [DEBUG] Received {len} bytes.");

                    // --- Switch to Main Thread to Invoke UnityEvent ---
                    await UniTask.SwitchToMainThread(PlayerLoopTiming.Update, token);
                    try
                    {
                        onReceiveCorrections?.Invoke(payload);
                    }
                    catch (Exception ex)
                    {
                        LogError($"[NTRIP] Exception in onReceiveCorrections subscriber: {ex}");
                    }

                    await UniTask.SwitchToThreadPool();
                }
            }
            catch (OperationCanceledException)
            {
                // From token or connect timeout
                Log(
                    $"[NTRIP] Operation cancelled. Successful connection prior: {connectionSuccessful}");
            }
            catch (Exception ex)
            {
                if (!_shuttingDown)
                {
                    LogError(
                        $"[NTRIP] Exception in TCP Loop: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                    await UniTask.SwitchToMainThread(); // Switch ONCE
                    if (connectionSuccessful)
                    {
                        // Only invoke disconnect if we were ever connected
                        try
                        {
                            onDisconnected?.Invoke();
                        }
                        catch (Exception uex)
                        {
                            LogError($"[NTRIP] Exception in onDisconnected subscriber: {uex}");
                        }
                    }

                    ScheduleReconnect(); // CALL SCHEDULER HERE, ON MAIN THREAD (via Dispatcher)
                }
                else
                {
                    Log(
                        $"[NTRIP] Exception caught during shutdown, suppressing reconnect. Exception: {ex.GetType().Name}");
                }
            }
            finally
            {
                Log("[NTRIP] Entering TCP Reader Loop finally block.");
                // --- Cleanup ---
                if (ggaSenderCts is { IsCancellationRequested: false })
                {
                    try
                    {
                        Log("[NTRIP] Requesting GGA sender cancellation...");
                        ggaSenderCts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (Exception cex)
                    {
                        LogWarning($"[NTRIP] Exception cancelling GGA CTS: {cex.Message}");
                    }
                }

                CleanupSocketResources(); // General cleanup

                if (token.IsCancellationRequested && !_shuttingDown && !_needsReconnect)
                {
                    Log(
                        "[NTRIP] Loop exited via cancellation but not shutting down or reconnecting yet, scheduling reconnect as safeguard.");
                    // Ensure scheduling happens on the main thread via dispatcher
                    ScheduleReconnect();
                }
                else if (_shuttingDown)
                {
                    Log("[NTRIP] TCP Reader Loop finished during shutdown process.");
                }

                Log("[NTRIP] Exiting TCP Reader Loop finally block.");
            }
        }

        private Task SendGgaInputLoopAsync(CancellationToken token)
        {
            Log("[NTRIP] Starting GGA input loop task.");
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (_gnssInputDataQueue.TryDequeue(out var ggaSentence))
                    {
                        try
                        {
                            await SendGgaAsync(ggaSentence);
                        }
                        catch (Exception ex)
                        {
                            LogError($"[NTRIP] Error sending GGA input: {ex.Message}");
                        }
                    }
                    else
                    {
                        // No data to send, wait a bit before checking again
                        await Task.Delay(100, token);
                    }
                }
            }, token);
        }

#if UNITY_EDITOR && METAVERSE_CLOUD_ENGINE_INTERNAL
        /// <summary>
        /// Asynchronous loop to periodically send a dynamically generated debug GPGGA sentence.
        /// Runs as a separate Task using Task.Run.
        /// </summary>
        private async Task SendDebugGgaLoopAsync(CancellationToken token)
        {
            Log("[NTRIP] [DEBUG] Dynamic GPGGA sender task started.");
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), token); // Initial delay

                while (!token.IsCancellationRequested && debugMode)
                {
                    var currentStream = _stream; // Capture locally
                    var currentClient = _client;

                    if (currentStream is not { CanWrite: true } || currentClient is not { Connected: true })
                    {
                        LogWarning(
                            "[NTRIP] [DEBUG] Cannot send dynamic GGA: Stream not available. Stopping sender.");
                        break;
                    }

                    try
                    {
                        // 1. Get current time and format it
                        var nowUtc = DateTime.UtcNow;
                        var timeStr = nowUtc.ToString("HHmmss.ff"); // Format: HHMMSS.ss (two decimal places)

                        // 2. Construct the sentence body using the template
                        var sentenceBody = string.Format(GpggaTemplateBody, timeStr);

                        // 3. Calculate the checksum for the body
                        var checksumStr = CalculateNmeaChecksum(sentenceBody);

                        // 4. Assemble the full NMEA sentence
                        var fullSentence = $"${sentenceBody}*{checksumStr}\r\n";

                        // 5. Convert to bytes (ASCII is standard for NMEA)
                        var ggaBytes = Encoding.ASCII.GetBytes(fullSentence);

                        // 6. Send the data
                        await currentStream.WriteAsync(ggaBytes, 0, ggaBytes.Length, token);
                        // await currentStream.FlushAsync(token); // Usually not needed after WriteAsync

                        // Log what was actually sent for verification
                        Log($"[NTRIP] [DEBUG] Sent Dynamic GGA: {fullSentence.Trim()}");
                    }
                    catch (ObjectDisposedException)
                    {
                        LogWarning(
                            "[NTRIP] [DEBUG] Stream disposed while trying to send dynamic GGA. Stopping sender.");
                        break;
                    }
                    catch (IOException ioex)
                    {
                        LogWarning(
                            $"[NTRIP] [DEBUG] IO Error sending dynamic GGA: {ioex.Message}. Continuing...");
                        if (ioex.InnerException is SocketException se &&
                            (se.SocketErrorCode == SocketError.ConnectionReset ||
                             se.SocketErrorCode == SocketError.ConnectionAborted))
                        {
                            LogError(
                                "[NTRIP] [DEBUG] Socket connection lost during dynamic GGA send. Stopping sender.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(
                            $"[NTRIP] [DEBUG] Unexpected Error sending dynamic GGA: {ex.Message}");
                        break; // Stop on unexpected errors
                    }

                    // 7. Wait for the interval
                    await Task.Delay(TimeSpan.FromSeconds(debugGgaInterval), token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("[NTRIP] [DEBUG] Dynamic GGA sender task cancelled.");
            }
            catch (Exception ex)
            {
                LogError($"[NTRIP] [DEBUG] Exception in Dynamic GGA sender task: {ex}");
            }
            finally
            {
                Log("[NTRIP] [DEBUG] Dynamic GGA sender task finished.");
            }
        }
#endif

        /// <summary>
        /// Schedules a reconnection attempt with exponential backoff.
        /// Uses MetaverseDispatcher to ensure execution on the main thread.
        /// </summary>
        private void ScheduleReconnect()
        {
            // Ensure execution on the main thread using the dispatcher
            MetaverseDispatcher.AtEndOfFrame(() => // Changed from AtEndOfFrame for potentially quicker scheduling
            {
                // Assert execution context just in case the dispatcher logic changes
                if (_shuttingDown || _needsReconnect) // Don't schedule if already shutting down or reconnect pending
                {
                    Log(
                        $"[NTRIP] Reconnect scheduling skipped (Shutting Down: {_shuttingDown}, Needs Reconnect: {_needsReconnect})");
                    return;
                }

                _needsReconnect = true;
                _reconnectDelay = Mathf.Clamp(_lastReconnectDelay > 1f ? _lastReconnectDelay * 1.5f : 2f, 2f,
                    maxBackoff);
                Log($"[NTRIP] Reconnect scheduled in {_reconnectDelay:F1}s");
            });
        }

        /// <summary>
        /// Public method to send a GNGGA/GPGGA sentence to the NTRIP caster.
        /// If ggaSentence is null and debugMode is true, sends a dynamically generated one.
        /// Otherwise, sends the provided sentence. Required by some casters (especially VRS).
        /// </summary>
        /// <param name="ggaSentence">The full NMEA sentence string (including $, checksum, \r\n), or null to trigger dynamic debug GGA.</param>
        public async UniTask SendGgaAsync(string ggaSentence)
        {
            var sentenceToSend = ggaSentence;

#if UNITY_EDITOR && METAVERSE_CLOUD_ENGINE_INTERNAL
            // If in debug mode and no sentence provided, generate one dynamically
            if (ggaSentence == null && debugMode)
            {
                try
                {
                    var nowUtc = DateTime.UtcNow;
                    var timeStr = nowUtc.ToString("HHmmss.ff");
                    var sentenceBody = string.Format(GpggaTemplateBody, timeStr);
                    var checksumStr = CalculateNmeaChecksum(sentenceBody);
                    sentenceToSend = $"${sentenceBody}*{checksumStr}\r\n";
                    Log(
                        $"[NTRIP] [DEBUG] SendGgaAsync generating dynamic GGA: {sentenceToSend.Trim()}");
                }
                catch (Exception ex)
                {
                    LogError($"[NTRIP] [DEBUG] Error generating dynamic GGA: {ex.Message}");
                    return; // Don't proceed if generation failed
                }
            }
#endif

            // Validate the sentence we intend to send
            if (string.IsNullOrEmpty(sentenceToSend) ||
                !(sentenceToSend.StartsWith("$GPGGA") ||
                  sentenceToSend.StartsWith("$GNGGA")) || // Allow both common types
                !sentenceToSend.Contains("*") ||
                !sentenceToSend.EndsWith("\r\n"))
            {
                LogWarning(
                    $"[NTRIP] Invalid or missing GGA sentence format for sending: {sentenceToSend ?? "NULL"}");
                return;
            }

            var currentStream = _stream;
            var currentClient = _client;
            if (currentStream is not { CanWrite: true } || currentClient is not { Connected: true })
            {
                LogWarning("[NTRIP] Cannot send GGA: Not connected or stream not writable.");
                return;
            }

            try
            {
                await UniTask.SwitchToThreadPool();
                var ggaBytes = Encoding.ASCII.GetBytes(sentenceToSend);
                await currentStream.WriteAsync(ggaBytes, 0, ggaBytes.Length,
                    CancellationToken.None); // Use appropriate token if needed
                Log($"[NTRIP] Sent GGA: {sentenceToSend.Trim()}");
            }
            catch (ObjectDisposedException)
            {
                LogWarning("[NTRIP] Stream disposed while trying to send GGA.");
            }
            catch (IOException ioex)
            {
                LogError($"[NTRIP] IO Error sending GGA: {ioex.Message}");
                if (ioex.InnerException is SocketException se && (se.SocketErrorCode == SocketError.ConnectionReset ||
                                                                  se.SocketErrorCode == SocketError.ConnectionAborted))
                {
                    // Schedule reconnect on the main thread
                    ScheduleReconnect();
                }
            }
            catch (Exception ex)
            {
                LogError($"[NTRIP] Unexpected Error sending GGA: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates the NMEA checksum for a sentence body (content between $ and *).
        /// </summary>
        /// <param name="sentenceWithoutChecksum">The sentence string excluding '$' and '*'.</param>
        /// <returns>A two-character uppercase hexadecimal checksum string.</returns>
        private static string CalculateNmeaChecksum(string sentenceWithoutChecksum)
        {
            var checksum =
                sentenceWithoutChecksum.Aggregate<char, byte>(0,
                    (current, character) => (byte)(current ^ (byte)character));
            return checksum.ToString("X2"); // Returns uppercase hex, e.g., "7C", "67"
        }
        
        private void LogError(string s)
        {
            if (!enableLogs) return;
            MetaverseProgram.Logger.LogWarning(s);
        }
        
        private void LogWarning(string s)
        {
            if (!enableLogs) return;
            MetaverseProgram.Logger.LogWarning(s);
        }
        
        private void Log(string s)
        {
            if (!enableLogs) return;
            MetaverseProgram.Logger.Log(s);
        }

        #endregion
    }
}
#endif // !UNITY_WEBGL || UNITY_EDITOR
#endif // METAVERSE_CLOUD_ENGINE && METAVERSE_CLOUD_ENGINE_INITIALIZED