using UnityEditor;
using UnityEngine;

public class WebBuildOptimizer
{
        private const string CleanExportSetupDialogTitle = "GamingCouch Clean WebGL Export Setup";

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

        [MenuItem("GamingCouch/WebGL Build/Setup clean WebGL export")]
        public static void SetupCleanWebGLExport()
        {
                var result = GamingCouchWebGLExportSetup.EnsureCleanWebGLExportSetup();
                var feedback = FormatSetupFeedback(result.message, result.details);

                if (result.IsBlocked)
                {
                        Debug.LogError(feedback);
                        EditorUtility.DisplayDialog(CleanExportSetupDialogTitle, feedback, "OK");
                        return;
                }

                if (result.HasWarning)
                {
                        Debug.LogWarning(feedback);
                        EditorUtility.DisplayDialog(CleanExportSetupDialogTitle, feedback, "OK");
                        return;
                }

                if (result.changed)
                {
                        Debug.Log(feedback);
                }
        }

        private static string FormatSetupFeedback(string message, string[] details)
        {
                if (details == null || details.Length == 0)
                {
                        return message;
                }

                return message + "\n\n" + string.Join("\n", details);
        }
}
