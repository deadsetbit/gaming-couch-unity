using System;
using System.IO;
using NUnit.Framework;

public sealed class GamingCouchCodexTestBridgeSecurityTests
{
    [Test]
    public void ProjectPathMatchRejectsMissingProjectPath()
    {
        Assert.That(GamingCouchCodexTestBridge.IsProjectPathForThisProject(null), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsProjectPathForThisProject(string.Empty), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsProjectPathForThisProject("   "), Is.False);
    }

    [Test]
    public void ProjectPathMatchRequiresCurrentProjectPath()
    {
        Assert.That(
            GamingCouchCodexTestBridge.IsProjectPathForThisProject(GamingCouchCodexTestBridge.ProjectPath),
            Is.True
        );
        Assert.That(
            GamingCouchCodexTestBridge.IsProjectPathForThisProject(GamingCouchCodexTestBridge.ProjectPath + "-other"),
            Is.False
        );
    }

    [Test]
    public void RequestIdMustBeGuidNonceWithoutSeparators()
    {
        Assert.That(GamingCouchCodexTestBridge.IsValidRequestId(Guid.NewGuid().ToString("N")), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsValidRequestId(Guid.NewGuid().ToString("D")), Is.False);
        Assert.That(GamingCouchCodexTestBridge.IsValidRequestId("../escape"), Is.False);
    }

    [Test]
    public void DefaultOutputPathStaysInsideBridgeOutputDirectory()
    {
        var requestId = Guid.NewGuid().ToString("N");

        var outputPath = GamingCouchCodexTestBridge.GetDefaultOutputPath(requestId, "xml");

        Assert.That(
            GamingCouchCodexTestBridge.IsPathInsideDirectory(outputPath, GamingCouchCodexTestBridge.OutputDirectory),
            Is.True
        );
        Assert.That(Path.GetFileName(outputPath), Is.EqualTo("gaming-couch-unity-test-" + requestId + ".xml"));
    }

    [Test]
    public void DefaultOutputPathRejectsInvalidRequestId()
    {
        Assert.Throws<ArgumentException>(() =>
            GamingCouchCodexTestBridge.GetDefaultOutputPath("../outside", "xml")
        );
    }

    // The bridge only creates + 0700/symlink-hardens directories at or below the package-owned
    // "<localappdata>/Gaming Couch" boundary; the user's XDG data home and any sibling apps above it
    // are never touched. This is the boundary truth table the PrepareBridgeSession tree walk relies on.
    [Test]
    public void PackageBoundaryGuardsOnlyTheGamingCouchSubtree()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), "xdg-data-home");
        var boundary = GamingCouchCodexTestBridge.GetAppDataBoundaryDirectory(localAppData);
        var bridgeRoot = Path.Combine(boundary, "CodexTestBridge", "abcdef0123456789");
        var siblingApp = Path.Combine(localAppData, "SomeOtherApp");

        Assert.That(boundary, Does.EndWith("Gaming Couch"));

        // The user's XDG data home sits ABOVE the boundary: not guarded, never chmod-ed or rejected.
        Assert.That(GamingCouchCodexTestBridge.IsPathInsideDirectory(localAppData, boundary), Is.False);
        // A neighbouring app's directory under the same XDG home is likewise off-limits.
        Assert.That(GamingCouchCodexTestBridge.IsPathInsideDirectory(siblingApp, boundary), Is.False);

        // The boundary itself and everything below it (the bridge session tree) is guarded.
        Assert.That(GamingCouchCodexTestBridge.IsPathInsideDirectory(boundary, boundary), Is.True);
        Assert.That(GamingCouchCodexTestBridge.IsPathInsideDirectory(bridgeRoot, boundary), Is.True);
    }

    [Test]
    public void RealBridgeRootLivesInsideThePackageBoundary()
    {
        // Guards the tree walk's precondition: PrepareBridgeSession passes AppDataBoundaryDirectory as
        // the guarded root, so BridgeRootDirectory must stay inside it (otherwise the walk throws).
        Assert.That(
            GamingCouchCodexTestBridge.IsPathInsideDirectory(
                GamingCouchCodexTestBridge.BridgeRootDirectory,
                GamingCouchCodexTestBridge.AppDataBoundaryDirectory
            ),
            Is.True
        );
    }
}
