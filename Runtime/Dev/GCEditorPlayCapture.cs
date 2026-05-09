#if UNITY_EDITOR
using System;

namespace DSB.GC.Dev
{
    internal struct GCEditorPlayCaptureResult
    {
        public bool success;
        public GCSetupOptions setupOptions;
        public GCPlayOptions playOptions;
        public GCSeatIdentity[] seatIdentities;
        public GCDevJsonValidationResult validation;
        public string path;
    }

    internal static class GCEditorPlayCapture
    {
        private static Func<GCEditorPlayCaptureResult> captureHandler;

        internal static void RegisterCaptureHandler(Func<GCEditorPlayCaptureResult> handler)
        {
            captureHandler = handler;
        }

        internal static GCEditorPlayCaptureResult Capture()
        {
            if (captureHandler == null)
            {
                return Failed(
                    null,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.ReadError,
                        "gc.dev.json could not be read because the editor JSON capture handler is not available.",
                        null
                    ))
                );
            }

            try
            {
                var result = captureHandler();
                if (result.success)
                {
                    return result;
                }

                if (result.validation != null)
                {
                    return result;
                }
            }
            catch (Exception exception)
            {
                return FailedForException(exception);
            }

            return Failed(
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                    GCDevJsonIssueCode.ReadError,
                    "gc.dev.json could not be read because the editor JSON capture handler returned an invalid result.",
                    null
                ))
            );
        }

        private static GCEditorPlayCaptureResult Failed(string path, GCDevJsonValidationResult validation)
        {
            return new GCEditorPlayCaptureResult
            {
                success = false,
                setupOptions = null,
                playOptions = null,
                seatIdentities = Array.Empty<GCSeatIdentity>(),
                validation = validation,
                path = path,
            };
        }

        private static GCEditorPlayCaptureResult FailedForException(Exception exception)
        {
            var message = "gc.dev.json could not be read because editor JSON capture failed: " + exception.Message;
            return Failed(
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }
    }

    internal enum GCEditorPlayPreflightContext
    {
        UnityPlayModeEntry,
        GamingCouchRestart,
    }

    internal sealed class GCEditorPlayPreflightResult
    {
        internal readonly bool success;
        internal readonly string message;
        internal readonly string path;
        internal readonly GCDevJsonValidationResult validation;

        private GCEditorPlayPreflightResult(bool success, string message, string path, GCDevJsonValidationResult validation)
        {
            this.success = success;
            this.message = message;
            this.path = path;
            this.validation = validation;
        }

        internal static GCEditorPlayPreflightResult Succeeded()
        {
            return new GCEditorPlayPreflightResult(true, null, null, GCDevJsonValidationResult.Valid());
        }

        internal static GCEditorPlayPreflightResult Failed(string message, string path, GCDevJsonValidationResult validation)
        {
            return new GCEditorPlayPreflightResult(false, message, path, validation);
        }
    }

    internal static class GCEditorPlayPreflight
    {
        private static Func<GCEditorPlayPreflightContext, GCEditorPlayPreflightResult> preflightHandler;
        private static Func<GCEditorPlayPreflightContext, GCEditorPlayPreflightResult> rootValidationHandler;
        private static Action captureSucceededHandler;

        internal static void RegisterPreflightHandler(Func<GCEditorPlayPreflightContext, GCEditorPlayPreflightResult> handler)
        {
            preflightHandler = handler;
        }

        internal static void RegisterCaptureSucceededHandler(Action handler)
        {
            captureSucceededHandler = handler;
        }

        internal static void RegisterRootValidationHandler(Func<GCEditorPlayPreflightContext, GCEditorPlayPreflightResult> handler)
        {
            rootValidationHandler = handler;
        }

        internal static GCEditorPlayPreflightResult Run(GCEditorPlayPreflightContext context)
        {
            if (preflightHandler != null)
            {
                try
                {
                    var result = preflightHandler(context);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (Exception exception)
                {
                    return FailedForException(context, exception);
                }
            }

            return ValidateRootJson(context);
        }

        internal static GCEditorPlayPreflightResult ValidateRootJson(GCEditorPlayPreflightContext context)
        {
            if (rootValidationHandler != null)
            {
                try
                {
                    var result = rootValidationHandler(context);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (Exception exception)
                {
                    return FailedForException(context, exception);
                }
            }

            var message = GetBoundaryDisplayName(context) + " blocked because the editor JSON preflight handler is not available.";
            return GCEditorPlayPreflightResult.Failed(
                message,
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }

        internal static void NotifyCaptureSucceeded()
        {
            if (captureSucceededHandler == null)
            {
                return;
            }

            try
            {
                captureSucceededHandler();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[GamingCouch] Could not update editor JSON state after capture: " + exception.Message);
            }
        }

        internal static string GetBoundaryDisplayName(GCEditorPlayPreflightContext context)
        {
            return context == GCEditorPlayPreflightContext.GamingCouchRestart
                ? "Gaming Couch restart"
                : "Unity Play Mode entry";
        }

        private static GCEditorPlayPreflightResult FailedForException(GCEditorPlayPreflightContext context, Exception exception)
        {
            var message = GetBoundaryDisplayName(context) + " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCEditorPlayPreflightResult.Failed(
                message,
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }
    }
}
#endif
