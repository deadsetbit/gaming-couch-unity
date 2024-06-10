using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DSB.GC.Hud
{
    /// <summary>
    /// Configuration for the players hud.
    /// </summary>
    [Serializable]
    public struct GCHudPlayersConfig
    {
        /// <summary>
        /// pointsSmall, text, lives
        /// </summary>
        public string valueType;
    }

    [Serializable]
    public struct GCHudConfig
    {
        public GCHudPlayersConfig players;
    }

    [Serializable]
    public struct GCPlayersHudDataPlayer
    {
        /// <summary>
        /// Gaming Couch player ID.
        /// </summary>
        public int playerId;
        /// <summary>
        /// If the player is out of the game, the HUD can be set to display this player as eliminated.
        /// </summary>
        public bool eliminated;
        /// <summary>
        /// The placement of the player. 0 is first place, 1 is second place, etc.
        /// Players in the HUD will be sorted based on this value to indicate placements at given time. (HUD sorting not yet implemented)
        /// </summary>
        public int placement;
        /// <summary>
        /// The value to display for the player. This can be points, lives, etc. based on the valueType in the GCHudPlayersConfig.
        /// </summary>
        public string value;
    }

    [Serializable]
    public struct GCPlayersHudData
    {
        /// <summary>
        /// The players to display in the HUD.
        /// </summary>
        public GCPlayersHudDataPlayer[] players;
    }

    public class GCHud
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchSetupHud(string hudConfigJson);

        [DllImport("__Internal")]
        private static extern void GamingCouchUpdatePlayersHud(string playersHudDataJson);

        /// <summary>
        /// Setup the HUD. Should be called once at the start of the game and before UpdatePlayers.
        /// </summary>
        public void Setup(GCHudConfig playersHudData)
        {
            string playersHudDataJson = JsonUtility.ToJson(playersHudData);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchSetupHud(playersHudDataJson);
#endif
        }

        /// <summary>
        /// Update the players in the HUD. Call Setup first.
        /// </summary>
        public void UpdatePlayers(GCPlayersHudData playersHudData)
        {
            string playersHudDataJson = JsonUtility.ToJson(playersHudData);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchUpdatePlayersHud(playersHudDataJson);
#endif
        }
    }
}
