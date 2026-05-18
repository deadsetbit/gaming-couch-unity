using UnityEditor;
using UnityEngine;

public class WebBuildOptimizer
{
        [MenuItem("GamingCouch/WebGL Build/Apply release build settings (slow build)")]
        public static void ApplyReleaseBuildSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenReleaseProfile();
        }


        [MenuItem("GamingCouch/WebGL Build/Apply dev build settings (fast build)")]
        public static void ApplyDevBuildSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenDevProfile();
        }

        [MenuItem("GamingCouch/WebGL Build/Setup clean WebGL export")]
        public static void SetupCleanWebGLExport()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenCleanExport(null);
        }
}
