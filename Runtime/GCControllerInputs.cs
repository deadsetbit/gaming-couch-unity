using UnityEngine;

namespace DSB.GC
{
    public class GCControllerInputs
    {
        public GCControllerInputs(GCControllerInputsData data)
        {
            this.data = data;
        }

        private GCControllerInputsData data;

        public static GCControllerInputs CreateFromJSON(string inputsDataJson)
        {
            return new GCControllerInputs(GCControllerInputsData.CreateFromJSON(inputsDataJson));
        }

        /**
         * The left stick X-axis or DPad left/right.
         * -1.0 is left
         * 1.0 is right
         **/
        public float leftX => data.b14 == 1 ? -1.0f : (data.b15 == 1 ? 1.0f : data.a0);
        /**
         * The left stick Y-axis or DPad up/down.
         * -1.0 is down
         * 1.0 is up
         **/
        public float leftY => data.b12 == 1 ? 1.0f : (data.b13 == 1 ? -1.0f : data.a1);
        /**
         * The right stick X-axis or DPad left/right.
         * -1.0 is left
         * 1.0 is right
         **/
        public float rightX => data.a2;
        /**
         * The right stick Y-axis or DPad up/down.
         * -1.0 is down
         * 1.0 is up
         **/
        public float rightY => data.a3;
        /**
         * Primary action button. Represents the A button on an Xbox controller layout.
         **/
        public bool primary => data.b0 == 1;
        /**
         * Secondary action button. Represents the B button on an Xbox controller layout.
         **/
        public bool secondary => data.b1 == 1;
        /**
         * A special button that should not be used for basic game mechanics,
         * (8such as combat) due to it's accessibility in touch screen controller.
         *
         * This button can instead be used for actions such as "reset player" in case player is stuck,
         * or something else that is not commonly required in the heat of the moment.
         *
         * Games should be primarily designed to work without this button.
         **/
        public bool alt => data.b2 == 1;
    }
}
