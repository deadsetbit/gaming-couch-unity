using UnityEngine;

namespace DSB.GC
{
    [System.Serializable]

    /// <summary>
    /// The input namings and indexes are following on Gamepad API standard.
    /// eg.
    /// a0 = axis[0] // This is expected to be "Left stick X-axis", but it may vary depending on browser, controller, and OS.
    /// b0 = button[0] // This is expected to be "A button" on xbox controller, but it may vary depending on browser, controller, and OS.
    /// </summary>
    public class GCControllerInputsData
    {
        /// <summary>
        /// Left stick X-axis
        /// </summary>
        public float a0;
        /// <summary>
        /// Left stick Y-axis
        /// </summary>
        public float a1;
        /// <summary>
        /// Right stick X-axis
        /// </summary>
        public float a2;
        /// <summary>
        /// Right stick Y-axis
        /// </summary>
        public float a3;
        /// <summary>
        /// Button index 0 - Would match A on Xbox controller
        /// </summary>
        public int b0;
        /// <summary>
        /// Button index 1 - Would match B on Xbox controller
        /// </summary>
        public int b1;
        /// <summary>
        /// Button index 2 - Would match X on Xbox controller
        /// </summary>
        public int b2;
        /// <summary>
        /// Button index 3 - Would match Y on Xbox controller
        /// </summary>
        public int b3;
        /// <summary>
        /// DPad up
        /// </summary>
        public int b12;
        /// <summary>
        /// DPad bottom
        /// </summary>
        public int b13;
        /// <summary>
        /// DPad left
        /// </summary>
        public int b14;
        /// <summary>
        /// DPad right
        /// </summary>
        public int b15;

        public static GCControllerInputsData CreateFromJSON(string inputsDataJson)
        {
            return JsonUtility.FromJson<GCControllerInputsData>(inputsDataJson);
        }
    }
}
