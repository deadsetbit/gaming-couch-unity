using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DSB.GC.Hud
{
    /// <summary>
    /// Configuration for the players hud.
    /// </summary>
    /// <param name="valueType">This can be "pointsSmall", "text" or "lives". TODO: Document the valueType elsewhere and reference to it, as it's shared with the platform?</param>
    [Serializable]
    public struct GCHudPlayersConfig
    {
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
        public int playerId;
        public bool eliminated;
        public int placement;
        public string value;
    }

    [Serializable]
    public struct GCPlayersHudData
    {
        public GCPlayersHudDataPlayer[] players;
    }

    public class GCHud
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchSetupHud(string hudConfigJson);

        [DllImport("__Internal")]
        private static extern void GamingCouchUpdatePlayersHud(string playersHudDataJson);

        public void Setup(GCHudConfig playersHudData)
        {
            string playersHudDataJson = JsonUtility.ToJson(playersHudData);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchSetupHud(playersHudDataJson);
#endif
        }

        public void UpdatePlayers(GCPlayersHudData playersHudData)
        {
            string playersHudDataJson = JsonUtility.ToJson(playersHudData);
#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchUpdatePlayersHud(playersHudDataJson);
#endif
        }
    }
}
