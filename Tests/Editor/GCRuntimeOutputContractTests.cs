using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DSB.GC;
using DSB.GC.Dev;
using DSB.GC.Game;
using DSB.GC.Hud;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCRuntimeOutputContractTests
{
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();
    private double nowSeconds;

    [SetUp]
    public void SetUp()
    {
        nowSeconds = 1.0;
        GCRuntimeMessageOutput.ResetForTests(() => nowSeconds);
        GCRuntimeMessageOutput.BeginActiveRun();
        GCLog.logLevel = LogLevel.None;
        ClearGamingCouchInstance();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var unityObject in objectsToDestroy)
        {
            if (unityObject)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        objectsToDestroy.Clear();
        GCRuntimeMessageOutput.ResetForTests(null);
        GCLog.logLevel = LogLevel.None;
        ClearGamingCouchInstance();
    }

    [Test]
    public void PlayerStateChangeEmitsTransitionBeforeOneCoalescedSnapshot()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        nowSeconds = 1.125;

        context.players[0].SetScore(10, "score reason");
        context.players[0].SetLives(2, "lives reason");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        var json = emitted[0];
        Assert.That(json, Does.Contain("\"type\":\"runtime_messages\""));
        Assert.That(json, Does.Contain("\"messageType\":\"gc.player.score_changed\""));
        Assert.That(json, Does.Contain("\"previousValue\":0"));
        Assert.That(json, Does.Contain("\"value\":10"));
        Assert.That(json, Does.Contain("\"reasonText\":\"score reason\""));
        Assert.That(json, Does.Contain("\"messageType\":\"gc.player.lives_changed\""));
        Assert.That(json, Does.Contain("\"messageType\":\"gc.state.snapshot\""));
        Assert.That(json.IndexOf("\"messageType\":\"gc.player.score_changed\""), Is.LessThan(json.IndexOf("\"messageType\":\"gc.state.snapshot\"")));
        Assert.That(CountOccurrences(json, "\"messageType\":\"gc.state.snapshot\""), Is.EqualTo(1));
        Assert.That(json, Does.Contain("\"sequence\":1"));
        Assert.That(json, Does.Contain("\"sequence\":2"));
        Assert.That(json, Does.Contain("\"sequence\":3"));
        Assert.That(json, Does.Contain("\"runtimeTimeMs\":125"));
    }

    [Test]
    public void DiagnosticFlushesQueuedRuntimeMessagesInSequenceOrder()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        nowSeconds = 1.125;

        context.players[0].SetScore(10, "score reason");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.clamped_value: Lives were clamped.");
        context.players[0].SetLives(-1, "invalid lives");

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"messageType\":\"gc.player.score_changed\""));
        Assert.That(emitted[0], Does.Contain("\"messageType\":\"gc.diagnostic\""));
        Assert.That(
            emitted[0].IndexOf("\"messageType\":\"gc.player.score_changed\""),
            Is.LessThan(emitted[0].IndexOf("\"messageType\":\"gc.diagnostic\""))
        );
        Assert.That(emitted[0], Does.Contain("\"sequence\":1"));
        Assert.That(emitted[0], Does.Contain("\"sequence\":2"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SnapshotBootToggleLeavesTransitionsEnabled()
    {
        GCRuntimeMessageOutput.BeginActiveRun(new GCRuntimeOutputOptions
        {
            stateSnapshots = false,
            screenSpace = true,
        });
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetMeter(50, "meter reason");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"messageType\":\"gc.player.meter_changed\""));
        Assert.That(emitted[0], Does.Not.Contain("\"messageType\":\"gc.state.snapshot\""));
    }

    [Test]
    public void TerminalPlacementSubmitsObjectPayloadAfterPendingSnapshotAndAcceptsOnlyFirst()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;

        Assert.That(context.gamingCouch.TrySubmitTerminalPlacement(new[] { 0, 1 }, out var firstEnvelope), Is.True);

        Assert.That(firstEnvelope, Is.EqualTo(emitted[0]));
        Assert.That(firstEnvelope, Does.Contain("\"messageType\":\"gc.state.snapshot\""));
        Assert.That(firstEnvelope, Does.Contain("\"messageType\":\"gc.game.game_over\""));
        Assert.That(firstEnvelope, Does.Contain("\"payload\":{\"playerIndicesByPlacement\":[0,1]}"));
        Assert.That(firstEnvelope.IndexOf("\"messageType\":\"gc.state.snapshot\""), Is.LessThan(firstEnvelope.IndexOf("\"messageType\":\"gc.game.game_over\"")));

        LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_terminal_placement: Terminal placement was already accepted for this active run.");
        Assert.That(context.gamingCouch.TrySubmitTerminalPlacement(new[] { 0, 1 }, out var secondEnvelope), Is.False);
        Assert.That(secondEnvelope, Is.Null);
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[1], Does.Contain("\"messageType\":\"gc.diagnostic\""));
        Assert.That(emitted[1], Does.Contain("\"code\":\"gc.runtime.invalid_terminal_placement\""));
        Assert.That(emitted[1], Does.Not.Contain("\"messageType\":\"gc.game.game_over\""));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void TerminalPlacementRejectsReentrantSubmissionBeforePublishingSecondResult()
    {
        var context = CreateRuntimeGame(2);
        var emitted = new List<string>();
        var reentered = false;
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += json =>
        {
            emitted.Add(json);
            if (reentered || !json.Contains("\"messageType\":\"gc.game.game_over\""))
            {
                return;
            }

            reentered = true;
            LogAssert.Expect(LogType.Error, "[GC] Diagnostic gc.runtime.invalid_terminal_placement: Terminal placement was already accepted for this active run.");
            Assert.That(context.gamingCouch.TrySubmitTerminalPlacement(new[] { 1, 0 }, out var reentrantEnvelope), Is.False);
            Assert.That(reentrantEnvelope, Is.Null);
        };

        Assert.That(context.gamingCouch.TrySubmitTerminalPlacement(new[] { 0, 1 }, out var firstEnvelope), Is.True);

        Assert.That(firstEnvelope, Is.EqualTo(emitted[0]));
        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"payload\":{\"playerIndicesByPlacement\":[0,1]}"));
        Assert.That(emitted[1], Does.Contain("\"code\":\"gc.runtime.invalid_terminal_placement\""));
        Assert.That(emitted[1], Does.Not.Contain("\"payload\":{\"playerIndicesByPlacement\":[1,0]}"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void TerminalPlacementPayloadRejectsMissingDuplicateAndOutOfRangeIndices()
    {
        Assert.Throws<System.ArgumentException>(() => GCRuntimeTerminalPlacementPayload.BuildJson(new[] { 0 }, 2));
        Assert.Throws<System.ArgumentException>(() => GCRuntimeTerminalPlacementPayload.BuildJson(new[] { 0, 0 }, 2));
        Assert.Throws<System.ArgumentException>(() => GCRuntimeTerminalPlacementPayload.BuildJson(new[] { 0, 2 }, 2));
        Assert.That(
            GCRuntimeTerminalPlacementPayload.BuildJson(new[] { 0, 1 }, 2),
            Is.EqualTo("{\"playerIndicesByPlacement\":[0,1]}")
        );
    }

    [Test]
    public void ScreenSpaceEmitsValidatedV1AnchorsAndKeepsNameTagsOutOfContract()
    {
        CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeScreenSpaceOutput.ScreenSpaceEmitted += emitted.Add;
        var hud = new GCHud();

        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 1.2f,
            y = -0.25f,
            isOffScreen = true,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerPosition",
            playerIndex = 0,
            x = 0.4f,
            y = 0.5f,
            isOffScreen = false,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "name",
            playerIndex = 0,
            x = 0.4f,
            y = 0.5f,
            isOffScreen = false,
        });

        hud.HandleQueue();

        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"type\":\"screen_space\""));
        Assert.That(emitted[0], Does.Contain("\"anchorType\":\"playerOverhead\""));
        Assert.That(emitted[0], Does.Contain("\"anchorType\":\"playerPosition\""));
        Assert.That(emitted[0], Does.Contain("\"playerIndex\":0"));
        Assert.That(emitted[0], Does.Contain("\"x\":1"));
        Assert.That(emitted[0], Does.Contain("\"y\":0"));
        Assert.That(emitted[0], Does.Contain("\"isOffScreen\":true"));
        Assert.That(emitted[0], Does.Not.Contain("\"anchorType\":\"name\""));
    }

    [Test]
    public void WebGLJslibExportsCanonicalScreenSpaceBridge()
    {
        var bridgePath = Path.Combine(FindPackageRootPath(), "Plugins", "GamingCouch.jslib");
        var bridge = File.ReadAllText(bridgePath);

        Assert.That(bridge, Does.Contain("GamingCouchScreenSpace: function (screenSpaceJsonString)"));
        Assert.That(bridge, Does.Contain("window.gamingCouchScreenSpace"));
        Assert.That(bridge, Does.Contain("JSON.parse(UTF8ToString(screenSpaceJsonString))"));
        Assert.That(bridge, Does.Contain("window.gamingCouchScreenSpace(screenSpace);"));
    }

    [Test]
    public void ScreenSpaceRejectsDuplicateAnchorPairs()
    {
        CreateRuntimeGame(1);
        var hud = new GCHud();
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 0.1f,
            y = 0.2f,
            isOffScreen = false,
        });
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 0,
            x = 0.3f,
            y = 0.4f,
            isOffScreen = false,
        });

        LogAssert.Expect(
            LogType.Warning,
            new Regex(@"\[GC\] Diagnostic gc\.runtime\.malformed_screen_space: Malformed screen-space output was rejected\.")
        );
        hud.HandleQueue();
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void ScreenSpaceRejectsOutOfRangePlayerIndexBeforeQueueingAnchor()
    {
        CreateRuntimeGame(1);
        var emitted = new List<string>();
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;
        GCRuntimeScreenSpaceOutput.ScreenSpaceEmitted += emitted.Add;
        var hud = new GCHud();

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.mapping.invalid_player_index: Player index is outside the active mapping.");
        hud.QueuePointData(new GCScreenPointDataPoint
        {
            type = "playerOverhead",
            playerIndex = 1,
            x = 0.5f,
            y = 0.5f,
            isOffScreen = false,
        });
        hud.HandleQueue();

        Assert.That(emitted, Has.Count.EqualTo(2));
        Assert.That(emitted[0], Does.Contain("\"code\":\"gc.mapping.invalid_player_index\""));
        Assert.That(emitted[1], Does.Contain("\"type\":\"screen_space\""));
        Assert.That(emitted[1], Does.Contain("\"anchors\":[]"));
        Assert.That(emitted[1], Does.Not.Contain("\"playerIndex\":1"));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void SetStatusWithNullTextDoesNotEmitDuplicateTransitionAfterEmptyText()
    {
        var context = CreateRuntimeGame(1);
        var emitted = new List<string>();
        var publicCallbackCount = 0;
        context.players[0].OnStatusChanged += (status, statusText, reason) => publicCallbackCount++;
        GCRuntimeMessageOutput.RuntimeMessagesEmitted += emitted.Add;

        context.players[0].SetStatus(GCPlayerStatus.Neutral, null, "same status");
        context.gamingCouch.FlushRuntimeOutput();

        Assert.That(publicCallbackCount, Is.EqualTo(0));
        Assert.That(emitted, Has.Count.EqualTo(1));
        Assert.That(emitted[0], Does.Contain("\"messageType\":\"gc.state.snapshot\""));
        Assert.That(emitted[0], Does.Not.Contain("\"messageType\":\"gc.player.status_changed\""));
    }

    [Test]
    public void PlayersHudDataIsBuiltFromCanonicalRuntimeStateSnapshot()
    {
        var context = CreateRuntimeGame(2);
        context.players[0].SetScore(12, "score");
        context.players[0].SetLives(3, "lives");
        context.players[0].SetStatus(GCPlayerStatus.Warning, "low fuel", "status");
        context.players[0].SetMeter(44, "meter");
        context.players[1].SetScore(7, "score");
        context.players[1].SetEliminatedPermanent("out");
        context.players[1].SetFinishedRevokable("finish");

        var hudData = context.game.BuildPlayersHudData();

        Assert.That(hudData.players, Has.Length.EqualTo(2));
        AssertHudPlayer(
            hudData.players[0],
            playerIndex: 0,
            score: 12,
            lives: 3,
            status: "Warning",
            statusText: "low fuel",
            meter: 44,
            placement: 1,
            eliminationState: "None",
            finishState: "None",
            eliminated: false,
            value: null
        );
        AssertHudPlayer(
            hudData.players[1],
            playerIndex: 1,
            score: 7,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: -1,
            placement: 2,
            eliminationState: "Permanent",
            finishState: "Revokable",
            eliminated: true,
            value: null
        );
    }

    private RuntimeGameContext CreateRuntimeGame(int playerCount)
    {
        var gameObject = new GameObject("Gaming Couch");
        objectsToDestroy.Add(gameObject);
        gameObject.SetActive(false);
        var gamingCouch = gameObject.AddComponent<GamingCouch>();
        gamingCouch.LogLevel = LogLevel.None;
        gameObject.SetActive(true);

        var store = gamingCouch.InternalPlayerStore;
        var game = new GCGame(gamingCouch, store, new GCGameSetupOptions
        {
            placementCriteria = new[] { GCPlacementSortCriteria.ScoreDescending },
        });
        SetPrivateField(gamingCouch, "game", game);
        SetPrivateField(gamingCouch, "status", GCStatus.Playing);
        SetPrivateField(gamingCouch, "activePlayerMapping", CreateActivePlayerMapping(playerCount));

        var players = new GCPlayer[playerCount];
        for (var index = 0; index < playerCount; index++)
        {
            players[index] = CreatePlayer(index);
            game.SetupPlayer(players[index]);
            store.AddPlayer(players[index]);
        }

        gamingCouch.QueueRuntimeStateSnapshot();
        return new RuntimeGameContext(gamingCouch, game, players);
    }

    private static string FindPackageRootPath()
    {
        var packageInfo = PackageInfo.FindForAssembly(typeof(GamingCouch).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            return packageInfo.resolvedPath;
        }

        throw new InvalidOperationException("Could not resolve Gaming Couch package root.");
    }

    private GCPlayer CreatePlayer(int playerIndex)
    {
        var gameObject = new GameObject("Player " + playerIndex);
        objectsToDestroy.Add(gameObject);
        var player = gameObject.AddComponent<GCPlayer>();
        player._InternalGamingCouchSetup(new GCPlayerSetupOptions
        {
            playerIndex = playerIndex,
            type = GCPlayerType.player,
            colorEnum = GCPlayerColor.blue,
            colorName = "blue",
        });
        return player;
    }

    private static GCActivePlayerMapping CreateActivePlayerMapping(int playerCount)
    {
        var players = new GCActivePlayerOptions[playerCount];
        var seats = new GCSeatIdentity[playerCount];
        for (var index = 0; index < playerCount; index++)
        {
            players[index] = new GCActivePlayerOptions
            {
                playerIndex = index,
                type = "player",
                color = "blue",
            };
            seats[index] = new GCSeatIdentity
            {
                sourceSeatIndex = index + 1,
                stableKey = (index + 1).ToString(),
                playerType = GCPlayerType.player,
                playerColor = GCPlayerColor.blue,
            };
        }

        return GCActivePlayerMapping.Create(new GCPlayOptions
        {
            players = players,
            seed = 123,
            usesMappedActivePlayers = true,
        }, seats);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static void ClearGamingCouchInstance()
    {
        typeof(GamingCouch)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static void AssertHudPlayer(
        GCPlayersHudDataPlayer player,
        int playerIndex,
        int score,
        int lives,
        string status,
        string statusText,
        int meter,
        int placement,
        string eliminationState,
        string finishState,
        bool eliminated,
        string value
    )
    {
        Assert.That(player.playerIndex, Is.EqualTo(playerIndex));
        Assert.That(player.score, Is.EqualTo(score));
        Assert.That(player.lives, Is.EqualTo(lives));
        Assert.That(player.status, Is.EqualTo(status));
        Assert.That(player.statusText, Is.EqualTo(statusText));
        Assert.That(player.meter, Is.EqualTo(meter));
        Assert.That(player.placement, Is.EqualTo(placement));
        Assert.That(player.eliminationState, Is.EqualTo(eliminationState));
        Assert.That(player.finishState, Is.EqualTo(finishState));
        Assert.That(player.eliminated, Is.EqualTo(eliminated));
        Assert.That(player.value, Is.EqualTo(value));
    }

    private readonly struct RuntimeGameContext
    {
        internal readonly GamingCouch gamingCouch;
        internal readonly GCGame game;
        internal readonly GCPlayer[] players;

        internal RuntimeGameContext(GamingCouch gamingCouch, GCGame game, GCPlayer[] players)
        {
            this.gamingCouch = gamingCouch;
            this.game = game;
            this.players = players;
        }
    }
}
