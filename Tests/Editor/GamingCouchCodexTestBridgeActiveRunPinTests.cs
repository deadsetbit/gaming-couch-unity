using System;
using NUnit.Framework;

// A PlayMode run's domain reload drops the static callbacks and UTF's non-serialized callback
// registration, so the bridge pins the in-flight request in SessionState and reconstructs the
// callbacks after the reload. The pin (de)serialization is pure, so its round-trip -- which the
// reconstructed run relies on for its output paths and display mode -- is unit-testable without a
// live Editor.
public sealed class GamingCouchCodexTestBridgeActiveRunPinTests
{
    [Test]
    public void ActiveRunRequestSurvivesTheSessionStateRoundTrip()
    {
        var request = new GamingCouchCodexTestBridge.CodexTestRequest
        {
            requestId = Guid.NewGuid().ToString("N"),
            token = "session-token",
            projectPath = "/some/project/root",
            testMode = "PlayMode",
            testNames = new[] { "Suite.TestA", "Suite.TestB" },
            groupNames = new[] { "Group" },
            categoryNames = new[] { "Category" },
            assemblyNames = new[] { "Assembly" },
            runSynchronously = true,
            skipRefresh = true
        };

        var restored = GamingCouchCodexTestBridge.DeserializeActiveRunRequest(
            GamingCouchCodexTestBridge.SerializeActiveRunRequest(request)
        );

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored.requestId, Is.EqualTo(request.requestId));
        Assert.That(restored.token, Is.EqualTo(request.token));
        Assert.That(restored.projectPath, Is.EqualTo(request.projectPath));
        Assert.That(restored.testMode, Is.EqualTo(request.testMode));
        Assert.That(restored.testNames, Is.EqualTo(request.testNames));
        Assert.That(restored.groupNames, Is.EqualTo(request.groupNames));
        Assert.That(restored.categoryNames, Is.EqualTo(request.categoryNames));
        Assert.That(restored.assemblyNames, Is.EqualTo(request.assemblyNames));
        Assert.That(restored.runSynchronously, Is.True);
        Assert.That(restored.skipRefresh, Is.True);

        // The reconstructed request must drive the same display mode the re-attached run reports.
        Assert.That(restored.GetDisplayMode(), Is.EqualTo("PlayMode"));
    }

    [Test]
    public void SerializeActiveRunRequestReturnsEmptyForNull()
    {
        Assert.That(GamingCouchCodexTestBridge.SerializeActiveRunRequest(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void DeserializeActiveRunRequestReturnsNullForMissingPin()
    {
        Assert.That(GamingCouchCodexTestBridge.DeserializeActiveRunRequest(null), Is.Null);
        Assert.That(GamingCouchCodexTestBridge.DeserializeActiveRunRequest(string.Empty), Is.Null);
        Assert.That(GamingCouchCodexTestBridge.DeserializeActiveRunRequest("   "), Is.Null);
    }

    [Test]
    public void StaleActiveRunPinReclaimedOnlyWhenReattachedAndEditorIdle()
    {
        // Deadlock-breaker: a lingering activeCallbacks pin is reclaimed before a new request only
        // when it was re-attached after a domain reload (an interrupted PlayMode run) AND nothing is
        // actually running -- not in play mode, not compiling, not updating.
        Assert.That(GamingCouchCodexTestBridge.ShouldReclaimStaleActiveRun(true, false, false, false), Is.True);
        // A fresh (non-reattached) run -- e.g. a live async EditMode run -- is never reclaimed even
        // when idle. This is the guard against clobbering a live run under overlapping requests.
        Assert.That(GamingCouchCodexTestBridge.ShouldReclaimStaleActiveRun(false, false, false, false), Is.False);
        // Any busy signal keeps the "a run is already active" rejection in force even for a reattached pin.
        Assert.That(GamingCouchCodexTestBridge.ShouldReclaimStaleActiveRun(true, true, false, false), Is.False);
        Assert.That(GamingCouchCodexTestBridge.ShouldReclaimStaleActiveRun(true, false, true, false), Is.False);
        Assert.That(GamingCouchCodexTestBridge.ShouldReclaimStaleActiveRun(true, false, false, true), Is.False);
    }
}
