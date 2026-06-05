using UnityEngine;
#if UNITY_EDITOR
using System.Collections;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using DSB.GC;
using DSB.GC.RuntimeMessages;
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

        private void OnEnable()
        {
            GCRuntimeMessageOutput.RuntimeMessagesEmitted += PublishRuntimeMessages;
            GCRuntimeScreenSpaceOutput.ScreenSpaceEmitted += PublishScreenSpace;
        }

        private void OnDisable()
        {
            GCRuntimeMessageOutput.RuntimeMessagesEmitted -= PublishRuntimeMessages;
            GCRuntimeScreenSpaceOutput.ScreenSpaceEmitted -= PublishScreenSpace;
        }

        private void Update()
        {
            TryPublishRuntimeSnapshot();
        }

        public void Connect()
        {
            shouldReconnect = true;

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

        RuntimeRegisterMessage BuildRuntimeRegisterMessage()
        {
            return GCDevAppRuntimeMessages.BuildRuntimeRegisterMessage(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                new GCUnityLocalProjectRootResolver()
            );
        }

        GCSeatIdentity[] GetRuntimeSeatIdentities(GamingCouch gamingCouch)
        {
            if (gamingCouch == null)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            return gamingCouch.GetCurrentPlaySeatIdentities();
        }

        RuntimeSnapshotState BuildRuntimeSnapshotState()
        {
            var gamingCouch = GamingCouch.Instance;
            var isRunning = Application.isPlaying;

            return GCDevAppRuntimeMessages.BuildRuntimeSnapshotState(
                currentRunId,
                isRunning,
                GetRuntimeSeatIdentities(gamingCouch),
                gamingCouch != null && gamingCouch.IsPaused,
                gamingCouch != null ? gamingCouch.CurrentTimescale : Time.timeScale
            );
        }

        RuntimeSnapshotMessage BuildRuntimeSnapshotMessage(RuntimeSnapshotState state)
        {
            return GCDevAppRuntimeMessages.BuildRuntimeSnapshotMessage(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                state
            );
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

        void PublishRuntimeMessages(string runtimeMessagesJson)
        {
            PublishRuntimeOutput(runtimeMessagesJson);
        }

        void PublishScreenSpace(string screenSpaceJson)
        {
            PublishRuntimeOutput(screenSpaceJson);
        }

        void PublishRuntimeOutput(string runtimeOutputJson)
        {
            if (string.IsNullOrEmpty(runtimeOutputJson) ||
                string.IsNullOrEmpty(currentRunId) ||
                websocket == null ||
                websocket.State != WebSocketState.Open)
            {
                return;
            }

            StartCoroutine(SendJsonMessage(runtimeOutputJson));
        }

        void TryPublishRuntimeSnapshot()
        {
            if (websocket == null || websocket.State != WebSocketState.Open || isSnapshotSendInFlight || string.IsNullOrEmpty(currentRunId))
            {
                return;
            }

            var snapshotState = BuildRuntimeSnapshotState();
            var snapshotSignature = GCDevAppRuntimeMessages.BuildRuntimeSnapshotSignature(snapshotState);
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
                yield return ScheduleReconnect();
                yield break;
            }

            yield return new WaitUntil(() => connectTask.IsCompleted);

            if (connectTask.IsFaulted || connectTask.IsCanceled || websocket == null)
            {
                isConnecting = false;
                if (connectTask.IsFaulted)
                {
                    LogWebSocket("Connect faulted.");
                }
                else if (connectTask.IsCanceled)
                {
                    LogWebSocket("Connect canceled.");
                }
                else
                {
                    LogWebSocket("Connect ended after websocket closed.");
                }
                yield return ScheduleReconnect();
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
                yield return ScheduleReconnect();
            }
        }

        IEnumerator ScheduleReconnect()
        {
            if (!shouldReconnect)
            {
                yield break;
            }

            LogWebSocket($"Reconnect scheduled in {reconnectDelay:0.##}s.");
            yield return new WaitForSeconds(reconnectDelay);

            if (shouldReconnect)
            {
                Connect();
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
                        ForwardToGamingCouch(message.payload, message.payload.inputs);
                    }
                    break;
                case "timescale_state":
                    if (message.payload != null)
                    {
                        ApplyTimescaleState(message.payload);
                    }
                    break;
                case "runtime_output_options":
                    if (message.payload != null)
                    {
                        ApplyRuntimeOutputOptions(message.payload);
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

        void ApplyRuntimeOutputOptions(WebSocketDevToolPayload message)
        {
            if (message.runtimeOutput == null)
            {
                return;
            }

            GCDevAppRuntimeOutputSettings.SetUnityLogCaptureMode(message.runtimeOutput.unityLogCapture);
        }


        void ForwardToGamingCouch(WebSocketDevToolPayload payload, WebSocketInputData inputs)
        {
            if (GamingCouch.Instance == null)
            {
                return;
            }

            var playerIndex = payload.playerIndex;
            if (playerIndex < 0 && payload.playerId > 0)
            {
                if (!GamingCouch.Instance.TryGetPlayerIndexForLegacyPlayerId(payload.playerId, out playerIndex))
                {
                    return;
                }
            }

            if (!GamingCouch.Instance.TryValidateActivePlayerIndex(playerIndex, "devapp_input", out _))
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

            string inputString = $"{playerIndex}|{JsonUtility.ToJson(inputData)}";
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
        public int playerIndex = -1;
        public int playerId;
        public WebSocketInputData inputs;
        public WebSocketRuntimeOutputOptions runtimeOutput;
    }

    [Serializable]
    public class WebSocketRuntimeOutputOptions
    {
        public string unityLogCapture;
    }

#endif
}
