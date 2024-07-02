using UnityEngine;

namespace DSB.GC.Hud
{
    public class GCNameTag : MonoBehaviour
    {
        private int playerId = -1;

        private GCPlayer FindPlayerComponent()
        {
            var player = GetComponent<GCPlayer>();
            if (player != null)
            {
                return player;
            }

            player = GetComponentInParent<GCPlayer>(true);
            if (player != null)
            {
                return player;
            }

            return null;
        }

        private void Start()
        {
            if (playerId == -1)
            {
                var player = FindPlayerComponent();
                if (player != null)
                {
                    playerId = player.GetId();
                }
            }

            if (playerId == -1)
            {
                Debug.LogError("GCNameTag: Player id not set. Attach GCNameTag to a player (GCPlayer), have it as a child or set player id manually via GCNameTag.SetPlayerId before Start.");
                return;
            }
        }

        public void SetPlayerId(int playerId)
        {
            this.playerId = playerId;
        }

        private void Update()
        {
            if (playerId == -1) return;

            var screenPosition = GamingCouch.Instance.Hud.Camera.WorldToScreenPoint(transform.position);

            var inScreen = screenPosition.z > 0 && screenPosition.x >= 0 && screenPosition.x <= Screen.width && screenPosition.y >= 0 && screenPosition.y <= Screen.height;
            if (!inScreen) return;

            GamingCouch.Instance.Hud.QueuePointData(new GCScreenPointDataPoint
            {
                type = "name",
                playerId = playerId,
                x = screenPosition.x / Screen.width,
                y = screenPosition.y / Screen.height
            });
        }
    }
}