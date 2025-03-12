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

        public static GCPlayOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GCPlayOptions>(optionsJson);
        }
    }
}
