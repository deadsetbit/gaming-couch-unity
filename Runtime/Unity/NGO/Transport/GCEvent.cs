using Unity.Netcode;

namespace DSB.GC.Unity.NGO.Transport
{
    public class GCEvent
    {
        public enum GCEventType
        {
            Nothing,
            Open,
            Payload,
        }

        public GCEventType Type;
        public ulong ClientId;
        public byte[] Payload;

        public NetworkEvent GetNetworkEvent()
        {
            switch (Type)
            {
                case GCEventType.Payload:
                    return NetworkEvent.Data;
                case GCEventType.Nothing:
                    return NetworkEvent.Nothing;
                case GCEventType.Open:
                    return NetworkEvent.Connect;
            }

            return NetworkEvent.Nothing;
        }
    }
}
