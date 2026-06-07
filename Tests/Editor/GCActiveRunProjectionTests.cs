using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCActiveRunProjectionTests
{
    [TearDown]
    public void TearDown()
    {
        GCDevAppRuntimeOutputSettings.ResetForTests();
    }

    [Test]
    public void ActivePlayersProjectionCreatesGameFacingOptionsAndMappedFallbackSeatIdentities()
    {
        var options = new GCPlayOptions
        {
            seed = 123,
            players = new[]
            {
                new GCActivePlayerOptions
                {
                    playerIndex = 1,
                    type = GCPlayerType.player.ToString(),
                    color = GCPlayerColor.blue.ToString(),
                },
                new GCActivePlayerOptions
                {
                    playerIndex = 0,
                    type = GCPlayerType.bot.ToString(),
                    color = GCPlayerColor.green.ToString(),
                },
            },
            usesMappedActivePlayers = true,
        };

        var projection = GCActiveRunProjection.Create(options);

        Assert.That(projection.ActivePlayerMapping.GetByPlayerIndex(0).CapturedOrder, Is.EqualTo(1));
        Assert.That(projection.GameFacingPlayOptions.seed, Is.EqualTo(123));
        Assert.That(projection.GameFacingPlayOptions.players[0].playerIndex, Is.EqualTo(0));
        Assert.That(projection.GameFacingPlayOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[0].color, Is.EqualTo(GCPlayerColor.green.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[1].playerIndex, Is.EqualTo(1));
        Assert.That(projection.GameFacingPlayOptions.players[1].type, Is.EqualTo(GCPlayerType.player.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[1].color, Is.EqualTo(GCPlayerColor.blue.ToString()));

        Assert.That(projection.MappedSeatIdentities[0].sourceSeatIndex, Is.EqualTo(2));
        Assert.That(projection.MappedSeatIdentities[0].label, Is.EqualTo("Seat 2"));
        Assert.That(projection.MappedSeatIdentities[0].playerType, Is.EqualTo(GCPlayerType.bot));
        Assert.That(projection.MappedSeatIdentities[0].playerColor, Is.EqualTo(GCPlayerColor.green));
        Assert.That(projection.MappedSeatIdentities[1].sourceSeatIndex, Is.EqualTo(1));
        Assert.That(projection.MappedSeatIdentities[1].label, Is.EqualTo("Seat 1"));
        Assert.That(projection.MappedSeatIdentities[1].playerType, Is.EqualTo(GCPlayerType.player));
        Assert.That(projection.MappedSeatIdentities[1].playerColor, Is.EqualTo(GCPlayerColor.blue));
    }

    [Test]
    public void ExplicitSeatIdentitiesProjectionKeepsPrivateMappingAndCopiesPlatformData()
    {
        var platformData = CreatePlatformData();
        var options = new GCPlayOptions
        {
            seed = 111,
            players = new[]
            {
                CreatePlayer(GCPlayerType.player, GCPlayerColor.blue),
                CreatePlayer(GCPlayerType.player, GCPlayerColor.green),
                CreatePlayer(GCPlayerType.bot, GCPlayerColor.brown),
            },
            runtimeOutput = new GCRuntimeOutputOptions
            {
                stateSnapshots = false,
                screenSpace = false,
            },
            platformData = platformData,
        };
        var seatIdentities = new[]
        {
            CreateSeatIdentity(1, 11, "1", GCPlayerType.player, GCPlayerColor.blue),
            CreateSeatIdentity(3, 33, "3", GCPlayerType.player, GCPlayerColor.green),
            CreateSeatIdentity(8, 88, "8", GCPlayerType.bot, GCPlayerColor.brown),
        };

        var projection = GCActiveRunProjection.Create(options, seatIdentities);

        Assert.That(projection.ActivePlayerMapping.TryGetPlayerIndexForSourceSeat(8, out var playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(0));
        Assert.That(projection.ActivePlayerMapping.TryGetPlayerIndexForLegacyPlayerId(88, out playerIndex), Is.True);
        Assert.That(playerIndex, Is.EqualTo(0));
        Assert.That(projection.MappedSeatIdentities[0].sourceSeatIndex, Is.EqualTo(8));
        Assert.That(projection.MappedSeatIdentities[1].sourceSeatIndex, Is.EqualTo(1));
        Assert.That(projection.MappedSeatIdentities[2].sourceSeatIndex, Is.EqualTo(3));
        Assert.That(projection.GameFacingPlayOptions.players[0].type, Is.EqualTo(GCPlayerType.bot.ToString()));
        Assert.That(projection.GameFacingPlayOptions.players[0].color, Is.EqualTo(GCPlayerColor.brown.ToString()));
        Assert.That(projection.GameFacingPlayOptions.runtimeOutput.stateSnapshots, Is.False);
        Assert.That(projection.GameFacingPlayOptions.runtimeOutput.screenSpace, Is.False);
        Assert.That(projection.PlatformData, Is.SameAs(projection.GameFacingPlayOptions.platformData));
        Assert.That(projection.PlatformData, Is.Not.SameAs(platformData));

        platformData.game.key = "mutated";
        platformData.entries[0].entryKey = "mutated";
        platformData.playerColors.blue.@base[0] = 99;

        Assert.That(projection.PlatformData.game.key, Is.EqualTo("contract-game"));
        Assert.That(projection.PlatformData.entries[0].entryKey, Is.EqualTo("duel"));
        Assert.That(projection.PlatformData.playerColors.blue.@base, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void FallbackSeatIdentitiesUseLegacyParticipantIdentityForPrivateMappingOnly()
    {
        var options = GCPlayOptions.CreateFromJSON(
            "{\"seed\":424242,\"players\":[" +
            "{\"playerId\":10,\"name\":\"Alice\",\"type\":\"player\",\"color\":\"blue\"}," +
            "{\"playerId\":20,\"name\":\"Bob\",\"type\":\"bot\",\"color\":\"green\"}" +
            "]}"
        );

        var projection = GCActiveRunProjection.Create(options);

        Assert.That(projection.ActivePlayerMapping.TryGetPlayerIndexForLegacyPlayerId(10, out var playerIndex), Is.True);
        Assert.That(projection.ActivePlayerMapping.GetByPlayerIndex(playerIndex).StableKey, Is.EqualTo("10"));
        Assert.That(projection.MappedSeatIdentities[playerIndex].platformPlayerId, Is.EqualTo(10));
        Assert.That(projection.MappedSeatIdentities[playerIndex].sourceSeatIndex, Is.EqualTo(1));
        Assert.That(projection.MappedSeatIdentities[playerIndex].label, Is.EqualTo("Seat 1"));
        Assert.That(projection.MappedSeatIdentities[playerIndex].playerType, Is.EqualTo(GCPlayerType.player));
        Assert.That(projection.MappedSeatIdentities[playerIndex].playerColor, Is.EqualTo(GCPlayerColor.blue));

        var json = UnityEngine.JsonUtility.ToJson(projection.GameFacingPlayOptions);
        Assert.That(json, Does.Contain("\"playerIndex\""));
        Assert.That(json, Does.Not.Contain("playerId"));
        Assert.That(json, Does.Not.Contain("Alice"));
        Assert.That(json, Does.Not.Contain("Bob"));
    }

    [Test]
    public void ProjectionAppliesEditorRuntimeOutputOverride()
    {
        var options = new GCPlayOptions
        {
            seed = 123,
            players = new[]
            {
                CreatePlayer(GCPlayerType.player, GCPlayerColor.blue),
            },
            runtimeOutput = new GCRuntimeOutputOptions
            {
                unityLogCapture = GCRuntimeUnityLogCaptureMode.Full,
            },
        };
        GCDevAppRuntimeOutputSettings.SetUnityLogCaptureMode(GCRuntimeUnityLogCaptureMode.WarningAndError);

        var projection = GCActiveRunProjection.Create(options);

        Assert.That(
            projection.GameFacingPlayOptions.runtimeOutput.unityLogCapture,
            Is.EqualTo(GCRuntimeUnityLogCaptureMode.WarningAndError)
        );
    }

    private static GCActivePlayerOptions CreatePlayer(GCPlayerType playerType, GCPlayerColor playerColor)
    {
        return new GCActivePlayerOptions
        {
            type = playerType.ToString(),
            color = playerColor.ToString(),
        };
    }

    private static GCSeatIdentity CreateSeatIdentity(
        int sourceSeatIndex,
        int platformPlayerId,
        string stableKey,
        GCPlayerType playerType,
        GCPlayerColor playerColor
    )
    {
        return new GCSeatIdentity
        {
            sourceSeatIndex = sourceSeatIndex,
            platformPlayerId = platformPlayerId,
            stableKey = stableKey,
            label = "Seat " + sourceSeatIndex,
            playerType = playerType,
            playerColor = playerColor,
        };
    }

    private static GCPlatformRuntimeView CreatePlatformData()
    {
        var playerColors = GCPlatformRuntimePlayerColors.CreateDefault();
        playerColors.blue = new GCPlatformRuntimePlayerColor(
            new[] { 1, 2, 3 },
            new[] { 4, 5, 6 },
            new[] { 7, 8, 9 }
        );

        return GCPlatformRuntimeView.CreateValid(
            GCPlatformRuntimeSource.Valid(1, "/tmp/gc.platform.json"),
            new GCPlatformRuntimeGame("contract-game", "Contract Game"),
            new GCPlatformRuntimePlatform("unity"),
            "duel",
            new[]
            {
                new GCPlatformRuntimeEntry("duel", "Duel", 1, 2, true),
            },
            playerColors
        );
    }
}
