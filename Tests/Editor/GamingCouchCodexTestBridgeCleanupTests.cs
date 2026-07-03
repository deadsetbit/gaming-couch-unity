using System;
using System.IO;
using NUnit.Framework;

public sealed class GamingCouchCodexTestBridgeCleanupTests
{
    private string rootPath;

    [SetUp]
    public void SetUp()
    {
        rootPath = Path.Combine(Path.GetTempPath(), "GamingCouchCodexTestBridgeCleanupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (rootPath != null && Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    [Test]
    public void PruneOutputsKeepsOnlyTheMostRecentFilesUpToLimit()
    {
        var files = SeedOutputFiles(6);

        GamingCouchCodexTestBridge.PruneOutputs(rootPath, 2);

        // Only the two newest survive; the older four are removed.
        Assert.That(Directory.GetFiles(rootPath).Length, Is.EqualTo(2));
        Assert.That(File.Exists(files[5]), Is.True);
        Assert.That(File.Exists(files[4]), Is.True);
        Assert.That(File.Exists(files[3]), Is.False);
        Assert.That(File.Exists(files[0]), Is.False);
    }

    [Test]
    public void PruneOutputsLeavesFilesUntouchedWhenUnderLimit()
    {
        SeedOutputFiles(3);

        GamingCouchCodexTestBridge.PruneOutputs(rootPath, 10);

        Assert.That(Directory.GetFiles(rootPath).Length, Is.EqualTo(3));
    }

    private string[] SeedOutputFiles(int count)
    {
        var paths = new string[count];
        var baseTimeUtc = DateTime.UtcNow.AddHours(-count);
        for (var index = 0; index < count; index++)
        {
            var path = Path.Combine(rootPath, "gaming-couch-unity-test-" + index + ".json");
            File.WriteAllText(path, "{}");
            // Stagger write times so retention ordering is deterministic (index 0 oldest).
            File.SetLastWriteTimeUtc(path, baseTimeUtc.AddMinutes(index));
            paths[index] = path;
        }

        return paths;
    }
}
