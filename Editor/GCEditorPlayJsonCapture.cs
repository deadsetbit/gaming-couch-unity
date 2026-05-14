using System;
using DSB.GC;
using DSB.GC.Dev;
using UnityEngine;

internal static class GCEditorPlayJsonCapture
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

    internal static GCEditorPlayCaptureResult Capture()
    {
        return Capture(new GCDevJsonStore().Read());
    }

    internal static GCEditorPlayPreflightResult ValidateRootJson(GCEditorPlayPreflightContext context)
    {
        GCDevJsonReadResult readResult;
        try
        {
            readResult = new GCDevJsonStore().Read();
        }
        catch (Exception exception)
        {
            return FailedForException(context, exception);
        }

        if (readResult != null && readResult.IsValid)
        {
            return GCEditorPlayPreflightResult.Succeeded();
        }

        return GCEditorPlayPreflightResult.Failed(
            GCEditorPlayPreflight.GetBoundaryDisplayName(context) + " blocked because root gc.dev.json is missing, invalid, or rejected by valid gc.metadata.json gates.",
            GetPath(readResult),
            readResult != null ? readResult.validation : GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                GCDevJsonIssueCode.ReadError,
                "gc.dev.json could not be read because the read result was missing.",
                null
            ))
        );
    }

    internal static GCEditorPlayCaptureResult Capture(GCDevJsonReadResult readResult)
    {
        if (readResult == null)
        {
            return Failed(
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
            return Failed(GetPath(readResult), readResult.validation);
        }

        var data = readResult.data;
        int seed;
        if (!TryResolveSeed(data.seed, out seed))
        {
            return Failed(
                GetPath(readResult),
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                    GCDevJsonIssueCode.InvalidSeed,
                    "gc.dev.json seed must be \"random\" or an integer string from " + GCDevJsonFile.MinSeed + " to " + GCDevJsonFile.MaxSeed + ".",
                    GetPath(readResult)
                ))
            );
        }

        var setupOptions = CreateSetupOptions(data);
        var playCapture = CreatePlayCapture(data, seed);
        playCapture.success = true;
        playCapture.setupOptions = setupOptions;
        playCapture.validation = readResult.validation;
        playCapture.path = GetPath(readResult);
        return playCapture;
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

    private static GCEditorPlayCaptureResult CreatePlayCapture(GCDevJsonFile data, int seed)
    {
        var activePlayerCount = data.EnabledSeatCount;
        var options = new GCPlayOptions
        {
            players = new GCPlayerOptions[activePlayerCount],
            seed = seed,
        };
        var seatIdentities = new GCSeatIdentity[activePlayerCount];

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

        return new GCEditorPlayCaptureResult
        {
            playOptions = options,
            seatIdentities = seatIdentities,
        };
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

    private static GCEditorPlayPreflightResult FailedForException(GCEditorPlayPreflightContext context, Exception exception)
    {
        var message = GCEditorPlayPreflight.GetBoundaryDisplayName(context) + " blocked because gc.dev.json preflight failed: " + exception.Message;
        return GCEditorPlayPreflightResult.Failed(
            message,
            null,
            GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.ReadError, message, null))
        );
    }

    private static string GetPath(GCDevJsonReadResult readResult)
    {
        return readResult?.parsedFile != null ? readResult.parsedFile.path : null;
    }
}
