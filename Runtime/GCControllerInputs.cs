using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]
    public class GCControllerInputs
    {
        /// <summary>
        /// Left stick X-axis
        /// </summary>
        public float lx; // Left stick X-axis
        /// <summary>
        /// Left stick Y-axis
        /// </summary>
        public float ly; // Left stick Y-axis
        /// <summary>
        /// Right stick X-axis
        /// </summary>
        public float rx; // Right stick X-axis
        /// <summary>
        /// Right stick Y-axis
        /// </summary>
        public float ry; // Right stick Y-axis
        /// <summary>
        /// Button 1, Would match A on Xbox controller
        /// </summary>
        public int b1;
        /// <summary>
        /// Button 2, Would match B on Xbox controller
        /// </summary>
        public int b2;
        /// <summary>
        /// Button 3, Would match X on Xbox controller
        /// </summary>
        public int b3;
        /// <summary>
        /// Button 4, Would match Y on Xbox controller
        /// </summary>
        public int b4;

        public static GCControllerInputs CreateFromJSON(string inputsJson)
        {
            return JsonUtility.FromJson<GCControllerInputs>(inputsJson);
        }
    }
}
