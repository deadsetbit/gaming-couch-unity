using UnityEngine;
using System;

namespace DSB.GC
{
    [System.Serializable]
    public struct GCPlayerOptions
    {
        public int playerIndex;
        public string type;
        public string color;
    }

    [System.Serializable]
    public class GCPlayOptions
    {
        public GCPlayerOptions[] players;
        /**
        * Value between 1-999999.
        *
        * Seed provided by the platform. This is unique for each round.
        * The seed can be used to generate random levels and such.
        * The idea is that the seed should always result in the same game.
        *
        * Use cases:
        * 1) for online multiplayer to generate levels or other
        * parts of the game that would require a lot of syncing over net when done one by one
        * (think level tiles, randomized atmosphere fx etc.).
        *
        * 2) potentially to generate repayable levels/games if we decide to allow players to define the seed in the future.
        */
        public int seed;

        [NonSerialized]
        internal GCPlayParticipantIdentity[] participantIdentities;

        [NonSerialized]
        internal bool usesMappedActivePlayers;

        public static GCPlayOptions CreateFromJSON(string optionsJson)
        {
            var transport = JsonUtility.FromJson<GCPlayOptionsTransport>(optionsJson);
            if (transport == null)
            {
                return null;
            }

            var players = transport.activePlayers;
            var usesMappedActivePlayers = players != null && players.Length > 0;
            if ((players == null || players.Length == 0) && transport.players != null)
            {
                players = new GCPlayerOptions[transport.players.Length];
                for (var index = 0; index < transport.players.Length; index++)
                {
                    players[index] = new GCPlayerOptions
                    {
                        playerIndex = index,
                        type = transport.players[index].type,
                        color = transport.players[index].color,
                    };
                }
            }

            return new GCPlayOptions
            {
                players = players,
                seed = transport.seed,
                participantIdentities = BuildParticipantIdentities(transport.players),
                usesMappedActivePlayers = usesMappedActivePlayers,
            };
        }

        private static GCPlayParticipantIdentity[] BuildParticipantIdentities(GCPlayerOptionsTransportPlayer[] players)
        {
            if (players == null || players.Length == 0)
            {
                return Array.Empty<GCPlayParticipantIdentity>();
            }

            var identities = new GCPlayParticipantIdentity[players.Length];
            for (var index = 0; index < players.Length; index++)
            {
                identities[index] = new GCPlayParticipantIdentity
                {
                    platformPlayerId = players[index].playerId,
                    stableKey = players[index].playerId > 0 ? players[index].playerId.ToString() : (index + 1).ToString(),
                };
            }

            return identities;
        }
    }

    internal struct GCPlayParticipantIdentity
    {
        internal int platformPlayerId;
        internal string stableKey;
    }

    [System.Serializable]
    internal sealed class GCPlayOptionsTransport
    {
        public GCPlayerOptions[] activePlayers;
        public GCPlayerOptionsTransportPlayer[] players;
        public int seed;
    }

    [System.Serializable]
    internal struct GCPlayerOptionsTransportPlayer
    {
        public string type;
        public int playerId;
        public string name;
        public string color;
    }
}
