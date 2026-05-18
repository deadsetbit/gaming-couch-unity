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
}
