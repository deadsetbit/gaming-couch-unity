using System;
using System.Collections.Generic;

namespace DSB.GC.Dev
{
    internal struct GCEditorPlayPlayerSettings
    {
        public string name;
        public GCPlayerColor color;
        public bool isBot;
        public int sourceSeatIndex;
    }

    internal struct GCEditorPlaySettingsSnapshot
    {
        public string gameModeId;
        public GCEditorPlayPlayerSettings[] playerData;
        public int numberOfPlayers;
        public bool randomizePlayerIds;
    }

    internal struct GCEditorPlayCaptureResult
    {
        public GCPlayOptions playOptions;
        public GCSeatIdentity[] seatIdentities;
    }

    internal static class GCEditorPlayCapture
    {
        internal static GCSetupOptions CreateSetupOptions(GCEditorPlaySettingsSnapshot snapshot)
        {
            return new GCSetupOptions
            {
                isServer = true,
                gameModeId = snapshot.gameModeId,
                mode = GCMode.Development,
            };
        }

        internal static GCPlayOptions CreatePlayOptions(GCEditorPlaySettingsSnapshot snapshot)
        {
            return CreatePlayCapture(snapshot).playOptions;
        }

        internal static GCEditorPlayCaptureResult CreatePlayCapture(GCEditorPlaySettingsSnapshot snapshot)
        {
            GCPlayOptions options = new GCPlayOptions
            {
                players = new GCPlayerOptions[snapshot.numberOfPlayers],
                seed = UnityEngine.Random.Range(1, 999999),
            };
            var seatIdentities = new GCSeatIdentity[snapshot.numberOfPlayers];

            var usedColors = new List<GCPlayerColor>();

            for (int i = 0; i < snapshot.numberOfPlayers; i++)
            {
                var player = snapshot.playerData[i];
                if (usedColors.Contains(player.color))
                {
                    throw new Exception("[GamingCouch] Player color '" + player.color + "' set more than once in GamingCouch 'playerData'. Make sure to use unique colors for each player.");
                }

                usedColors.Add(player.color);

                var sourceSeatIndex = ResolveSourceSeatIndex(player.sourceSeatIndex, i);
                var playerType = player.isBot ? GCPlayerType.bot : GCPlayerType.player;
                var playerId = snapshot.randomizePlayerIds ? UnityEngine.Random.Range(1, 99) : i + 1;

                options.players[i] = new GCPlayerOptions
                {
                    type = playerType.ToString(),
                    playerId = playerId,
                    name = player.name,
                    color = player.color.ToString(),
                };

                seatIdentities[i] = new GCSeatIdentity
                {
                    playerId = playerId,
                    sourceSeatIndex = sourceSeatIndex,
                    label = "Seat " + sourceSeatIndex,
                    playerType = playerType,
                    playerColor = player.color,
                };
            }

            return new GCEditorPlayCaptureResult
            {
                playOptions = options,
                seatIdentities = seatIdentities,
            };
        }

        private static int ResolveSourceSeatIndex(int sourceSeatIndex, int activePlayerIndex)
        {
            return sourceSeatIndex > 0 ? sourceSeatIndex : activePlayerIndex + 1;
        }
    }
}
