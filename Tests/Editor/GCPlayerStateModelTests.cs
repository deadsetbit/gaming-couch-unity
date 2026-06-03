using System;
using System.Collections.Generic;
using System.Reflection;
using DSB.GC;
using DSB.GC.Game;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCPlayerStateModelTests
{
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        GCRuntimeMessageOutput.ResetForTests(() => 0);
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
        GCLog.logLevel = LogLevel.None;
        ClearGamingCouchInstance();
    }

    [Test]
    public void EliminationTransitionsExposeStateBooleansTimestampsAndEventArgs()
    {
        var player = CreatePlayer(2);
        var events = new List<GCPlayerEliminationStateChangedEventArgs>();
        player.OnEliminationStateChanged += events.Add;

        player.SetEliminatedRevokable("temporary hazard");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Revokable));
        Assert.That(player.IsEliminated, Is.True);
        Assert.That(player.IsEliminatedRevokable, Is.True);
        Assert.That(player.IsEliminatedPermanent, Is.False);
        Assert.That(player.LastSetEliminatedRevokableGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetEliminatedGameTime, Is.EqualTo(player.LastSetEliminatedRevokableGameTime));
        Assert.That(events, Has.Count.EqualTo(1));
        AssertEliminationEvent(
            events[0],
            2,
            GCPlayerEliminationState.None,
            GCPlayerEliminationState.Revokable,
            "temporary hazard",
            player.LastSetEliminatedRevokableGameTime
        );

        player.SetEliminatedPermanent("final hazard");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Permanent));
        Assert.That(player.IsEliminatedPermanent, Is.True);
        Assert.That(player.IsEliminatedRevokable, Is.False);
        Assert.That(player.LastSetEliminatedPermanentGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetEliminatedGameTime, Is.EqualTo(player.LastSetEliminatedPermanentGameTime));
        Assert.That(events, Has.Count.EqualTo(2));
        AssertEliminationEvent(
            events[1],
            2,
            GCPlayerEliminationState.Revokable,
            GCPlayerEliminationState.Permanent,
            "final hazard",
            player.LastSetEliminatedPermanentGameTime
        );
    }

    [Test]
    public void FinishTransitionsExposeStateBooleansTimestampsAndEventArgs()
    {
        var player = CreatePlayer(1);
        var events = new List<GCPlayerFinishStateChangedEventArgs>();
        player.OnFinishStateChanged += events.Add;

        player.SetFinishedRevokable("checkpoint");

        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Revokable));
        Assert.That(player.IsFinished, Is.True);
        Assert.That(player.IsFinishedRevokable, Is.True);
        Assert.That(player.IsFinishedPermanent, Is.False);
        Assert.That(player.LastSetFinishedRevokableGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetFinishedGameTime, Is.EqualTo(player.LastSetFinishedRevokableGameTime));
        Assert.That(events, Has.Count.EqualTo(1));
        AssertFinishEvent(
            events[0],
            1,
            GCPlayerFinishState.None,
            GCPlayerFinishState.Revokable,
            "checkpoint",
            player.LastSetFinishedRevokableGameTime
        );

        player.SetFinishedPermanent("finish line");

        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Permanent));
        Assert.That(player.IsFinishedPermanent, Is.True);
        Assert.That(player.IsFinishedRevokable, Is.False);
        Assert.That(player.LastSetFinishedPermanentGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetFinishedGameTime, Is.EqualTo(player.LastSetFinishedPermanentGameTime));
        Assert.That(events, Has.Count.EqualTo(2));
        AssertFinishEvent(
            events[1],
            1,
            GCPlayerFinishState.Revokable,
            GCPlayerFinishState.Permanent,
            "finish line",
            player.LastSetFinishedPermanentGameTime
        );
    }

    [Test]
    public void RevokingRevokableStatesClearsOnlyTheMatchingState()
    {
        var player = CreatePlayer(3);
        player.SetEliminatedRevokable("temporary");
        player.SetFinishedRevokable("checkpoint");

        player.SetRevokeEliminated("respawn");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.None));
        Assert.That(player.IsEliminated, Is.False);
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Revokable));
        Assert.That(player.IsFinished, Is.True);
        Assert.That(player.LastSetRevokeEliminatedGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetRevokeGameTime, Is.EqualTo(player.LastSetRevokeEliminatedGameTime));

        player.SetRevokeFinished("rollback");

        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.None));
        Assert.That(player.IsFinished, Is.False);
        Assert.That(player.LastSetRevokeFinishedGameTime, Is.GreaterThanOrEqualTo(0));
        Assert.That(player.LastSetRevokeGameTime, Is.EqualTo(player.LastSetRevokeFinishedGameTime));
    }

    [Test]
    public void DuplicateAndInvalidStateTransitionsDiagnoseAndNoOp()
    {
        var player = CreatePlayer(4);
        player.SetEliminatedPermanent("final");
        player.SetFinishedPermanent("final");
        var eliminationEventCount = 0;
        var finishEventCount = 0;
        player.OnEliminationStateChanged += args => eliminationEventCount++;
        player.OnFinishStateChanged += args => finishEventCount++;

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_elimination: Player is already permanently eliminated.");
        player.SetEliminatedPermanent("duplicate");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_transition: Permanent elimination cannot transition back to revokable elimination.");
        player.SetEliminatedRevokable("invalid");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable elimination can be revoked.");
        player.SetRevokeEliminated("invalid revoke");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_finish: Player is already permanently finished.");
        player.SetFinishedPermanent("duplicate");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_transition: Permanent finish cannot transition back to revokable finish.");
        player.SetFinishedRevokable("invalid");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable finish can be revoked.");
        player.SetRevokeFinished("invalid revoke");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Permanent));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Permanent));
        Assert.That(eliminationEventCount, Is.EqualTo(0));
        Assert.That(finishEventCount, Is.EqualTo(0));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void DuplicateRevokableStatesAndRevokeFromNoneDiagnoseAndNoOp()
    {
        var player = CreatePlayer(8);
        var eliminationEventCount = 0;
        var finishEventCount = 0;
        player.OnEliminationStateChanged += args => eliminationEventCount++;
        player.OnFinishStateChanged += args => finishEventCount++;

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable elimination can be revoked.");
        player.SetRevokeEliminated("none");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.invalid_revoke: Only revokable finish can be revoked.");
        player.SetRevokeFinished("none");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.None));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.None));

        player.SetEliminatedRevokable("temporary");
        player.SetFinishedRevokable("checkpoint");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_elimination: Player is already revokably eliminated.");
        player.SetEliminatedRevokable("duplicate");

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.duplicate_finish: Player is already revokably finished.");
        player.SetFinishedRevokable("duplicate");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.Revokable));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.Revokable));
        Assert.That(eliminationEventCount, Is.EqualTo(1));
        Assert.That(finishEventCount, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void FinishAndEliminationCanCoexist()
    {
        var player = CreatePlayer(5);

        player.SetEliminatedPermanent("out");
        player.SetFinishedRevokable("checkpoint");

        Assert.That(player.IsEliminatedPermanent, Is.True);
        Assert.That(player.IsFinishedRevokable, Is.True);

        player.SetRevokeFinished("rollback");

        Assert.That(player.IsEliminatedPermanent, Is.True);
        Assert.That(player.IsFinished, Is.False);
    }

    [Test]
    public void StoreBroadEliminationListsTrackNoneVersusAnyEliminatedState()
    {
        var player = CreatePlayer(7);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(player);

        Assert.That(store.PlayersUneliminated.Count, Is.EqualTo(1));
        Assert.That(store.PlayersEliminated.Count, Is.EqualTo(0));

        player.SetEliminatedRevokable("temporary");

        Assert.That(store.PlayersUneliminated.Count, Is.EqualTo(0));
        Assert.That(store.PlayersEliminated.Count, Is.EqualTo(1));
        Assert.That(store.PlayersEliminatedRevokable, Is.EqualTo(new[] { player }));
        Assert.That(store.PlayersEliminatedPermanent, Is.Empty);

        player.SetEliminatedPermanent("promotion");

        Assert.That(store.PlayersUneliminated.Count, Is.EqualTo(0));
        Assert.That(store.PlayersEliminated.Count, Is.EqualTo(1));
        Assert.That(store.PlayersEliminatedRevokable, Is.Empty);
        Assert.That(store.PlayersEliminatedPermanent, Is.EqualTo(new[] { player }));
    }

    [Test]
    public void StoreStateCollectionsExposePlayersPrefixAndBotNonBotSymmetry()
    {
        var activePlayer = CreatePlayer(0);
        var eliminatedBot = CreatePlayer(1, GCPlayerType.bot);
        var eliminatedNonBot = CreatePlayer(2);
        var finishedBot = CreatePlayer(3, GCPlayerType.bot);
        var finishedNonBot = CreatePlayer(4);

        eliminatedBot.SetEliminatedRevokable("temporary");
        eliminatedNonBot.SetEliminatedPermanent("final");
        finishedBot.SetFinishedRevokable("checkpoint");
        finishedNonBot.SetFinishedPermanent("done");

        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(activePlayer);
        store.AddPlayer(eliminatedBot);
        store.AddPlayer(eliminatedNonBot);
        store.AddPlayer(finishedBot);
        store.AddPlayer(finishedNonBot);

        Assert.That(store.Players, Is.EqualTo(new[] { activePlayer, eliminatedBot, eliminatedNonBot, finishedBot, finishedNonBot }));
        Assert.That(store.PlayersBot, Is.EqualTo(new[] { eliminatedBot, finishedBot }));
        Assert.That(store.PlayersNonBot, Is.EqualTo(new[] { activePlayer, eliminatedNonBot, finishedNonBot }));

        Assert.That(store.PlayersUneliminated, Is.EqualTo(new[] { activePlayer, finishedBot, finishedNonBot }));
        Assert.That(store.PlayersUneliminatedBot, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersUneliminatedNonBot, Is.EqualTo(new[] { activePlayer, finishedNonBot }));

        Assert.That(store.PlayersEliminated, Is.EqualTo(new[] { eliminatedBot, eliminatedNonBot }));
        Assert.That(store.PlayersEliminatedBot, Is.EqualTo(new[] { eliminatedBot }));
        Assert.That(store.PlayersEliminatedNonBot, Is.EqualTo(new[] { eliminatedNonBot }));
        Assert.That(store.PlayersEliminatedRevokable, Is.EqualTo(new[] { eliminatedBot }));
        Assert.That(store.PlayersEliminatedRevokableBot, Is.EqualTo(new[] { eliminatedBot }));
        Assert.That(store.PlayersEliminatedRevokableNonBot, Is.Empty);
        Assert.That(store.PlayersEliminatedPermanent, Is.EqualTo(new[] { eliminatedNonBot }));
        Assert.That(store.PlayersEliminatedPermanentBot, Is.Empty);
        Assert.That(store.PlayersEliminatedPermanentNonBot, Is.EqualTo(new[] { eliminatedNonBot }));

        Assert.That(store.PlayersFinished, Is.EqualTo(new[] { finishedBot, finishedNonBot }));
        Assert.That(store.PlayersFinishedBot, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersFinishedNonBot, Is.EqualTo(new[] { finishedNonBot }));
        Assert.That(store.PlayersFinishedRevokable, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersFinishedRevokableBot, Is.EqualTo(new[] { finishedBot }));
        Assert.That(store.PlayersFinishedRevokableNonBot, Is.Empty);
        Assert.That(store.PlayersFinishedPermanent, Is.EqualTo(new[] { finishedNonBot }));
        Assert.That(store.PlayersFinishedPermanentBot, Is.Empty);
        Assert.That(store.PlayersFinishedPermanentNonBot, Is.EqualTo(new[] { finishedNonBot }));
    }

    [Test]
    public void StoreRejectsDuplicatePlayersAndPlayerIndices()
    {
        var player = CreatePlayer(0);
        var duplicateIndexPlayer = CreatePlayer(0);
        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(player);

        Assert.Throws<InvalidOperationException>(() => store.AddPlayer(player));
        Assert.Throws<InvalidOperationException>(() => store.AddPlayer(duplicateIndexPlayer));
        Assert.That(store.Players, Is.EqualTo(new[] { player }));
        Assert.That(store.PlayersUneliminated, Is.EqualTo(new[] { player }));
    }

    [Test]
    public void BroadPlacementCriteriaTreatPermanentAndRevokableStatesAsCurrent()
    {
        var eliminatedRevokable = CreatePlayer(0);
        var eliminatedPermanent = CreatePlayer(1);
        var activePlayer = CreatePlayer(2);
        eliminatedRevokable.SetEliminatedRevokable("temporary");
        eliminatedPermanent.SetEliminatedPermanent("final");

        var finishedRevokable = CreatePlayer(3);
        var finishedPermanent = CreatePlayer(4);
        var unfinishedPlayer = CreatePlayer(5);
        finishedRevokable.SetFinishedRevokable("checkpoint");
        finishedPermanent.SetFinishedPermanent("done");

        var eliminatedGame = CreateGameWithPlacementCriteria(GCPlacementSortCriteria.Eliminated);
        var eliminatedOrder = eliminatedGame.GetPlayersInPlacementOrder(new[] { eliminatedRevokable, eliminatedPermanent, activePlayer });
        Assert.That(eliminatedOrder, Is.EqualTo(new[] { eliminatedRevokable, eliminatedPermanent, activePlayer }));

        var finishedGame = CreateGameWithPlacementCriteria(GCPlacementSortCriteria.Finished);
        var finishedOrder = finishedGame.GetPlayersInPlacementOrder(new[] { finishedRevokable, finishedPermanent, unfinishedPlayer });
        Assert.That(finishedOrder, Is.EqualTo(new[] { finishedRevokable, finishedPermanent, unfinishedPlayer }));
    }

    [Test]
    public void RuntimeStateSnapshotProjectsCanonicalDynamicPlayerState()
    {
        var leadingPlayer = CreatePlayer(0);
        var trailingPlayer = CreatePlayer(1, GCPlayerType.bot);
        leadingPlayer.SetScore(10, "score");
        leadingPlayer.SetLives(2, "lives");
        leadingPlayer.SetStatus(GCPlayerStatus.Success, "Finished lap", "status");
        leadingPlayer.SetMeter(74, "meter");
        leadingPlayer.SetFinishedRevokable("checkpoint");
        trailingPlayer.SetScore(5, "score");
        trailingPlayer.SetEliminatedPermanent("out");

        var store = new GCPlayerStore<GCPlayer>();
        store.AddPlayer(leadingPlayer);
        store.AddPlayer(trailingPlayer);
        var game = new GCGame(null, store, new GCGameSetupOptions
        {
            placementCriteria = new[] { GCPlacementSortCriteria.ScoreDescending },
        });

        var payload = game.BuildRuntimeStateSnapshotPayload(GCStatus.Playing);
        var json = payload.ToJson();

        Assert.That(payload.game.status, Is.EqualTo("playing"));
        Assert.That(payload.players, Has.Length.EqualTo(2));
        AssertSnapshotPlayer(
            payload.players[0],
            playerIndex: 0,
            score: 10,
            lives: 2,
            status: "Success",
            statusText: "Finished lap",
            meter: 74,
            placement: 1,
            eliminationState: "None",
            finishState: "Revokable"
        );
        AssertSnapshotPlayer(
            payload.players[1],
            playerIndex: 1,
            score: 5,
            lives: 0,
            status: "Neutral",
            statusText: "",
            meter: -1,
            placement: 2,
            eliminationState: "Permanent",
            finishState: "None"
        );
        Assert.That(json, Does.Contain("\"game\":{\"status\":\"playing\"}"));
        Assert.That(json, Does.Contain("\"playerIndex\":0"));
        Assert.That(json, Does.Contain("\"placement\":1"));
        Assert.That(json, Does.Contain("\"eliminationState\":\"Permanent\""));
        Assert.That(json, Does.Not.Contain("\"type\""));
        Assert.That(json, Does.Not.Contain("\"color\""));
    }

    [Test]
    public void RuntimeStateSnapshotRequiresCanonicalPlayerIndicesExactlyOnce()
    {
        var player = CreatePlayer(0);
        var duplicateIndexPlayer = CreatePlayer(0);
        var outOfRangePlayer = CreatePlayer(2);

        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { player, duplicateIndexPlayer },
            new[] { player, duplicateIndexPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { player, outOfRangePlayer },
            new[] { player, outOfRangePlayer }
        ));
    }

    [Test]
    public void RuntimeStateSnapshotValidatesPlayerAndPlacementInputs()
    {
        var firstPlayer = CreatePlayer(0);
        var secondPlayer = CreatePlayer(1);
        var outsidePlayer = CreatePlayer(2);

        Assert.Throws<ArgumentNullException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            null,
            new[] { firstPlayer }
        ));
        Assert.Throws<ArgumentNullException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer },
            null
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new GCPlayer[] { firstPlayer, null },
            new[] { firstPlayer, secondPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer },
            new GCPlayer[] { null }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { firstPlayer, firstPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { firstPlayer }
        ));
        Assert.Throws<ArgumentException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { firstPlayer, outsidePlayer }
        ));
    }

    [Test]
    public void RuntimeStateSnapshotUsesExactNormalizedGameStatusAndOneBasedPlacements()
    {
        var firstPlayer = CreatePlayer(0);
        var secondPlayer = CreatePlayer(1);

        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.PendingSetup, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("pending_setup")
        );
        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.SetupDone, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("setup_done")
        );
        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.Playing, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("playing")
        );
        Assert.That(
            GCRuntimeStateSnapshotBuilder.BuildPayload(GCStatus.GameOver, new[] { firstPlayer }, new[] { firstPlayer }).game.status,
            Is.EqualTo("game_over")
        );

        var reversedPayload = GCRuntimeStateSnapshotBuilder.BuildPayload(
            GCStatus.Playing,
            new[] { firstPlayer, secondPlayer },
            new[] { secondPlayer, firstPlayer }
        );
        Assert.That(reversedPayload.players[0].placement, Is.EqualTo(2));
        Assert.That(reversedPayload.players[1].placement, Is.EqualTo(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GCRuntimeStateSnapshotBuilder.BuildPayload(
            (GCStatus)999,
            new[] { firstPlayer },
            new[] { firstPlayer }
        ));
    }

    [Test]
    public void RemovedStateApisFailAtSourceWithMigrationGuidance()
    {
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetEliminated"),
            "SetEliminatedPermanent"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetEliminated"),
            "SetEliminatedRevokable"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetUneliminated"),
            "SetRevokeEliminated"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetFinished"),
            "SetFinishedPermanent"
        );
        AssertObsoleteError(
            typeof(GCPlayer).GetMethod("SetFinished"),
            "SetFinishedRevokable"
        );

        Assert.That(typeof(GCPlayer).GetField("OnEliminated"), Is.Null);
        Assert.That(typeof(GCPlayer).GetField("OnUneliminated"), Is.Null);
        Assert.That(typeof(GCPlayer).GetField("OnFinished"), Is.Null);
    }

    [Test]
    public void RemovedStoreCollectionsFailAtSourceWithMigrationGuidance()
    {
        var storeType = typeof(GCPlayerStore<GCPlayer>);

        AssertObsoleteError(storeType.GetProperty("PlayersEnumerable"), "Players");
        AssertObsoleteError(storeType.GetProperty("PlayerCount"), "Players.Count");
        AssertObsoleteError(storeType.GetProperty("UneliminatedPlayers"), "PlayersUneliminated");
        AssertObsoleteError(storeType.GetProperty("UneliminatedPlayersEnumerable"), "PlayersUneliminated");
        AssertObsoleteError(storeType.GetProperty("UneliminatedBotPlayers"), "PlayersUneliminatedBot");
        AssertObsoleteError(storeType.GetProperty("UneliminatedNonBotPlayers"), "PlayersUneliminatedNonBot");
        AssertObsoleteError(storeType.GetProperty("UneliminatedPlayerCount"), "PlayersUneliminated.Count");
        AssertObsoleteError(storeType.GetProperty("EliminatedPlayers"), "PlayersEliminated");
        AssertObsoleteError(storeType.GetProperty("EliminatedPlayersEnumerable"), "PlayersEliminated");
        AssertObsoleteError(storeType.GetProperty("EliminatedBotPlayers"), "PlayersEliminatedBot");
        AssertObsoleteError(storeType.GetProperty("EliminatedNonBotPlayers"), "PlayersEliminatedNonBot");
        AssertObsoleteError(storeType.GetProperty("EliminatedPlayerCount"), "PlayersEliminated.Count");
    }

    [Test]
    public void PostGameOverPlayerMutationsDiagnoseAndNoOp()
    {
        CreateGameOverGamingCouch();
        var player = CreatePlayer(6);

        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetEliminatedPermanent("too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetFinishedPermanent("too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetScore(10, "too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetLives(3, "too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetStatus(GCPlayerStatus.Success, "done", "too late");
        LogAssert.Expect(LogType.Warning, "[GC] Diagnostic gc.state.post_game_over_mutation: Player mutation after game over was ignored.");
        player.SetMeter(50, "too late");

        Assert.That(player.EliminationState, Is.EqualTo(GCPlayerEliminationState.None));
        Assert.That(player.FinishState, Is.EqualTo(GCPlayerFinishState.None));
        Assert.That(player.Score, Is.EqualTo(0));
        Assert.That(player.Lives, Is.EqualTo(0));
        Assert.That(player.Status, Is.EqualTo(GCPlayerStatus.Neutral));
        Assert.That(player.StatusText, Is.EqualTo(""));
        Assert.That(player.Meter, Is.EqualTo(-1));
        LogAssert.NoUnexpectedReceived();
    }

    private GCPlayer CreatePlayer(int playerIndex, GCPlayerType playerType = GCPlayerType.player)
    {
        var gameObject = new GameObject("Player " + playerIndex);
        objectsToDestroy.Add(gameObject);
        var player = gameObject.AddComponent<GCPlayer>();
        player._InternalGamingCouchSetup(new GCPlayerSetupOptions
        {
            playerIndex = playerIndex,
            type = playerType,
            colorEnum = GCPlayerColor.blue,
            colorName = "blue",
        });
        return player;
    }

    private static GCGame CreateGameWithPlacementCriteria(params GCPlacementSortCriteria[] criteria)
    {
        return new GCGame(null, new GCPlayerStore<GCPlayer>(), new GCGameSetupOptions
        {
            placementCriteria = criteria,
        });
    }

    private void CreateGameOverGamingCouch()
    {
        var gameObject = new GameObject("Gaming Couch");
        objectsToDestroy.Add(gameObject);
        gameObject.SetActive(false);
        var gamingCouch = gameObject.AddComponent<GamingCouch>();
        gamingCouch.LogLevel = LogLevel.None;
        gameObject.SetActive(true);
        typeof(GamingCouch)
            .GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(gamingCouch, GCStatus.GameOver);
    }

    private static void ClearGamingCouchInstance()
    {
        typeof(GamingCouch)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, null);
    }

    private static void AssertObsoleteError(MemberInfo member, string expectedGuidance)
    {
        Assert.That(member, Is.Not.Null);

        var obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
        Assert.That(obsolete, Is.Not.Null);
        Assert.That(obsolete.IsError, Is.True);
        Assert.That(obsolete.Message, Does.Contain(expectedGuidance));
    }

    private static void AssertEliminationEvent(
        GCPlayerEliminationStateChangedEventArgs args,
        int playerIndex,
        GCPlayerEliminationState oldState,
        GCPlayerEliminationState newState,
        string reason,
        float changedAtGameTime
    )
    {
        Assert.That(args.playerIndex, Is.EqualTo(playerIndex));
        Assert.That(args.oldState, Is.EqualTo(oldState));
        Assert.That(args.newState, Is.EqualTo(newState));
        Assert.That(args.reason, Is.EqualTo(reason));
        Assert.That(args.changedAtGameTime, Is.EqualTo(changedAtGameTime));
    }

    private static void AssertFinishEvent(
        GCPlayerFinishStateChangedEventArgs args,
        int playerIndex,
        GCPlayerFinishState oldState,
        GCPlayerFinishState newState,
        string reason,
        float changedAtGameTime
    )
    {
        Assert.That(args.playerIndex, Is.EqualTo(playerIndex));
        Assert.That(args.oldState, Is.EqualTo(oldState));
        Assert.That(args.newState, Is.EqualTo(newState));
        Assert.That(args.reason, Is.EqualTo(reason));
        Assert.That(args.changedAtGameTime, Is.EqualTo(changedAtGameTime));
    }

    private static void AssertSnapshotPlayer(
        GCRuntimeStateSnapshotPlayer player,
        int playerIndex,
        int score,
        int lives,
        string status,
        string statusText,
        int meter,
        int placement,
        string eliminationState,
        string finishState
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
    }
}
