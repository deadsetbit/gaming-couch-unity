using System;
using System.Collections.Generic;

namespace DSB.GC.Game
{
    public class GCGameVersusSetupOptions : GCGameSetupOptions { }

    public class GCGameVersus : GCGame
    {
        private GCGameVersusSetupOptions options;

        public GCGameVersus(GCGameVersusSetupOptions options) : base(options)
        {
            options = options;
        }
    }
}
