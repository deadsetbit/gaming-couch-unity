#if GC_UNITY_NETCODE_GAMEOBJECTS
using UnityEngine;
using Unity.Netcode;

namespace DSB.GC.Unity.NGO
{
    [RequireComponent(typeof(GCPlayer))]
    [RequireComponent(typeof(NetworkObject))]
    public class GCNetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<uint> netPlayerId = new NetworkVariable<uint>(0);
        private GCPlayer player;
        public GCPlayer Player => player;

        protected virtual void Awake()
        {
            player = GetComponent<GCPlayer>();
            Debug.Log("debug - Player Awake: " + netPlayerId.Value);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                var id = player.Id;
                Debug.Assert(id > 0, "Player ID not set");
                netPlayerId.Value = (uint)id;
                Debug.Log("debug - Server: " + netPlayerId.Value);
            }
            else
            {
                Debug.Log("debug - Client: " + netPlayerId.Value);
                var playerOptions = GamingCouch.Instance.GetPlayerOptions((int)netPlayerId.Value);
                GamingCouch.Instance._InternalSetPlayerProperties(player, playerOptions);
            }
        }
    }
}
#endif