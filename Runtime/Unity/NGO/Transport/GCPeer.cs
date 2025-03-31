#if GC_UNITY_NETCODE_GAMEOBJECTS
namespace DSB.GC.Unity.NGO.Transport
{
    public struct GCPeer
    {
        public ulong ClientId;

        public GCPeer(ulong clientId)
        {
            ClientId = clientId;
        }

        internal void Send(byte[] data)
        {
            UnityEngine.Debug.Log("GCPeer - Implement Send!");
        }

        internal ulong Ping
        {
            get
            {
                return 0;
            }
        }
    }
}
#endif