using System;
using System.Collections.Generic;

namespace DSB.GC.Game
{
    public class GCGameVersusSetupOptions : GCGameSetupOptions { }

    public class GCGameVersus : GCGame
    {
        public GCGameVersus(GCGameVersusSetupOptions options) : base(options) { }
    }
}
