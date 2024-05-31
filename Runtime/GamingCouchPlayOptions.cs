using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public struct PlayerOptions
    {
        public int playerId;
        public string name;
        public string color;
    }

    [System.Serializable]
    public class GamingCouchPlayOptions
    {
        public PlayerOptions[] players;

        public static GamingCouchPlayOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GamingCouchPlayOptions>(optionsJson);
        }
    }
}
