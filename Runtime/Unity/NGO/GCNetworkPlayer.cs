#if GC_UNITY_NETCODE_GAMEOBJECTS
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UIElements;

namespace DSB.GC.Unity.NGO
{
    [RequireComponent(typeof(GCPlayer))]
    [RequireComponent(typeof(NetworkObject))]
    public class GCNetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<uint> netPlayerId = new NetworkVariable<uint>(0);
        public NetworkVariable<bool> isEliminated = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> isFinished = new NetworkVariable<bool>(false);
        public NetworkVariable<int> score = new NetworkVariable<int>(0);
        public NetworkVariable<int> lives = new NetworkVariable<int>(0);
        public NetworkVariable<GCPlayerStatus> status = new NetworkVariable<GCPlayerStatus>(0);
        public NetworkVariable<GCPlayerType> playerType = new NetworkVariable<GCPlayerType>(0);
        private GCPlayer player;
        public GCPlayer Player => player;

        private static string TEMP_REASON_NOT_SYNCED = "not available (not synced over net)";

        protected virtual void Awake()
        {
            player = GetComponent<GCPlayer>();
            isEliminated.Value = player.IsEliminated;
            Debug.Log("debug - Player Awake: " + netPlayerId.Value);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                StateSyncServer();
            }
            else
            {
                StateSyncClient();
            }
        }

        private void StateSyncClient()
        {
            var playerOptions = GamingCouch.Instance.GetPlayerOptions((int)netPlayerId.Value);
            GamingCouch.Instance._InternalSetPlayerProperties(player, playerOptions);

            isEliminated.OnValueChanged += (oldValue, newValue) =>
            {
                if (oldValue == newValue)
                {
                    return;
                }

                if (newValue)
                {
                    player.SetEliminated(TEMP_REASON_NOT_SYNCED);
                }
                else
                {
                    player.SetUneliminated(TEMP_REASON_NOT_SYNCED);
                }
            };

            isFinished.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetFinished(TEMP_REASON_NOT_SYNCED);
            };

            score.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetScore(newValue, TEMP_REASON_NOT_SYNCED);
            };

            lives.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetLives(newValue, TEMP_REASON_NOT_SYNCED);
            };


            status.OnValueChanged += (oldValue, newValue) =>
            {
                player.SetStatus(newValue, "TODO", TEMP_REASON_NOT_SYNCED);
            };
        }

        private void StateSyncServer()
        {
            // init the network values
            var id = player.Id;
            Debug.Assert(id > 0, "Player ID not set");
            netPlayerId.Value = (uint)id;

            isEliminated.Value = player.IsEliminated;
            isFinished.Value = player.IsFinished;
            score.Value = player.Score;
            lives.Value = player.Lives;
            status.Value = player.Status;
            playerType.Value = player.PlayerType;

            // sync basic GCPlayer state changes by default
            player.OnEliminated += reason => isEliminated.Value = true;
            player.OnUneliminated += reason => isEliminated.Value = false;
            player.OnFinished += reason => isFinished.Value = true;
            player.OnScoreChanged += (playerId, score, reason) =>
            {
                this.score.Value = score;
            };
            player.OnLivesChanged += (playerId, lives, reason) =>
            {
                this.lives.Value = lives;
            };
            player.OnStatusChanged += (status, statusText, reason) =>
            {
                this.status.Value = status;
            };
        }
    }
}
#endif