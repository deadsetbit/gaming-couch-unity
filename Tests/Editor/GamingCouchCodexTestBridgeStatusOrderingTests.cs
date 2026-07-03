using System;
using System.Collections.Generic;
using NUnit.Framework;

// Remediation Task 1: a synchronously-completed EditMode run must report its true terminal status
// and never be clobbered back to "started" (which made the Python harness time out on success).
public sealed class GamingCouchCodexTestBridgeStatusOrderingTests
{
    [Test]
    public void SynchronousRunLeavesTerminalStatusAsTheLastWrite()
    {
        var request = new GamingCouchCodexTestBridge.CodexTestRequest
        {
            requestId = Guid.NewGuid().ToString("N"),
            testMode = "EditMode",
            runSynchronously = true
        };

        var writtenStates = new List<string>();
        var runActive = true;

        // Simulate Unity's synchronous Execute: RunFinished fires before Execute returns, writing
        // the terminal status and clearing the active run.
        Func<string> execute = () =>
        {
            writtenStates.Add("completed");
            runActive = false;
            return Guid.NewGuid().ToString("N");
        };

        GamingCouchCodexTestBridge.RunAndTrackStatus(
            request,
            execute,
            status => writtenStates.Add(status.state),
            () => runActive,
            jobId => { }
        );

        Assert.That(writtenStates, Is.Not.Empty);
        Assert.That(
            writtenStates[writtenStates.Count - 1],
            Is.EqualTo("completed"),
            "A synchronously-completed run must not be overwritten with a later 'started' status."
        );
    }

    [Test]
    public void AsynchronousRunWritesStartedAndRecordsTheJobId()
    {
        var request = new GamingCouchCodexTestBridge.CodexTestRequest
        {
            requestId = Guid.NewGuid().ToString("N"),
            testMode = "EditMode"
        };

        var writtenStates = new List<string>();
        string recordedJobId = null;

        // Asynchronous Execute returns before completion; the run stays active for a later callback.
        Func<string> execute = () => "async-job-id";

        GamingCouchCodexTestBridge.RunAndTrackStatus(
            request,
            execute,
            status => writtenStates.Add(status.state),
            () => true,
            jobId => recordedJobId = jobId
        );

        Assert.That(writtenStates[writtenStates.Count - 1], Is.EqualTo("started"));
        Assert.That(recordedJobId, Is.EqualTo("async-job-id"));
    }
}
