using System.Runtime.InteropServices;
using UnityEngine;

namespace DSB.GC
{
    internal static class GCWebGLRuntimeInfoBootstrap
    {
        internal const string RuntimeInfoResourceName = "GamingCouchRuntimeInfo";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GamingCouchRegisterRuntimeInfo(string runtimeInfoJson);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void RegisterRuntimeInfoBeforeSplashScreen()
        {
            var runtimeInfoJson = LoadBakedRuntimeInfoJson();
            if (string.IsNullOrWhiteSpace(runtimeInfoJson))
            {
                Debug.LogError("Gaming Couch runtime info payload is missing.");
                return;
            }

            GamingCouchRegisterRuntimeInfo(runtimeInfoJson);
        }
#endif

        internal static string LoadBakedRuntimeInfoJson()
        {
            var runtimeInfo = Resources.Load<TextAsset>(RuntimeInfoResourceName);
            if (runtimeInfo == null)
            {
                return null;
            }

            return runtimeInfo.text.TrimEnd('\r', '\n');
        }
    }
}
