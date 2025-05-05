#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCClient
    {
        public Queue<GCEvent> EventQueue { get; } = new Queue<GCEvent>();

        [DllImport("__Internal")]
        internal static extern void _GCGameMessageToJS(byte[] data, int offset, int count, bool isReliable);

        private static GCEvent NothingEvent = new GCEvent(NetworkEvent.Nothing, 0, null);

        public ulong WaitTime => 0;

        public void Connect()
        {
            UnityEngine.Debug.Log("GCClient - Connect");

            OnOpen();
        }

        public void Send(ArraySegment<byte> data, bool isReliable)
        {
            _GCGameMessageToJS(data.Array, data.Offset, data.Count, isReliable);
        }

        public GCEvent Poll()
        {
            if (EventQueue.Count > 0)
            {
                return EventQueue.Dequeue();
            }
            else
            {
                return NothingEvent;
            }
        }

        public void OnOpen()
        {
            UnityEngine.Debug.Log("GCClient - OnOpen");

            EventQueue.Enqueue(new GCEvent(NetworkEvent.Connect, 0, null));
        }

        public void OnMessage(ArraySegment<byte> data)
        {
            EventQueue.Enqueue(new GCEvent(NetworkEvent.Data, 0, data.Array));
        }
    }
}
#endif