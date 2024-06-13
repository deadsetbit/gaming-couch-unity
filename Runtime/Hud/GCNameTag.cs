using UnityEngine;

namespace DSB.GC.Hud
{
    public class GCNameTag : MonoBehaviour
    {
        private int playerId = -1;

        [SerializeField]
        [Tooltip("Offset X from the player position in screen space. Percentage 0-1.")]
        private float offsetX = 0;

        [SerializeField]
        [Tooltip("Offset Y from the player position in screen space. Percentage 0-1.")]
        private float offsetY = 0;

        private IGCPlayer FindPlayerComponent()
        {
            var player = GetComponent<IGCPlayer>();
            if (player != null)
            {
                return player;
            }

            player = GetComponentInParent<IGCPlayer>();
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
                Debug.LogError("GCNameTag: Player id not set. Attach GCNameTag to a player (IGCPlayer), have it as a child or set player id manually via GCNameTag.SetPlayerId before Start.");
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
            screenPosition += new Vector3(offsetX * Screen.width, offsetY * Screen.height, 0);

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