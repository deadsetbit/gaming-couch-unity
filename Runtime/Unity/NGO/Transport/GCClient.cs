#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCClient
    {
        public Queue<GCEvent> EventQueue { get; } = new Queue<GCEvent>();

        [DllImport("__Internal")]
        internal static extern void _GCGameMessageToJS(byte[] data, int offset, int count, bool isReliable);

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
                return new GCEvent()
                {
                    ClientId = 0,
                    Payload = null,
                    Type = GCEvent.GCEventType.Nothing,
                };
            }
        }

        public void OnOpen()
        {
            UnityEngine.Debug.Log("GCClient - OnOpen");

            EventQueue.Enqueue(new GCEvent()
            {
                ClientId = 0,
                Payload = null,
                Type = GCEvent.GCEventType.Open,
            });
        }

        public void OnMessage(ArraySegment<byte> data)
        {
            EventQueue.Enqueue(new GCEvent()
            {
                ClientId = 0,
                Payload = data.Array,
                Type = GCEvent.GCEventType.Payload,
            });
        }
    }
}
#endif