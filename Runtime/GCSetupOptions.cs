using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public class GCSetupOptions
    {
        public string gameModeId;

        public static GCSetupOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GCSetupOptions>(optionsJson);
        }
    }
}
