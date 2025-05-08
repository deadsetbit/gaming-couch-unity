using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public class GCSetupOptions
    {
        public uint clientId;
        public bool isServer;
        public string gameModeId;

        public static GCSetupOptions CreateFromJSON(string optionsJson)
        {
            return JsonUtility.FromJson<GCSetupOptions>(optionsJson);
        }
    }
}
