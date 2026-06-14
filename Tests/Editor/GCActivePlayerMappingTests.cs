using System;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCActivePlayerMappingTests
{
    [SetUp]
    public void SetUp()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 1.0);
        GCRuntimeMessageOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
    }

    [TearDown]
    public void TearDown()
    {
        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
    }

    [Test]
    public void FnvHashSortMatchesRequiredLocalSparseSeatFixture()
    {
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("111:8"), Is.EqualTo(426892096u));
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("111:1"), Is.EqualTo(577890667u));
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("111:3"), Is.EqualTo(611445905u));

        var mapping = GCActivePlayerMapping.Create(
            CreatePlayOptions(111, GCPlayerType.player, GCPlayerType.player, GCPlayerType.bot),
            CreateSeatIdentities(
                (1, "1", GCPlayerType.player, GCPlayerColor.blue),
                (3, "3", GCPlayerType.player, GCPlayerColor.green),
                (8, "8", GCPlayerType.bot, GCPlayerColor.brown)
            )
        );
        var gameFacingOptions = mapping.CreateGameFacingPlayOptions();

        Assert.That(mapping.GetByPlayerIndex(0).SourceSeatIndex, Is.EqualTo(8));
        Assert.That(mapping.GetByPlayerIndex(1).SourceSeatIndex, Is.EqualTo(1));
        Assert.That(mapping.GetByPlayerIndex(2).SourceSeatIndex, Is.EqualTo(3));
        Assert.That(gameFacingOptions.players[0].playerIndex, Is.EqualTo(0));
        Assert.That(gameFacingOptions.players[0].playerSeed, Is.GreaterThan(0));
        Assert.That(gameFacingOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(gameFacingOptions.players[0].color, Is.EqualTo(GCPlayerColor.brown.ToString()));
        Assert.That(gameFacingOptions.players[1].color, Is.EqualTo(GCPlayerColor.blue.ToString()));
        Assert.That(gameFacingOptions.players[2].color, Is.EqualTo(GCPlayerColor.green.ToString()));
        var json = JsonUtility.ToJson(gameFacingOptions);
        Assert.That(json, Does.Contain("\"playerIndex\":0"));
        Assert.That(json, Does.Not.Contain("playerId"));
        Assert.That(json, Does.Not.Contain("sourceSeatIndex"));
        Assert.That(json, Does.Not.Contain("stableKey"));
    }

    [Test]
    public void FnvHashSortMatchesRequiredStableKeyFixture()
    {
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("424242:player-d"), Is.EqualTo(264058611u));
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("424242:player-a"), Is.EqualTo(314391468u));
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("424242:player-c"), Is.EqualTo(347946706u));
        Assert.That(GCActivePlayerMapping.ComputeFnv1A32("424242:player-b"), Is.EqualTo(364724325u));

        var mapping = GCActivePlayerMapping.Create(
            CreatePlayOptions(424242, GCPlayerType.player, GCPlayerType.player, GCPlayerType.player, GCPlayerType.player),
            CreateSeatIdentities(
                (1, "player-a", GCPlayerType.player, GCPlayerColor.blue),
                (2, "player-b", GCPlayerType.player, GCPlayerColor.red),
                (3, "player-c", GCPlayerType.player, GCPlayerColor.green),
                (4, "player-d", GCPlayerType.player, GCPlayerColor.yellow)
            )
        );

        Assert.That(mapping.GetByPlayerIndex(0).StableKey, Is.EqualTo("player-d"));
        Assert.That(mapping.GetByPlayerIndex(1).StableKey, Is.EqualTo("player-a"));
        Assert.That(mapping.GetByPlayerIndex(2).StableKey, Is.EqualTo("player-c"));
        Assert.That(mapping.GetByPlayerIndex(3).StableKey, Is.EqualTo("player-b"));
    }

    [Test]
    public void MappingTranslatesSparseSourceSeatsToPlayerIndices()
    {
        var mapping = GCActivePlayerMapping.Create(
            CreatePlayOptions(111, GCPlayerType.player, GCPlayerType.player, GCPlayerType.player),
            CreateSeatIdentities(
                (1, "1", GCPlayerType.player, GCPlayerColor.blue),
                (3, "3", GCPlayerType.player, GCPlayerColor.green),
                (8, "8", GCPlayerType.player, GCPlayerColor.brown)
            )
        );

        Assert.That(mapping.TryGetPlayerIndexForSourceSeat(8, out var playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(0));
        Assert.That(mapping.TryGetPlayerIndexForSourceSeat(1, out playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(1));
        Assert.That(mapping.TryGetPlayerIndexForSourceSeat(3, out playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(2));
        Assert.That(mapping.TryGetPlayerIndexForSourceSeat(2, out playerIndex), Is.False);
    }

    [Test]
    public void MappingPreservesBlueCapturedColorWhenUnderlyingEnumValueIsZero()
    {
        var playOptions = CreatePlayOptions(111, GCPlayerType.player, GCPlayerType.player, GCPlayerType.player);
        for (var index = 0; index < playOptions.players.Length; index++)
        {
            playOptions.players[index].color = GCPlayerColor.red.ToString();
        }

        var mapping = GCActivePlayerMapping.Create(
            playOptions,
            CreateSeatIdentities(
                (1, "1", GCPlayerType.player, GCPlayerColor.blue),
                (3, "3", GCPlayerType.player, GCPlayerColor.green),
                (8, "8", GCPlayerType.player, GCPlayerColor.brown)
            )
        );

        var gameFacingOptions = mapping.CreateGameFacingPlayOptions();

        Assert.That(mapping.GetByPlayerIndex(1).SourceSeatIndex, Is.EqualTo(1));
        Assert.That(gameFacingOptions.players[1].color, Is.EqualTo(GCPlayerColor.blue.ToString()));
    }

    [Test]
    public void LegacyPlayJsonThrowsTargetedClientSdkTranslationError()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => GCPlayOptions.CreateFromJSON(
                "{\"seed\":424242,\"players\":[" +
                "{\"playerId\":10,\"playerSeed\":111111,\"name\":\"Alice\",\"type\":\"player\",\"color\":\"blue\"}," +
                "{\"playerId\":20,\"playerSeed\":222222,\"name\":\"Bob\",\"type\":\"bot\",\"color\":\"green\"}" +
                "]}"
            )
        );

        Assert.That(exception.Message, Does.Contain("Legacy play payloads containing players[]"));
        Assert.That(
            exception.Message,
            Does.Contain("client/SDK must translate legacy players[] payloads to activePlayers[] before invoking Unity")
        );
    }

    [Test]
    public void ActivePlayersJsonPreservesHostedBoundaryMappingWithoutPrivatePlatformIdentity()
    {
        var options = GCPlayOptions.CreateFromJSON(
            "{\"seed\":424242,\"activePlayers\":[" +
            "{\"playerIndex\":1,\"playerSeed\":333333,\"type\":\"player\",\"color\":\"blue\"}," +
            "{\"playerIndex\":0,\"playerSeed\":444444,\"type\":\"bot\",\"color\":\"green\"}" +
            "]}"
        );
        var json = JsonUtility.ToJson(options);

        Assert.That(options.players, Has.Length.EqualTo(2));
        Assert.That(options.usesMappedActivePlayers, Is.True);
        Assert.That(options.players[0].playerIndex, Is.EqualTo(1));
        Assert.That(options.players[1].playerIndex, Is.EqualTo(0));
        Assert.That(json, Does.Contain("\"playerIndex\":1"));
        Assert.That(json, Does.Contain("\"playerIndex\":0"));
        Assert.That(json, Does.Not.Contain("playerId"));

        var mapping = GCActivePlayerMapping.Create(
            options,
            CreateSeatIdentities(
                (1, "", GCPlayerType.player, GCPlayerColor.blue),
                (2, "", GCPlayerType.bot, GCPlayerColor.green)
            )
        );
        var gameFacingOptions = mapping.CreateGameFacingPlayOptions();

        Assert.That(mapping.GetByPlayerIndex(0).CapturedOrder, Is.EqualTo(1));
        Assert.That(mapping.GetByPlayerIndex(1).CapturedOrder, Is.EqualTo(0));
        Assert.That(gameFacingOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(gameFacingOptions.players[0].playerSeed, Is.EqualTo(444444));
        Assert.That(gameFacingOptions.players[0].color, Is.EqualTo(GCPlayerColor.green.ToString()));
        Assert.That(gameFacingOptions.players[1].type, Is.EqualTo(GCPlayerType.player.ToString()));
        Assert.That(gameFacingOptions.players[1].playerSeed, Is.EqualTo(333333));
        Assert.That(gameFacingOptions.players[1].color, Is.EqualTo(GCPlayerColor.blue.ToString()));
    }

    [Test]
    public void InvalidPlayerIndexEmitsMappingDiagnosticWithContext()
    {
        string emittedJson = null;
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += json => emittedJson = json;
        var mapping = GCActivePlayerMapping.Create(
            CreatePlayOptions(111, GCPlayerType.player),
            CreateSeatIdentities((1, "1", GCPlayerType.player, GCPlayerColor.blue))
        );

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        Assert.That(mapping.TryValidatePlayerIndex(9, "test_input", out _), Is.False);

        Assert.That(emittedJson, Does.Contain("\"name\":\"gc.mapping.invalid_player_index\""));
        Assert.That(emittedJson, Does.Contain("\"sourceArea\":\"mapping\""));
        Assert.That(emittedJson, Does.Contain("\"playerIndex\":9"));
        Assert.That(emittedJson, Does.Contain("\"mapping\""));
        Assert.That(emittedJson, Does.Contain("\"seed\":111"));
        Assert.That(emittedJson, Does.Contain("\"participantCount\":1"));
        Assert.That(emittedJson, Does.Contain("\"offendingReference\":\"test_input:playerIndex:9\""));
    }

    [Test]
    public void MappingDiagnosticContextDoesNotExposeStableKeys()
    {
        string emittedJson = null;
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += json => emittedJson = json;
        var mapping = GCActivePlayerMapping.Create(
            CreatePlayOptions(424242, GCPlayerType.player),
            CreateSeatIdentities((1, "local-stable-key-secret", GCPlayerType.player, GCPlayerColor.blue))
        );

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        Assert.That(mapping.TryValidatePlayerIndex(9, "test_input", out _), Is.False);

        Assert.That(emittedJson, Does.Contain("\"mappingId\":\"map-"));
        Assert.That(emittedJson, Does.Not.Contain("local-stable-key-secret"));
    }

    [Test]
    public void PlacementValidationAcceptsZeroIndexAndRejectsMissingDuplicateOrOutOfRangeIndices()
    {
        var mapping = GCActivePlayerMapping.Create(
            CreatePlayOptions(111, GCPlayerType.player, GCPlayerType.player),
            CreateSeatIdentities(
                (1, "1", GCPlayerType.player, GCPlayerColor.blue),
                (2, "2", GCPlayerType.player, GCPlayerColor.red)
            )
        );

        Assert.That(mapping.TryValidatePlacement(new[] { 0, 1 }, "placement"), Is.True);

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        Assert.That(mapping.TryValidatePlacement(new[] { 0, 0 }, "placement"), Is.False);

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        Assert.That(mapping.TryValidatePlacement(new[] { 0, 2 }, "placement"), Is.False);

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        Assert.That(mapping.TryValidatePlacement(new[] { 0 }, "placement"), Is.False);
    }

    private static GCPlayOptions CreatePlayOptions(int seed, params GCPlayerType[] playerTypes)
    {
        var players = new GCActivePlayerOptions[playerTypes.Length];
        for (var index = 0; index < playerTypes.Length; index++)
        {
            players[index] = new GCActivePlayerOptions
            {
                playerIndex = index,
                type = playerTypes[index].ToString(),
                color = GCPlayerColor.blue.ToString(),
            };
        }

        return new GCPlayOptions
        {
            players = players,
            seed = seed,
        };
    }

    private static GCSeatIdentity[] CreateSeatIdentities(
        params (int sourceSeatIndex, string stableKey, GCPlayerType playerType, GCPlayerColor playerColor)[] source
    )
    {
        var identities = new GCSeatIdentity[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            identities[index] = new GCSeatIdentity
            {
                sourceSeatIndex = source[index].sourceSeatIndex,
                stableKey = source[index].stableKey,
                label = "Seat " + source[index].sourceSeatIndex,
                playerType = source[index].playerType,
                playerColor = source[index].playerColor,
            };
        }

        return identities;
    }
}
