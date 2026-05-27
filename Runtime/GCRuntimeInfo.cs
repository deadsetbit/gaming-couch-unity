using System;

namespace DSB.GC
{
    [Serializable]
    internal sealed class GCRuntimeInfo
    {
        public string platform;
        public string packageName;
        public string packageVersion;
        public int gameProtocolVersion;
    }
}
