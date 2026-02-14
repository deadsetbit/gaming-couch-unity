using UnityEngine;

namespace DSB.GC.Dev
{
#if !UNITY_EDITOR
    public class GCDevAppIntegration : MonoBehaviour
    {
        // stud for builds as dev integrations are not part of the final builds
    }
#else
    using System;
    using System.Collections;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using DSB.GC;

    [RequireComponent(typeof(GamingCouch))]
    public class GCDevAppIntegration : MonoBehaviour
    {
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

        private void Start()
        {
            if (autoConnectOnPlay)
            {
                Connect();
            }
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
            var name = Uri.EscapeDataString(string.IsNullOrEmpty(Application.productName) ? "Unity" : Application.productName);
            var platform = Uri.EscapeDataString(Application.isEditor ? "Unity Editor" : Application.platform.ToString());
            var separator = serverUrl.Contains("?") ? "&" : "?";
            return $"{serverUrl}{separator}identity=project&name={name}&platform={platform}";
        }
        IEnumerator ConnectWebSocket()
        {
            isConnecting = true;
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
                GamingCouch.Instance.InternalHandleGamePlayModeRestart();
            }
        }

        void SetPause(bool paused)
        {
            if (GamingCouch.Instance != null)
            {
                GamingCouch.Instance.SendMessage("GamingCouchPause", paused.ToString(), SendMessageOptions.DontRequireReceiver);
            }
        }

        void SetTimescale(float timescale)
        {
            Time.timeScale = Mathf.Clamp(timescale, 0.1f, 5.0f);
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
    }

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
#endif
}
