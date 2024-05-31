using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public class GamingCouchSetupOptions
    {
        public string gameModeId;

        public static GamingCouchSetupOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GamingCouchSetupOptions>(optionsJson);
        }
    }
}
