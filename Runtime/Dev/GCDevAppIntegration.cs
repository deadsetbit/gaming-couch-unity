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
        private string serverUrl = "ws://localhost:3167/ws";
        private float reconnectDelay = 2f;
        private bool autoConnectOnPlay = true;

        [Header("Debug")]
        [SerializeField]
        private bool webSocketLogging = false;

        // An inbound message is a DevApp command or a compact input frame, so nothing legitimate
        // comes close to this. The cap stops a truncated or hostile fragment stream from growing
        // the reassembly accumulators without bound.
        private const int MaxInboundMessageBytes = 1024 * 1024;

        private ClientWebSocket websocket;
        private CancellationTokenSource cancellationTokenSource;
        private int connectionEpoch;
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

            if (webSocketLogging)
            {
                LogWebSocket($"Outgoing: {payload}");
            }

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
                // Read sendTask.Exception so the faulted Task's AggregateException is observed by the
                // TPL (otherwise it can resurface via TaskScheduler.UnobservedTaskException on
                // finalization). The read must stay outside the logging guard for that reason.
                var sendException = sendTask.Exception;
                if (webSocketLogging)
                {
                    LogWebSocket($"Send faulted: {sendException}");
                }
            }
        }

        IEnumerator SendRuntimeRegisterMessage()
        {
            yield return SendJsonMessage(JsonUtility.ToJson(BuildRuntimeRegisterMessage()));
        }

        IEnumerator SendRuntimeSnapshotMessage(RuntimeSnapshotState state, string signature)
        {
            var epoch = connectionEpoch;
            isSnapshotSendInFlight = true;

            yield return SendJsonMessage(JsonUtility.ToJson(BuildRuntimeSnapshotMessage(state)));

            if (epoch != connectionEpoch)
            {
                // CloseWebSocket already reset the in-flight flag and the signature; a newer
                // connection owns both now.
                yield break;
            }

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
            var epoch = connectionEpoch;
            cancellationTokenSource = new CancellationTokenSource();
            websocket = new ClientWebSocket();

            System.Threading.Tasks.Task connectTask = null;
            bool hasException = false;

            try
            {
                var connectUrl = BuildConnectionUrl();
                if (webSocketLogging)
                {
                    LogWebSocket($"Connecting to {connectUrl}");
                }

                connectTask = websocket.ConnectAsync(new Uri(connectUrl), cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                hasException = true;
                if (webSocketLogging)
                {
                    LogWebSocket($"Connect exception: {e.Message}");
                }
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

            if (epoch == connectionEpoch && websocket.State == WebSocketState.Open)
            {
                LogWebSocket("Connected.");
                currentRunId = BuildRunId();
                lastSentSnapshotSignature = null;
                runtimeInbound.ResetInputSequences();
                yield return SendRuntimeRegisterMessage();
                TryPublishRuntimeSnapshot();
                StartCoroutine(ReceiveMessages(epoch));
            }

            isConnecting = false;
        }

        IEnumerator ReceiveMessages(int epoch)
        {
            var buffer = new byte[1024 * 16];
            var messageBuilder = new StringBuilder(1024);
            var binaryMessage = new List<byte>(GCDevAppRuntimeInbound.CompactControllerInputByteLength);
            var isDiscardingOversizedMessage = false;

            LogWebSocket("Receive loop started.");
            while (epoch == connectionEpoch && websocket != null && websocket.State == WebSocketState.Open)
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
                    if (webSocketLogging)
                    {
                        LogWebSocket($"Receive exception: {e.Message}");
                    }
                }

                if (hasException)
                {
                    break;
                }

                yield return new WaitUntil(() => receiveTask.IsCompleted);

                if (epoch != connectionEpoch)
                {
                    break;
                }

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
                    if (webSocketLogging)
                    {
                        LogWebSocket($"Remote closed: {result.CloseStatus} {result.CloseStatusDescription}");
                    }

                    break;
                }

                if (isDiscardingOversizedMessage ||
                    messageBuilder.Length + binaryMessage.Count + result.Count > MaxInboundMessageBytes)
                {
                    if (webSocketLogging && !isDiscardingOversizedMessage)
                    {
                        LogWebSocket($"Dropping inbound message past {MaxInboundMessageBytes} bytes.");
                    }

                    // Drop the whole message rather than the fragment that crossed the cap, so its
                    // tail is never reassembled into a message of its own.
                    messageBuilder.Length = 0;
                    binaryMessage.Clear();
                    isDiscardingOversizedMessage = !result.EndOfMessage;
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Length = 0;
                        if (webSocketLogging)
                        {
                            LogWebSocket($"Incoming: {message}");
                        }

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

            if (epoch != connectionEpoch)
            {
                LogWebSocket("Receive loop ended for a superseded connection.");
                yield break;
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

            if (webSocketLogging)
            {
                LogWebSocket($"Reconnect scheduled in {reconnectDelay:0.##}s.");
            }

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

                if (webSocketLogging && decision.status == GCDevAppRuntimeInboundStatus.Unhandled)
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
                    if (webSocketLogging)
                    {
                        LogWebSocket($"Unhandled binary message length: {message?.Length ?? 0}");
                    }

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

            if (webSocketLogging && string.Equals(validationSource, "devapp_input", StringComparison.Ordinal))
            {
                LogWebSocket($"Applying JSON input to GamingCouch player {decision.playerIndex}");
            }

            GamingCouch.Instance.ApplyExternalPlayerInput(decision.playerIndex, decision.inputs, "devapp_input");
        }

        void ApplyTimescaleState(GCDevAppRuntimeInboundDecision decision)
        {
            GCDevUtils.ApplyTimescale(decision.timescale);
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
            // Retires every coroutine still running for the previous socket, so a stale receive loop
            // or in-flight snapshot cannot touch the next connection's state.
            connectionEpoch += 1;
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
                        if (webSocketLogging)
                        {
                            LogWebSocket($"Close exception: {e.Message}");
                        }
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

        // Callers that build their message (interpolation, concatenation) guard on webSocketLogging
        // themselves, so nothing is formatted while logging is off.
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
