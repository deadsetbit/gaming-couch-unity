using System.IO;
using NUnit.Framework;

public sealed class GamingCouchCodexTestBridgeActivationGateTests
{
    [Test]
    public void MarkerPresenceActivatesRegardlessOfEnvironment()
    {
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(true, null), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(true, string.Empty), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(true, "0"), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(true, "false"), Is.True);
    }

    [Test]
    public void TruthyEnvironmentValuesActivateWithoutMarker()
    {
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "1"), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "true"), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "yes"), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "YES"), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "  true  "), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, " yes "), Is.True);
    }

    [Test]
    public void FalsyOrMissingEnvironmentWithoutMarkerStaysInert()
    {
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, null), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, string.Empty), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "   "), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "0"), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "false"), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "no"), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "enabled"), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsActivationRequested(false, "2"), Is.False);
    }

    [Test]
    public void ActivationMarkerPathIsInsideProjectDotGamingcouch()
    {
        Assert.That(
            GamingCouchCodexTestBridge.ActivationMarkerPath,
            Does.EndWith(Path.Combine(".gamingcouch", "codex-bridge.enabled"))
        );
        Assert.That(
            GamingCouchCodexTestBridge.IsPathInsideDirectory(
                GamingCouchCodexTestBridge.ActivationMarkerPath,
                GamingCouchCodexTestBridge.ProjectPath
            ),
            Is.True
        );
    }
}
