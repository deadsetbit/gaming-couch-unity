#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Unity.Netcode;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCServer
    {
        [DllImport("__Internal")]
        internal static extern void _GCGameMessageToJS(byte[] data, int offset, int count, uint gcClientId, bool isReliable);

        private static Dictionary<ulong, GCPeer> Clients = new Dictionary<ulong, GCPeer>();

        // private static readonly object ConnectionLock = new object();

        private static ulong ClientIdCounter = 1;
        private static Queue<GCEvent> EventQueue = new Queue<GCEvent>();

        private static GCEvent NothingEvent = new GCEvent(NetworkEvent.Nothing, 0, null);

        private static ulong GetNextClientId()
        {
            return ClientIdCounter++;
        }

        public static ulong Ping(ulong clientId)
        {
            // lock (ConnectionLock)
            // {
            if (Clients.ContainsKey(clientId))
            {
                return Clients[clientId].Ping;
            }
            // }

            return 0;
        }

        public static void Send(ulong clientId, ArraySegment<byte> data, bool isReliable)
        {
            UnityEngine.Debug.Log("GCServer - Send - clientId:" + clientId + " data: " + data.Count + " isReliable: " + isReliable + " Clients.Count: " + Clients.Count);
            // lock (ConnectionLock)
            // {
            if (Clients.ContainsKey(clientId))
            {
                var gcClientId = GCNetworkManager.Instance.GetGCClientIdByNetworkId(clientId);

                if (data.Count < data.Array.Length || data.Offset > 0)
                {
                    byte[] slimPayload = new byte[data.Count];

                    Buffer.BlockCopy(data.Array, data.Offset, slimPayload, 0, data.Count);

                    _GCGameMessageToJS(slimPayload, 0, slimPayload.Length, gcClientId, isReliable);
                    // Clients[clientId].Send(slimPayload);
                }
                else
                {
                    _GCGameMessageToJS(data.Array, data.Offset, data.Count, gcClientId, isReliable);
                    // Clients[clientId].Send(data.Array);
                }
            }
            // }
        }

        public static GCEvent Poll()
        {
            // lock (ConnectionLock)
            // {
            if (EventQueue.Count > 0)
            {
                UnityEngine.Debug.Log("GCServer - Dequeue");
                return EventQueue.Dequeue();
            }
            else
            {
                return NothingEvent;
            }
            // }
        }

        public IPEndPoint Endpoint { get; private set; }
        public ulong ClientId { get; private set; }

        private void OnOpen()
        {
            // lock (ConnectionLock)
            // {
            ClientId = GetNextClientId();
            Clients[ClientId] = new GCPeer(ClientId);

            EventQueue.Enqueue(new GCEvent(NetworkEvent.Connect, ClientId, null));
            // }
        }

        public void OnMessage(ulong clientId, ArraySegment<byte> data)
        {
            UnityEngine.Debug.Log("GCServer - OnMessage - ClientId: " + clientId + " data: " + data.Count);
            // lock (ConnectionLock)
            // {
            if (Clients.ContainsKey(ClientId))
            {
                EventQueue.Enqueue(new GCEvent(NetworkEvent.Data, clientId, data.Array));
            }
            // }
        }

        public void AddMockClients()
        {
            UnityEngine.Debug.Log("AddMockClients");
            // lock (ConnectionLock)
            // {
            for (int i = 0; i < 3; i++)
            {
                UnityEngine.Debug.Log("AddMockClient " + i + 1);
                OnOpen();
            }
            // }
        }
    }
}
#endif