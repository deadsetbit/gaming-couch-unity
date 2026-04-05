using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using DSB.GC;
#endif

namespace DSB.GC.Dev
{
#if UNITY_EDITOR
    [RequireComponent(typeof(GamingCouch))]
#endif
    public class GCDevAppIntegration : MonoBehaviour
    {
#if !UNITY_EDITOR
        // Stub for builds as dev integrations are not part of final builds.
#else
        [Header("WebSocket Configuration")]
        [SerializeField]
        private string serverUrl = "ws://localhost:3100/ws";
        private float reconnectDelay = 2f;
        private bool autoConnectOnPlay = true;

        [Header("Debug")]
        [SerializeField]
        private bool webSocketLogging = false;

        private ClientWebSocket websocket;
        private CancellationTokenSource cancellationTokenSource;
        private bool isConnecting = false;
        private bool shouldReconnect = true;
        private bool isSnapshotSendInFlight = false;
        private string currentRunId;
        private string lastSentSnapshotSignature;

        private void Start()
        {
            if (autoConnectOnPlay)
            {
                Connect();
            }
        }

        private void Update()
        {
            TryPublishRuntimeSnapshot();
        }

        public void Connect()
        {
            if (isConnecting || (websocket != null && websocket.State == WebSocketState.Open))
            {
                LogWebSocket("Connect ignored; already connecting or connected.");
                return;
            }

            LogWebSocket("Connect requested.");
            StartCoroutine(ConnectWebSocket());
        }

        public void Disconnect()
        {
            shouldReconnect = false;
            LogWebSocket("Disconnect requested.");
            CloseWebSocket();
        }

        string BuildConnectionUrl()
        {
            var separator = serverUrl.Contains("?") ? "&" : "?";
            return $"{serverUrl}{separator}identity=runtime";
        }

        static string BuildRunId()
        {
            return Guid.NewGuid().ToString("N");
        }

        static bool IsWindowsPath(string value)
        {
            return value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '/';
        }

        static string NormalizeProjectRootPath(string value)
        {
            var fullPath = Path.GetFullPath(value);
            var normalizedSlashes = fullPath.Replace("\\", "/");
            var trimmedPath = normalizedSlashes.TrimEnd('/');
            if (string.IsNullOrEmpty(trimmedPath))
            {
                trimmedPath = "/";
            }

            if (IsWindowsPath(trimmedPath) || trimmedPath.StartsWith("//", StringComparison.Ordinal))
            {
                return trimmedPath.ToLowerInvariant();
            }

            return trimmedPath;
        }

        static string ResolveProjectRootPath()
        {
            var projectRootPath = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return NormalizeProjectRootPath(Application.dataPath);
            }

            return NormalizeProjectRootPath(projectRootPath);
        }

        static string ResolveProjectName()
        {
            if (!string.IsNullOrWhiteSpace(Application.productName))
            {
                return Application.productName.Trim();
            }

            return new DirectoryInfo(ResolveProjectRootPath()).Name;
        }

        static string ResolveSeatType(string playerType)
        {
            return string.Equals(playerType, GCPlayerType.bot.ToString(), StringComparison.OrdinalIgnoreCase) ? "bot" : "player";
        }

        RuntimeRegisterMessage BuildRuntimeRegisterMessage()
        {
            return new RuntimeRegisterMessage
            {
                type = "runtime_register",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runtimeKind = "unity_editor",
                projectRootPath = ResolveProjectRootPath(),
                projectName = ResolveProjectName(),
                platform = "unity",
                rendererMode = "external",
                displayName = "Unity Editor",
            };
        }

        RuntimeCapabilitiesMessage BuildRuntimeCapabilities()
        {
            return new RuntimeCapabilitiesMessage
            {
                restart = true,
                pause = true,
                timescale = true,
            };
        }

        RuntimeSeatMessage[] BuildRuntimeSeats()
        {
            var gamingCouch = GamingCouch.Instance;
            if (gamingCouch == null)
            {
                return Array.Empty<RuntimeSeatMessage>();
            }

            var playerOptions = gamingCouch.GetCurrentPlayPlayerOptions();
            if (playerOptions.Length == 0)
            {
                return Array.Empty<RuntimeSeatMessage>();
            }

            var seats = new RuntimeSeatMessage[playerOptions.Length];
            for (var index = 0; index < playerOptions.Length; index++)
            {
                var playerOption = playerOptions[index];
                seats[index] = new RuntimeSeatMessage
                {
                    playerId = playerOption.playerId,
                    seatIndex = index + 1,
                    label = $"Seat {index + 1}",
                    type = ResolveSeatType(playerOption.type),
                };
            }

            return seats;
        }

        RuntimeSnapshotState BuildRuntimeSnapshotState()
        {
            var gamingCouch = GamingCouch.Instance;
            var isRunning = Application.isPlaying;

            return new RuntimeSnapshotState
            {
                runId = isRunning ? currentRunId : null,
                isRunning = isRunning,
                capabilities = BuildRuntimeCapabilities(),
                seats = isRunning ? BuildRuntimeSeats() : Array.Empty<RuntimeSeatMessage>(),
                paused = gamingCouch != null && gamingCouch.IsPaused,
                timescale = gamingCouch != null ? gamingCouch.CurrentTimescale : Time.timeScale,
            };
        }

        RuntimeSnapshotMessage BuildRuntimeSnapshotMessage(RuntimeSnapshotState state)
        {
            return new RuntimeSnapshotMessage
            {
                type = "runtime_snapshot",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId = state.runId,
                isRunning = state.isRunning,
                capabilities = state.capabilities,
                seats = state.seats,
                paused = state.paused,
                timescale = state.timescale,
            };
        }

        IEnumerator SendJsonMessage(string payload)
        {
            if (websocket == null || websocket.State != WebSocketState.Open)
            {
                yield break;
            }

            LogWebSocket($"Outgoing: {payload}");
            var bytes = Encoding.UTF8.GetBytes(payload);
            var sendTask = websocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationTokenSource != null ? cancellationTokenSource.Token : CancellationToken.None
            );

            yield return new WaitUntil(() => sendTask.IsCompleted);

            if (sendTask.IsFaulted)
            {
                LogWebSocket("Send faulted.");
            }
        }

        IEnumerator SendRuntimeRegisterMessage()
        {
            yield return SendJsonMessage(JsonUtility.ToJson(BuildRuntimeRegisterMessage()));
        }

        IEnumerator SendRuntimeSnapshotMessage(RuntimeSnapshotState state, string signature)
        {
            isSnapshotSendInFlight = true;

            yield return SendJsonMessage(JsonUtility.ToJson(BuildRuntimeSnapshotMessage(state)));

            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                lastSentSnapshotSignature = signature;
            }

            isSnapshotSendInFlight = false;
        }

        void TryPublishRuntimeSnapshot()
        {
            if (websocket == null || websocket.State != WebSocketState.Open || isSnapshotSendInFlight || string.IsNullOrEmpty(currentRunId))
            {
                return;
            }

            var snapshotState = BuildRuntimeSnapshotState();
            var snapshotSignature = JsonUtility.ToJson(snapshotState);
            if (snapshotSignature == lastSentSnapshotSignature)
            {
                return;
            }

            StartCoroutine(SendRuntimeSnapshotMessage(snapshotState, snapshotSignature));
        }

        IEnumerator ConnectWebSocket()
        {
            isConnecting = true;
            CloseWebSocket();
            cancellationTokenSource = new CancellationTokenSource();
            websocket = new ClientWebSocket();

            System.Threading.Tasks.Task connectTask = null;
            bool hasException = false;

            try
            {
                var connectUrl = BuildConnectionUrl();
                LogWebSocket($"Connecting to {connectUrl}");
                connectTask = websocket.ConnectAsync(new Uri(connectUrl), cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                hasException = true;
                LogWebSocket($"Connect exception: {e.Message}");
            }

            if (hasException)
            {
                isConnecting = false;
                if (shouldReconnect)
                {
                    LogWebSocket($"Reconnect scheduled in {reconnectDelay:0.##}s.");
                    yield return new WaitForSeconds(reconnectDelay);
                    StartCoroutine(ConnectWebSocket());
                }
                yield break;
            }

            yield return new WaitUntil(() => connectTask.IsCompleted);

            if (connectTask.IsFaulted)
            {
                isConnecting = false;
                LogWebSocket("Connect faulted.");
                if (shouldReconnect)
                {
                    LogWebSocket($"Reconnect scheduled in {reconnectDelay:0.##}s.");
                    yield return new WaitForSeconds(reconnectDelay);
                    StartCoroutine(ConnectWebSocket());
                }
                yield break;
            }

            if (websocket.State == WebSocketState.Open)
            {
                LogWebSocket("Connected.");
                currentRunId = BuildRunId();
                lastSentSnapshotSignature = null;
                yield return SendRuntimeRegisterMessage();
                TryPublishRuntimeSnapshot();
                StartCoroutine(ReceiveMessages());
            }

            isConnecting = false;
        }

        IEnumerator ReceiveMessages()
        {
            var buffer = new byte[1024 * 16];
            var messageBuilder = new StringBuilder(1024);

            LogWebSocket("Receive loop started.");
            while (websocket != null && websocket.State == WebSocketState.Open)
            {
                System.Threading.Tasks.Task<System.Net.WebSockets.WebSocketReceiveResult> receiveTask = null;
                bool hasException = false;

                try
                {
                    receiveTask = websocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    hasException = true;
                    LogWebSocket($"Receive exception: {e.Message}");
                }

                if (hasException)
                {
                    break;
                }

                yield return new WaitUntil(() => receiveTask.IsCompleted);

                if (receiveTask.IsFaulted)
                {
                    LogWebSocket("Receive faulted.");
                    break;
                }

                if (receiveTask.IsCanceled)
                {
                    LogWebSocket("Receive canceled.");
                    break;
                }

                var result = receiveTask.Result;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    LogWebSocket($"Remote closed: {result.CloseStatus} {result.CloseStatusDescription}");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Length = 0;
                        LogWebSocket($"Incoming: {message}");
                        ProcessWebSocketMessage(message);
                    }
                }
            }

            LogWebSocket("Receive loop ended.");
            if (shouldReconnect && websocket != null && websocket.State != WebSocketState.Open)
            {
                LogWebSocket($"Reconnect scheduled in {reconnectDelay:0.##}s.");
                yield return new WaitForSeconds(reconnectDelay);
                StartCoroutine(ConnectWebSocket());
            }
        }

        void ProcessWebSocketMessage(string message)
        {
            try
            {
                if (message.Contains("\"type\":\"gcdevtool\""))
                {
                    var data = JsonUtility.FromJson<WebSocketDevToolMessage>(message);
                    if (data.type == "gcdevtool")
                    {
                        HandleDevToolAction(data);
                    }
                }
                else
                {
                    LogWebSocket("Unhandled message type." + message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing WebSocket message: {e.Message}\nMessage: {message}");
            }
        }

        void HandleDevToolAction(WebSocketDevToolMessage message)
        {
            switch (message.action)
            {
                case "restart":
                    RestartGame();
                    break;
                case "input":
                    if (message.payload != null && message.payload.inputs != null)
                    {
                        ForwardToGamingCouch(message.payload.playerId, message.payload.inputs);
                    }
                    break;
                case "timescale_state":
                    if (message.payload != null)
                    {
                        ApplyTimescaleState(message.payload);
                    }
                    break;
            }
        }

        void RestartGame()
        {
            if (GamingCouch.Instance != null && !GamingCouch.Instance.IsRestarting)
            {
                GamingCouch.Instance._InternalHandleGamePlayModeRestart();
            }
        }

        void SetPause(bool paused)
        {
            if (GamingCouch.Instance != null)
            {
                GamingCouch.Instance.ApplyDevPause(paused);
            }
        }

        void SetTimescale(float timescale)
        {
            if (GamingCouch.Instance != null)
            {
                GamingCouch.Instance.ApplyDevTimescale(timescale);
                return;
            }

            Time.timeScale = Mathf.Clamp(timescale, 0.1f, 10.0f);
        }

        void ApplyTimescaleState(WebSocketDevToolPayload message)
        {
            SetTimescale(message.timescale);
            if (GamingCouch.Instance?.IsPaused == message.paused)
            {
                return;
            }

            SetPause(message.paused);
        }


        void ForwardToGamingCouch(int playerId, WebSocketInputData inputs)
        {
            if (GamingCouch.Instance == null)
            {
                return;
            }

            var inputData = new GCControllerInputsData
            {
                a0 = inputs.a0,
                a1 = inputs.a1,
                a2 = 0f,
                a3 = 0f,
                b0 = inputs.b0 > 0.5f ? 1 : 0,
                b1 = inputs.b1 > 0.5f ? 1 : 0,
                b2 = 0,
                b3 = 0,
                b12 = 0,
                b13 = 0,
                b14 = 0,
                b15 = 0
            };

            string inputString = $"{playerId}|{JsonUtility.ToJson(inputData)}";
            LogWebSocket($"Outgoing (to GamingCouch): {inputString}");
            GamingCouch.Instance.SendMessage("GamingCouchInputs", inputString);
        }

        void CloseWebSocket()
        {
            currentRunId = null;
            lastSentSnapshotSignature = null;
            isSnapshotSendInFlight = false;

            if (websocket != null)
            {
                cancellationTokenSource?.Cancel();

                if (websocket.State == WebSocketState.Open)
                {
                    try
                    {
                        LogWebSocket("Closing websocket.");
                        websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(1000);
                    }
                    catch (Exception e)
                    {
                        LogWebSocket($"Close exception: {e.Message}");
                    }
                }

                websocket.Dispose();
                websocket = null;
            }

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }

        void OnDestroy()
        {
            CloseWebSocket();
        }

        void OnApplicationQuit()
        {
            shouldReconnect = false;
            CloseWebSocket();
        }

        void LogWebSocket(string message)
        {
            if (!webSocketLogging)
            {
                return;
            }

            Debug.Log($"[GCDevApp][WebSocket] {message}");
        }
#endif
    }

#if UNITY_EDITOR
    [Serializable]
    public class WebSocketInputData
    {
        public float a0;
        public float a1;
        public float b0;
        public float b1;
    }

    [Serializable]
    public class WebSocketDevToolMessage
    {
        public string type;
        public string action;
        public WebSocketDevToolPayload payload;
        public long timestamp;
    }

    [Serializable]
    public class WebSocketDevToolPayload
    {
        public float timescale;
        public bool paused;
        public int playerId;
        public WebSocketInputData inputs;
    }

    [Serializable]
    public class RuntimeCapabilitiesMessage
    {
        public bool restart;
        public bool pause;
        public bool timescale;
    }

    [Serializable]
    public class RuntimeSeatMessage
    {
        public int playerId;
        public int seatIndex;
        public string label;
        public string type;
    }

    [Serializable]
    public class RuntimeRegisterMessage
    {
        public string type;
        public long timestamp;
        public string runtimeKind;
        public string projectRootPath;
        public string projectName;
        public string platform;
        public string rendererMode;
        public string displayName;
    }

    [Serializable]
    public class RuntimeSnapshotState
    {
        public string runId;
        public bool isRunning;
        public RuntimeCapabilitiesMessage capabilities;
        public RuntimeSeatMessage[] seats;
        public bool paused;
        public float timescale;
    }

    [Serializable]
    public class RuntimeSnapshotMessage
    {
        public string type;
        public long timestamp;
        public string runId;
        public bool isRunning;
        public RuntimeCapabilitiesMessage capabilities;
        public RuntimeSeatMessage[] seats;
        public bool paused;
        public float timescale;
    }
#endif
}
