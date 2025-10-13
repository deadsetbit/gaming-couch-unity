using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public struct GCPlayerOptions
    {
        public string type;
        public int playerId;
        public string name;
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

        public static GCPlayOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GCPlayOptions>(optionsJson);
        }
    }
}
