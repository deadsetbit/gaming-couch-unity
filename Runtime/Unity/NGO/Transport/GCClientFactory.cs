#if GC_UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Runtime.InteropServices;
using AOT;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCClientFactory
    {
        public static GCClient Client;
        internal delegate void OnMessageCallback(IntPtr messagePointer, int messageSize);

        [DllImport("__Internal")]
        internal static extern void _SetOnMessage(OnMessageCallback callback);

        [MonoPInvokeCallback(typeof(OnMessageCallback))]
        internal static void OnMessageEvent(IntPtr payloadPointer, int length)
        {
            var buffer = new byte[length];

            Marshal.Copy(payloadPointer, buffer, 0, length);
            Client.OnMessage(new ArraySegment<byte>(buffer, 0, length));
        }

        public static GCClient Create()
        {
            Client = new GCClient();
            _SetOnMessage(OnMessageEvent);

            return Client;
        }
    }
}
#endif