using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public class GamingCouchControllerInputs
    {
        public float lx; // Left stick X-axis
        public float ly; // Left stick Y-axis
        public float rx; // Right stick X-axis
        public float ry; // Right stick Y-axis
        public int b1; // A
        public int b2; // B
        public int b3; // X
        public int b4; // Y

        public static GamingCouchControllerInputs CreateFromJSON(string inputsJson)
        {
            return JsonUtility.FromJson<GamingCouchControllerInputs>(inputsJson);
        }
    }
}
