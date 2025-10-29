using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DSB.GC.Hud
{
    public enum PlayersHudValueType
    {
        None,
        PointsSmall,
        Status,
        Text,
        Lives
    }

    public enum PlayersHudMeterType
    {
        None,
        RedBar,
        OrangeBar,
        GreenBar,
        BlueBar,
    }

    /// <summary>
    /// Configuration for the players hud.
    /// </summary>
    [Serializable]
    public struct GCHudPlayersConfig : ISerializationCallbackReceiver
    {
        [NonSerialized]
        public PlayersHudValueType valueTypeEnum;
        [SerializeField]
        private string valueType;

        [NonSerialized]
        public PlayersHudMeterType meterTypeEnum;
        [SerializeField]
        private string meterType;

        public void OnBeforeSerialize()
        {
            var str = valueTypeEnum.ToString();
            valueType = char.ToLower(str[0]) + str[1..]; // set first letter to lowercase

            str = meterTypeEnum.ToString();
            meterType = char.ToLower(str[0]) + str[1..]; // set first letter to lowercase
        }

        public void OnAfterDeserialize()
        {
            valueTypeEnum = (PlayersHudValueType)Enum.Parse(typeof(PlayersHudValueType), valueType);
            meterTypeEnum = (PlayersHudMeterType)Enum.Parse(typeof(PlayersHudMeterType), meterType);
        }
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
        /// <summary>
        /// The meter value (-1-100) to display for the player. This can be health, boost, etc. based on the meterType in the GCHudPlayersConfig.
        /// </summary>
        public int meterValue;
    }

    [Serializable]
    public struct GCPlayersHudData
    {
        /// <summary>
        /// The players to display in the HUD.
        /// </summary>
        public GCPlayersHudDataPlayer[] players;
    }

    [Serializable]
    public struct GCScreenPointDataPoint
    {
        public string type;
        /// <summary>
        /// The player ID.
        /// </summary>
        public int playerId;
        /// <summary>
        /// The x position of the point in percentages eg. 0-1. Values outside this range are considered off screen but not disregarded.
        /// </summary>
        public float x;
        /// <summary>
        /// The y position of the point in percentages eg. 0-1. Values outside this range are considered off screen but not disregarded.
        /// </summary>
        public float y;
    }

    [Serializable]
    public struct GCScreenPointData
    {
        /// <summary>
        /// List of points to display in the HUD.
        /// </summary>
        public GCScreenPointDataPoint[] points;
    }

    public class GCHud
    {
        [DllImport("__Internal")]
        private static extern void GamingCouchSetupHud(string hudConfigJson);

        [DllImport("__Internal")]
        private static extern void GamingCouchUpdatePlayersHud(string playersHudDataJson);

        [DllImport("__Internal")]
        private static extern void GamingCouchUpdateScreenPointHud(string playersHudDataJson);

        private Camera camera = null;
        public Camera Camera
        {
            get
            {
                if (camera == null)
                {
                    camera = Camera.main;
                }

                return camera;
            }
        }

        /// <summary>
        /// Setup the HUD. Should be called once at the start of the game and before UpdatePlayers.
        /// </summary>
        public void Setup(GCHudConfig config)
        {
            string playersHudDataJson = JsonUtility.ToJson(config);
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

        public void UpdateScreenPointHud(GCScreenPointData pointData)
        {
            string screenPointHudDataJson = JsonUtility.ToJson(pointData);

#if UNITY_WEBGL && !UNITY_EDITOR
        GamingCouchUpdateScreenPointHud(screenPointHudDataJson);
#endif
        }

        private List<GCScreenPointDataPoint> pointDataQueue = new List<GCScreenPointDataPoint>();

        public void QueuePointData(GCScreenPointDataPoint pointData)
        {
            pointDataQueue.Add(pointData);
        }

        public void HandleQueue()
        {
            var pointData = new GCScreenPointData
            {
                points = pointDataQueue.ToArray()
            };
            UpdateScreenPointHud(pointData);
            pointDataQueue.Clear();
        }

        public void SetCamera(Camera camera)
        {
            this.camera = camera;
        }
    }
}
