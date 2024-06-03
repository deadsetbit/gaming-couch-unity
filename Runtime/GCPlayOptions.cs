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
    public class GCPlayOptions
    {
        public PlayerOptions[] players;

        public static GCPlayOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GCPlayOptions>(optionsJson);
        }
    }
}
