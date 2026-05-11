using UnityEditor;

public class WebBuildOptimizer
{
        [MenuItem("GamingCouch/WebGL Build/Apply release build settings (slow build)")]
        public static void ApplyReleaseBuildSettings()
        {
                GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile();
        }


        [MenuItem("GamingCouch/WebGL Build/Apply dev build settings (fast build)")]
        public static void ApplyDevBuildSettings()
        {
                GamingCouchWebGLBuildSettingsProfiles.ApplyDevProfile();
        }

}
