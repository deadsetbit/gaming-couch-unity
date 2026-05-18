using UnityEditor;

public static class GamingCouchWebGLBuildMenu
{
        [MenuItem("GamingCouch/WebGL Build/Preview release build settings (slow build)")]
        public static void PreviewReleaseBuildSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenReleaseProfile();
        }


        [MenuItem("GamingCouch/WebGL Build/Preview dev build settings (fast build)")]
        public static void PreviewDevBuildSettings()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenDevProfile();
        }

        [MenuItem("GamingCouch/WebGL Build/Preview clean WebGL export setup")]
        public static void PreviewCleanWebGLExportSetup()
        {
                GamingCouchWebGLBuildSettingsPreviewWindow.OpenCleanExport(null);
        }
}
