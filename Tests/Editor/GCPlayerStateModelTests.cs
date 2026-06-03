using System;
using System.Collections.Generic;
using System.Reflection;
using DSB.GC;
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

        Assert.That(store.UneliminatedPlayerCount, Is.EqualTo(1));
        Assert.That(store.EliminatedPlayerCount, Is.EqualTo(0));

        player.SetEliminatedRevokable("temporary");

        Assert.That(store.UneliminatedPlayerCount, Is.EqualTo(0));
        Assert.That(store.EliminatedPlayerCount, Is.EqualTo(1));

        player.SetEliminatedPermanent("promotion");

        Assert.That(store.UneliminatedPlayerCount, Is.EqualTo(0));
        Assert.That(store.EliminatedPlayerCount, Is.EqualTo(1));
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
}
