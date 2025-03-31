using System;
using Unity.Netcode;
using UnityEngine;
using DSB.GC;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCTransport : NetworkTransport
    {
        private GCServer GCServer = null;
        private GCClient GCClient = null;
        private bool IsStarted = false;

        public override ulong ServerClientId => 0;

        private void OnValidate()
        {
            // Try to find GamingCouch component from the scene
            var gamingCouch = FindFirstObjectByType<GamingCouch>();

            if (!gamingCouch)
            {
                throw new InvalidOperationException("GCTransport requires GamingCouch to be present in the scene");
            }

            if (!gamingCouch.OnlineMultiplayerSupport)
            {
                throw new InvalidOperationException("GCTransport requires Online Multiplayer Support to be enabled in GamingCouch. Please enable it from the GamingCouch object on scene and follow the GamingCouch online multiplayer instructions in the documentation.");
            }
        }

        private void ReceiveMessage(string base64String)
        {
            byte[] data = Convert.FromBase64String(base64String);

            if (NetworkManager.Singleton.IsServer)
            {
                GCServer.OnMessage(new ArraySegment<byte>(data));
            }
            else
            {
                GCClient.OnMessage(new ArraySegment<byte>(data));
            }
        }

        public override void DisconnectLocalClient()
        {
            // Not required for GamingCouch as the platform handles the disconnection
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            // Not required for GamingCouch as the platform handles the disconnection
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0; // TODO: If this is useful or required, the platform should calculate this
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            Debug.Log("GCTransport - Initializing");
        }

        public GCEvent GetNextWebSocketEvent()
        {
            if (GCClient != null)
            {
                return GCClient.Poll();
            }

            return GCServer.Poll();
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            var e = GetNextWebSocketEvent();

            clientId = e.ClientId;
            receiveTime = Time.realtimeSinceStartup;

            if (e.Payload != null)
            {
                Debug.Log("Payload: " + e.Payload.Length);
                payload = new ArraySegment<byte>(e.Payload);
            }
            else
            {
                payload = new ArraySegment<byte>();
            }

            return e.GetNetworkEvent();
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
            Debug.Log("Sending message: " + data.Count + " delivery mode:" + delivery);

            var isReliable = false;

            // It could be possible to achieve all of these with multiple data channels on WebRTC side,
            // but should be sufficient now to either reliable or unreliable.
            switch (delivery)
            {
                case NetworkDelivery.Reliable:
                case NetworkDelivery.ReliableSequenced:
                case NetworkDelivery.ReliableFragmentedSequenced:
                    isReliable = true;
                    break;
                case NetworkDelivery.Unreliable:
                case NetworkDelivery.UnreliableSequenced:
                    isReliable = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "Unhandled NetworkDelivery mode");
            }

            if (clientId == ServerClientId)
            {
                GCClient.Send(data, isReliable);
            }
            else
            {
                GCServer.Send(clientId, data, isReliable);
            }
        }

        public override void Shutdown()
        {

        }

        public override bool StartClient()
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Socket already started");
            }

            Debug.Log("GCTransport - Starting client");

            GCClient = GCClientFactory.Create();
            GCClient.Connect();

            IsStarted = true;

            return true;
        }

        public override bool StartServer()
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("Socket already started");
            }

            Debug.Log("GCTransport - Starting server");

            GCServer = new GCServer();

            //--- hack
            // TODO: This is required to start sending the messages from the server to the clients for now
            // TODO: Currently one client is enough for everything. Think the client as the GC client and the client
            // TODO: broadcasts all the messages to all the clients. This makes it impossible for server to send message for a specific client.
            // TODO: This should be changed so that the host waits for all the clients to connect or?:
            // TODO:   - the GC client handles the connections and server only replicates messages to all clients
            GCServer.AddMockClients();
            // InvokeOnTransportEvent(NetworkEvent.Connect, 1, new ArraySegment<byte>(), Time.realtimeSinceStartup);
            //--- hack ends

            IsStarted = true;


            return true;
        }
    }
}
