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
                    playerId = player.Id;
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


        [Header("Debug")]
        [SerializeField]
        private bool drawDebugGizmo = true;
        private float debugGizmoWidth = 100.0f; // px
        private float debugGizmoHeight = 20.0f; // px
        private Color gizmoColor = Color.green;

        private void OnDrawGizmos()
        {
            // if (!drawDebugGizmo) return;
            // if (playerId == -1) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 screenPosition = camera.WorldToScreenPoint(transform.position);

            float halfWidth = debugGizmoWidth * 0.5f;

            Vector3 topLeftScreen = new Vector3(screenPosition.x - halfWidth, screenPosition.y + debugGizmoHeight, screenPosition.z);
            Vector3 topRightScreen = new Vector3(screenPosition.x + halfWidth, screenPosition.y + debugGizmoHeight, screenPosition.z);
            Vector3 bottomLeftScreen = new Vector3(screenPosition.x - halfWidth, screenPosition.y, screenPosition.z);
            Vector3 bottomRightScreen = new Vector3(screenPosition.x + halfWidth, screenPosition.y, screenPosition.z);

            Vector3 topLeftWorld = camera.ScreenToWorldPoint(topLeftScreen);
            Vector3 topRightWorld = camera.ScreenToWorldPoint(topRightScreen);
            Vector3 bottomLeftWorld = camera.ScreenToWorldPoint(bottomLeftScreen);
            Vector3 bottomRightWorld = camera.ScreenToWorldPoint(bottomRightScreen);

            Gizmos.color = gizmoColor;
            Gizmos.DrawLine(topLeftWorld, topRightWorld);
            Gizmos.DrawLine(topRightWorld, bottomRightWorld);
            Gizmos.DrawLine(bottomRightWorld, bottomLeftWorld);
            Gizmos.DrawLine(bottomLeftWorld, topLeftWorld);
        }
    }
}
