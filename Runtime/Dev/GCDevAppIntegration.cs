using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
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
        private readonly GCDevAppRuntimeInbound runtimeInbound = new GCDevAppRuntimeInbound();

        private void Start()
        {
            if (autoConnectOnPlay)
            {
                Connect();
            }
        }

        private void OnEnable()
        {
            GCRuntimeOutput.RuntimeMessagesEmitted += PublishRuntimeMessages;
            GCRuntimeOutput.ScreenSpaceEmitted += PublishScreenSpace;
        }

        private void OnDisable()
        {
            GCRuntimeOutput.RuntimeMessagesEmitted -= PublishRuntimeMessages;
            GCRuntimeOutput.ScreenSpaceEmitted -= PublishScreenSpace;
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
                runtimeInbound.ResetInputSequences();
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
            var binaryMessage = new List<byte>(GCDevAppRuntimeInbound.CompactControllerInputByteLength);

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
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    for (var i = 0; i < result.Count; i += 1)
                    {
                        binaryMessage.Add(buffer[i]);
                    }

                    if (result.EndOfMessage)
                    {
                        ProcessBinaryWebSocketMessage(binaryMessage.ToArray());
                        binaryMessage.Clear();
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
                var decision = runtimeInbound.RouteTextMessage(message, BuildInboundContext());
                ApplyInboundDecision(decision, "devapp_input");

                if (decision.status == GCDevAppRuntimeInboundStatus.Unhandled)
                {
                    LogWebSocket("Unhandled message type." + message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing WebSocket message: {e.Message}\nMessage: {message}");
            }
        }

        void ProcessBinaryWebSocketMessage(byte[] message)
        {
            try
            {
                if (!GCDevAppRuntimeInbound.TryParseCompactControllerInputFrame(message, out var inputFrame))
                {
                    LogWebSocket($"Unhandled binary message length: {message?.Length ?? 0}");
                    return;
                }

                if (GamingCouch.Instance == null)
                {
                    return;
                }

                if (!GamingCouch.Instance.TryValidatePlayerIndex(inputFrame.playerIndex, "devapp_compact_input", out _))
                {
                    return;
                }

                var decision = runtimeInbound.RouteValidatedCompactControllerInputFrame(inputFrame);
                ApplyInboundDecision(decision, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing binary WebSocket message: {e.Message}");
            }
        }

        GCDevAppRuntimeInboundContext BuildInboundContext()
        {
            var gamingCouch = GamingCouch.Instance;
            return new GCDevAppRuntimeInboundContext
            {
                isPaused = gamingCouch != null && gamingCouch.IsPaused,
            };
        }

        void ApplyInboundDecision(GCDevAppRuntimeInboundDecision decision, string inputValidationSource)
        {
            if (decision.status != GCDevAppRuntimeInboundStatus.Intent)
            {
                return;
            }

            switch (decision.intentKind)
            {
                case GCDevAppRuntimeInboundIntentKind.Restart:
                    RestartGame();
                    break;
                case GCDevAppRuntimeInboundIntentKind.Input:
                    ApplyInboundInput(decision, inputValidationSource);
                    break;
                case GCDevAppRuntimeInboundIntentKind.TimescaleState:
                    ApplyTimescaleState(decision);
                    break;
                case GCDevAppRuntimeInboundIntentKind.RuntimeOutputOptions:
                    ApplyRuntimeOutputOptions(decision);
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

        void ApplyInboundInput(GCDevAppRuntimeInboundDecision decision, string validationSource)
        {
            if (GamingCouch.Instance == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(validationSource) &&
                !GamingCouch.Instance.TryValidatePlayerIndex(decision.playerIndex, validationSource, out _))
            {
                return;
            }

            if (string.Equals(validationSource, "devapp_input", StringComparison.Ordinal))
            {
                LogWebSocket($"Applying JSON input to GamingCouch player {decision.playerIndex}");
            }

            GamingCouch.Instance.ApplyDevAppInput(decision.playerIndex, decision.inputs);
        }

        void ApplyTimescaleState(GCDevAppRuntimeInboundDecision decision)
        {
            SetTimescale(decision.timescale);
            if (decision.shouldApplyPause)
            {
                SetPause(decision.paused);
            }
        }

        void ApplyRuntimeOutputOptions(GCDevAppRuntimeInboundDecision decision)
        {
            GCDevAppRuntimeOutputSettings.SetRuntimeLogCaptureMode(decision.runtimeLogCaptureMode);
        }

        void CloseWebSocket()
        {
            currentRunId = null;
            lastSentSnapshotSignature = null;
            isSnapshotSendInFlight = false;
            runtimeInbound.ResetInputSequences();

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
}
