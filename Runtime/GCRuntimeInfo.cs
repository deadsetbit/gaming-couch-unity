using System;
using UnityEngine;

namespace DSB.GC
{
    [Serializable]
    internal sealed class GCRuntimeInfo
    {
        internal const string PackageName = "com.dsb.gamingcouch";
        internal const string PackageVersion = "0.1.0-alpha.3";
        internal const int GameProtocolVersion = 1;
        internal const string Platform = "unity";

        public string platform;
        public string packageName;
        public string packageVersion;
        public int gameProtocolVersion;

        internal static GCRuntimeInfo Create()
        {
            return new GCRuntimeInfo
            {
                platform = Platform,
                packageName = PackageName,
                packageVersion = PackageVersion,
                gameProtocolVersion = GameProtocolVersion,
            };
        }

        internal static string ToJson()
        {
            return JsonUtility.ToJson(Create());
        }
    }
}
