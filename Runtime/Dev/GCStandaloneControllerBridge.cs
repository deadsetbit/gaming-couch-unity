using UnityEngine;
#if UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System;
using System.Collections;
using DSB.GC;
#endif

namespace DSB.GC.Dev
{
#if UNITY_EDITOR
    [RequireComponent(typeof(GamingCouch))]
    public class GCStandaloneControllerBridge : MonoBehaviour
    {
        [Header("WebSocket Configuration")]
        [SerializeField]
        private string serverUrl = "ws://localhost:3100/ws";

        [SerializeField]
        private float reconnectDelay = 2f;

        [SerializeField]
        private bool autoConnect = true;

        private ClientWebSocket websocket;
        private CancellationTokenSource cancellationTokenSource;
        private bool isConnecting = false;
        private bool shouldReconnect = true;

        private void Start()
        {
            if (autoConnect)
            {
                Connect();
            }
        }

        public void Connect()
        {
            if (isConnecting || (websocket != null && websocket.State == WebSocketState.Open))
            {
                return;
            }

            StartCoroutine(ConnectWebSocket());
        }

        public void Disconnect()
        {
            shouldReconnect = false;
            CloseWebSocket();
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
                connectTask = websocket.ConnectAsync(new Uri(serverUrl), cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                hasException = true;
            }

            if (hasException)
            {
                isConnecting = false;
                if (shouldReconnect)
                {
                    yield return new WaitForSeconds(reconnectDelay);
                    StartCoroutine(ConnectWebSocket());
                }
                yield break;
            }

            yield return new WaitUntil(() => connectTask.IsCompleted);

            if (connectTask.IsFaulted)
            {
                isConnecting = false;
                if (shouldReconnect)
                {
                    yield return new WaitForSeconds(reconnectDelay);
                    StartCoroutine(ConnectWebSocket());
                }
                yield break;
            }

            if (websocket.State == WebSocketState.Open)
            {
                StartCoroutine(ReceiveMessages());
            }

            isConnecting = false;
        }

        IEnumerator ReceiveMessages()
        {
            var buffer = new byte[1024 * 16];
            var messageBuilder = new StringBuilder(1024);

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
                }

                if (hasException)
                {
                    break;
                }

                yield return new WaitUntil(() => receiveTask.IsCompleted);

                if (receiveTask.IsFaulted)
                {
                    break;
                }

                var result = receiveTask.Result;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Length = 0;
                        ProcessWebSocketMessage(message);
                    }
                }
            }

            if (shouldReconnect && websocket != null && websocket.State != WebSocketState.Open)
            {
                yield return new WaitForSeconds(reconnectDelay);
                StartCoroutine(ConnectWebSocket());
            }
        }

        void ProcessWebSocketMessage(string message)
        {
            try
            {
                if (message.Contains("\"type\":\"input\""))
                {
                    var data = JsonUtility.FromJson<WebSocketInputMessage>(message);
                    if (data.type == "input" && data.inputs != null)
                    {
                        ForwardToGamingCouch(data.playerId, data.inputs);
                    }
                }
                else if (message.Contains("\"type\":\"devtool\""))
                {
                    var data = JsonUtility.FromJson<WebSocketDevToolMessage>(message);
                    if (data.type == "devtool")
                    {
                        HandleDevToolAction(data);
                    }
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
                case "pause":
                    SetPause(true);
                    break;
                case "play":
                    SetPause(false);
                    break;
                case "pauseToggle":
                    TogglePause();
                    break;
                case "setTimescale":
                    if (message.payload != null)
                    {
                        SetTimescale(message.payload.timescale);
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

        void TogglePause()
        {
            if (GamingCouch.Instance != null)
            {
                bool isCurrentlyPaused = Mathf.Approximately(Time.timeScale, 0.0f);
                GamingCouch.Instance.SendMessage("GamingCouchPause", (!isCurrentlyPaused).ToString(), SendMessageOptions.DontRequireReceiver);
            }
        }

        void SetTimescale(float timescale)
        {
            Time.timeScale = Mathf.Clamp(timescale, 0.1f, 5.0f);
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
                b2 = inputs.b2 > 0.5f ? 1 : 0,
                b3 = 0,
                b12 = 0,
                b13 = 0,
                b14 = 0,
                b15 = 0
            };

            string inputString = $"{playerId}|{JsonUtility.ToJson(inputData)}";
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
                        websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).Wait(1000);
                    }
                    catch (Exception e)
                    {
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
    }

    [Serializable]
    public class WebSocketInputMessage
    {
        public string type;
        public int playerId;
        public WebSocketInputData inputs;
        public long timestamp;
    }

    [Serializable]
    public class WebSocketInputData
    {
        public float a0;
        public float a1;
        public float b0;
        public float b1;
        public float b2;
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
    }
#endif
}
