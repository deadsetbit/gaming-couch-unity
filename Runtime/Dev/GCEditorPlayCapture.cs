using System;
using System.Collections.Generic;

namespace DSB.GC.Dev
{
    internal struct GCEditorPlayPlayerSettings
    {
        public string name;
        public GCPlayerColor color;
        public bool isBot;
    }

    internal struct GCEditorPlaySettingsSnapshot
    {
        public string gameModeId;
        public GCEditorPlayPlayerSettings[] playerData;
        public int numberOfPlayers;
        public bool randomizePlayerIds;
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
            GCPlayOptions options = new GCPlayOptions
            {
                players = new GCPlayerOptions[snapshot.numberOfPlayers],
                seed = UnityEngine.Random.Range(1, 999999),
            };

            var usedColors = new List<GCPlayerColor>();

            for (int i = 0; i < snapshot.numberOfPlayers; i++)
            {
                var player = snapshot.playerData[i];
                if (usedColors.Contains(player.color))
                {
                    throw new Exception("[GamingCouch] Player color '" + player.color + "' set more than once in GamingCouch 'playerData'. Make sure to use unique colors for each player.");
                }

                usedColors.Add(player.color);

                options.players[i] = new GCPlayerOptions
                {
                    type = player.isBot ? GCPlayerType.bot.ToString() : GCPlayerType.player.ToString(),
                    playerId = snapshot.randomizePlayerIds ? UnityEngine.Random.Range(1, 99) : i + 1,
                    name = player.name,
                    color = player.color.ToString(),
                };
            }

            return options;
        }
    }
}
