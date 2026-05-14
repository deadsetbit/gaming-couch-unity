#if UNITY_EDITOR
using System;
using UnityEngine;

namespace DSB.GC.Dev
{
    internal enum GCLocalPlaySessionBoundary
    {
        UnityPlayModeEntry,
        GamingCouchRestart,
    }

    internal sealed class GCLocalPlaySessionCaptureResult
    {
        internal readonly bool success;
        internal readonly GCSetupOptions setupOptions;
        internal readonly GCPlayOptions playOptions;
        internal readonly GCSeatIdentity[] seatIdentities;
        internal readonly GCDevJsonValidationResult validation;
        internal readonly string path;

        private GCLocalPlaySessionCaptureResult(
            bool success,
            GCSetupOptions setupOptions,
            GCPlayOptions playOptions,
            GCSeatIdentity[] seatIdentities,
            GCDevJsonValidationResult validation,
            string path
        )
        {
            this.success = success;
            this.setupOptions = setupOptions;
            this.playOptions = playOptions;
            this.seatIdentities = seatIdentities ?? Array.Empty<GCSeatIdentity>();
            this.validation = validation;
            this.path = path;
        }

        internal static GCLocalPlaySessionCaptureResult Succeeded(
            GCSetupOptions setupOptions,
            GCPlayOptions playOptions,
            GCSeatIdentity[] seatIdentities,
            GCDevJsonValidationResult validation,
            string path
        )
        {
            return new GCLocalPlaySessionCaptureResult(true, setupOptions, playOptions, seatIdentities, validation, path);
        }

        internal static GCLocalPlaySessionCaptureResult Failed(string path, GCDevJsonValidationResult validation)
        {
            return new GCLocalPlaySessionCaptureResult(false, null, null, Array.Empty<GCSeatIdentity>(), validation, path);
        }
    }

    internal sealed class GCLocalPlaySessionPreflightResult
    {
        internal readonly bool success;
        internal readonly string message;
        internal readonly string path;
        internal readonly GCDevJsonValidationResult validation;

        private GCLocalPlaySessionPreflightResult(bool success, string message, string path, GCDevJsonValidationResult validation)
        {
            this.success = success;
            this.message = message;
            this.path = path;
            this.validation = validation;
        }

        internal static GCLocalPlaySessionPreflightResult Succeeded()
        {
            return new GCLocalPlaySessionPreflightResult(true, null, null, GCDevJsonValidationResult.Valid());
        }

        internal static GCLocalPlaySessionPreflightResult Failed(
            string message,
            string path,
            GCDevJsonValidationResult validation
        )
        {
            return new GCLocalPlaySessionPreflightResult(false, message, path, validation);
        }
    }

    internal static class GCLocalPlaySession
    {
        private static readonly GCPlayerColor[] SeatColors =
        {
            GCPlayerColor.blue,
            GCPlayerColor.red,
            GCPlayerColor.green,
            GCPlayerColor.yellow,
            GCPlayerColor.purple,
            GCPlayerColor.pink,
            GCPlayerColor.cyan,
            GCPlayerColor.brown,
        };

        private static Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> preflightHandler;
        private static Func<GCDevJsonReadResult> readHandler;
        private static Action captureSucceededHandler;
        private static GCLocalPlaySessionCaptureResult activeCapture;

        internal static void RegisterPreflightHandler(
            Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> handler
        )
        {
            preflightHandler = handler;
        }

        internal static void RegisterCaptureSucceededHandler(Action handler)
        {
            captureSucceededHandler = handler;
        }

        internal static bool CaptureForRuntimeEntry()
        {
            activeCapture = Capture();
            LogCaptureIssues(activeCapture);

            if (activeCapture.success)
            {
                NotifyCaptureSucceeded();
            }

            return activeCapture.success;
        }

        internal static bool CaptureForRestart()
        {
            return CaptureForRuntimeEntry();
        }

        internal static GCLocalPlaySessionCaptureResult Capture()
        {
            try
            {
                return Capture(ReadRootJson());
            }
            catch (Exception exception)
            {
                return FailedForCaptureException(exception);
            }
        }

        internal static GCLocalPlaySessionCaptureResult Capture(GCDevJsonReadResult readResult)
        {
            if (readResult == null)
            {
                return GCLocalPlaySessionCaptureResult.Failed(
                    null,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.ReadError,
                        "gc.dev.json could not be read because the read result was missing.",
                        null
                    ))
                );
            }

            if (!readResult.IsValid)
            {
                return GCLocalPlaySessionCaptureResult.Failed(GetPath(readResult), readResult.validation);
            }

            var data = readResult.data;
            int seed;
            if (!TryResolveSeed(data.seed, out seed))
            {
                return GCLocalPlaySessionCaptureResult.Failed(
                    GetPath(readResult),
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.InvalidSeed,
                        "gc.dev.json seed must be \"random\" or an integer string from " +
                        GCDevJsonFile.MinSeed + " to " + GCDevJsonFile.MaxSeed + ".",
                        GetPath(readResult)
                    ))
                );
            }

            var setupOptions = CreateSetupOptions(data);
            var playOptions = CreatePlayOptions(data, seed, out var seatIdentities);
            return GCLocalPlaySessionCaptureResult.Succeeded(
                setupOptions,
                playOptions,
                seatIdentities,
                readResult.validation,
                GetPath(readResult)
            );
        }

        internal static bool TryRequireCapturedSetupOptions(string source, out GCSetupOptions setupOptions)
        {
            if (HasUsableActiveCapture() && activeCapture.setupOptions != null)
            {
                setupOptions = activeCapture.setupOptions;
                return true;
            }

            setupOptions = null;
            Debug.LogError(
                "[GamingCouch] " + source +
                " blocked because root gc.dev.json did not produce valid editor setup options. Fix gc.dev.json and re-enter Play Mode."
            );
            return false;
        }

        internal static bool TryRequireCapturedPlayOptions(
            string source,
            out GCPlayOptions playOptions,
            out GCSeatIdentity[] seatIdentities
        )
        {
            if (HasUsableActiveCapture() && activeCapture.playOptions != null)
            {
                playOptions = activeCapture.playOptions;
                seatIdentities = activeCapture.seatIdentities;
                return true;
            }

            playOptions = null;
            seatIdentities = Array.Empty<GCSeatIdentity>();
            Debug.LogError(
                "[GamingCouch] " + source +
                " blocked because root gc.dev.json is missing, invalid, or rejected by valid gc.metadata.json gates. Fix gc.dev.json and re-enter Play Mode."
            );
            return false;
        }

        internal static bool TryRunRestartPreflight()
        {
            var result = RunPreflight(GCLocalPlaySessionBoundary.GamingCouchRestart);
            if (result.success)
            {
                return true;
            }

            LogBlockedBoundary(result);
            return false;
        }

        internal static GCLocalPlaySessionPreflightResult RunPreflight(GCLocalPlaySessionBoundary context)
        {
            if (preflightHandler != null)
            {
                try
                {
                    var result = preflightHandler(context);
                    if (result != null && !result.success)
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

        internal static GCLocalPlaySessionPreflightResult ValidateRootJson(GCLocalPlaySessionBoundary context)
        {
            GCDevJsonReadResult readResult;
            try
            {
                readResult = ReadRootJson();
            }
            catch (Exception exception)
            {
                return FailedForException(context, exception);
            }

            if (readResult != null && readResult.IsValid)
            {
                return GCLocalPlaySessionPreflightResult.Succeeded();
            }

            var message = GetBoundaryDisplayName(context) +
                          " blocked because root gc.dev.json is missing, invalid, or rejected by valid gc.metadata.json gates.";
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                GetPath(readResult),
                readResult != null
                    ? readResult.validation
                    : GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.ReadError,
                        "gc.dev.json could not be read because the read result was missing.",
                        null
                    ))
            );
        }

        internal static void LogBlockedBoundary(GCLocalPlaySessionPreflightResult result)
        {
            if (result == null)
            {
                return;
            }

            Debug.LogError("[GamingCouch] " + result.message);
            LogDevJsonIssues(result.validation);
        }

        internal static string GetBoundaryDisplayName(GCLocalPlaySessionBoundary context)
        {
            return context == GCLocalPlaySessionBoundary.GamingCouchRestart
                ? "Gaming Couch restart"
                : "Unity Play Mode entry";
        }

        internal static IDisposable OverrideForTests(
            Func<GCDevJsonReadResult> readHandlerOverride,
            Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> preflightHandlerOverride,
            Action captureSucceededHandlerOverride
        )
        {
            return new TestOverride(readHandlerOverride, preflightHandlerOverride, captureSucceededHandlerOverride);
        }

        internal static GCLocalPlaySessionCaptureResult GetActiveCaptureForTests()
        {
            return activeCapture;
        }

        private static bool HasUsableActiveCapture()
        {
            return activeCapture != null &&
                   activeCapture.success &&
                   activeCapture.setupOptions != null &&
                   activeCapture.playOptions != null;
        }

        private static GCSetupOptions CreateSetupOptions(GCDevJsonFile data)
        {
            return new GCSetupOptions
            {
                isServer = true,
                gameModeId = data.entryKey,
                mode = GCMode.Development,
            };
        }

        private static GCPlayOptions CreatePlayOptions(GCDevJsonFile data, int seed, out GCSeatIdentity[] seatIdentities)
        {
            var activePlayerCount = data.EnabledSeatCount;
            var options = new GCPlayOptions
            {
                players = new GCPlayerOptions[activePlayerCount],
                seed = seed,
            };
            seatIdentities = new GCSeatIdentity[activePlayerCount];

            var activePlayerIndex = 0;
            for (var sourceSeatIndex = 0; sourceSeatIndex < data.seats.Length; sourceSeatIndex++)
            {
                var seat = data.seats[sourceSeatIndex];
                if (!seat.enabled)
                {
                    continue;
                }

                var playerType = seat.isBot ? GCPlayerType.bot : GCPlayerType.player;
                var playerColor = SeatColors[sourceSeatIndex];
                var playerId = activePlayerIndex + 1;
                var oneBasedSourceSeatIndex = sourceSeatIndex + 1;

                options.players[activePlayerIndex] = new GCPlayerOptions
                {
                    type = playerType.ToString(),
                    playerId = playerId,
                    name = seat.name,
                    color = playerColor.ToString(),
                };

                seatIdentities[activePlayerIndex] = new GCSeatIdentity
                {
                    playerId = playerId,
                    sourceSeatIndex = oneBasedSourceSeatIndex,
                    label = "Seat " + oneBasedSourceSeatIndex,
                    playerType = playerType,
                    playerColor = playerColor,
                };

                activePlayerIndex++;
            }

            return options;
        }

        private static bool TryResolveSeed(string seed, out int value)
        {
            if (seed == GCDevJsonFile.RandomSeed)
            {
                value = UnityEngine.Random.Range(GCDevJsonFile.MinSeed, GCDevJsonFile.MaxSeed + 1);
                return true;
            }

            return int.TryParse(seed, out value) &&
                   value >= GCDevJsonFile.MinSeed &&
                   value <= GCDevJsonFile.MaxSeed;
        }

        private static GCDevJsonReadResult ReadRootJson()
        {
            return readHandler != null ? readHandler() : new GCDevJsonStore().Read();
        }

        private static GCLocalPlaySessionCaptureResult FailedForCaptureException(Exception exception)
        {
            var message = "gc.dev.json could not be read because editor JSON capture failed: " + exception.Message;
            return GCLocalPlaySessionCaptureResult.Failed(
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }

        private static GCLocalPlaySessionPreflightResult FailedForException(
            GCLocalPlaySessionBoundary context,
            Exception exception
        )
        {
            var message = GetBoundaryDisplayName(context) +
                          " blocked because gc.dev.json preflight failed: " + exception.Message;
            return GCLocalPlaySessionPreflightResult.Failed(
                message,
                null,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
            );
        }

        private static void NotifyCaptureSucceeded()
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

        private static void LogCaptureIssues(GCLocalPlaySessionCaptureResult capture)
        {
            if (capture == null)
            {
                return;
            }

            LogDevJsonIssues(capture.validation);

            if (!capture.success && !HasAnyIssues(capture.validation))
            {
                Debug.LogError("[GamingCouch] Editor play capture failed. Fix root gc.dev.json and re-enter Play Mode.");
            }
        }

        private static void LogDevJsonIssues(GCDevJsonValidationResult validation)
        {
            var issues = validation != null ? validation.issues : null;
            if (issues == null)
            {
                return;
            }

            for (var index = 0; index < issues.Length; index++)
            {
                var issue = issues[index];
                if (issue == null)
                {
                    continue;
                }

                if (issue.severity == GCDevJsonIssueSeverity.Warning)
                {
                    Debug.LogWarning("[GamingCouch] " + GCDevJsonIssueFormatter.Format(issue));
                }
                else
                {
                    Debug.LogError("[GamingCouch] " + GCDevJsonIssueFormatter.Format(issue));
                }
            }
        }

        private static bool HasAnyIssues(GCDevJsonValidationResult validation)
        {
            return validation != null && validation.issues != null && validation.issues.Length > 0;
        }

        private static string GetPath(GCDevJsonReadResult readResult)
        {
            return readResult != null && readResult.parsedFile != null ? readResult.parsedFile.path : null;
        }

        private sealed class TestOverride : IDisposable
        {
            private readonly Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> previousPreflightHandler;
            private readonly Func<GCDevJsonReadResult> previousReadHandler;
            private readonly Action previousCaptureSucceededHandler;
            private readonly GCLocalPlaySessionCaptureResult previousActiveCapture;
            private bool disposed;

            internal TestOverride(
                Func<GCDevJsonReadResult> readHandlerOverride,
                Func<GCLocalPlaySessionBoundary, GCLocalPlaySessionPreflightResult> preflightHandlerOverride,
                Action captureSucceededHandlerOverride
            )
            {
                previousPreflightHandler = preflightHandler;
                previousReadHandler = readHandler;
                previousCaptureSucceededHandler = captureSucceededHandler;
                previousActiveCapture = activeCapture;

                preflightHandler = preflightHandlerOverride;
                readHandler = readHandlerOverride;
                captureSucceededHandler = captureSucceededHandlerOverride;
                activeCapture = null;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                preflightHandler = previousPreflightHandler;
                readHandler = previousReadHandler;
                captureSucceededHandler = previousCaptureSucceededHandler;
                activeCapture = previousActiveCapture;
                disposed = true;
            }
        }
    }
}
#endif
