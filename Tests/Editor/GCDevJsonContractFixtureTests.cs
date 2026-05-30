using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;
using UnityEngine;

public sealed class GCDevJsonContractFixtureTests
{
    private const string CorpusRelativePath = "ContractFixtures/LocalPlay";

    [Test]
    public void ValidSparseRosterCapturesDensePlayersAndStableSeatIdentities()
    {
        RunContractFixtureCase("valid-sparse-roster-capture");
    }

    [Test]
    public void MissingPlatformDataKeepsValidDevJsonReadableWithWarningOnlyIssue()
    {
        RunContractFixtureCase("missing-platform-data-warning-only");
    }

    [Test]
    public void PlatformDataMaxPlayerGateFailsValidationAndCapture()
    {
        RunContractFixtureCase("platform-data-max-player-gate-failure");
    }

    [Test]
    public void WrongSeatCountFailsWithExistingIssueCode()
    {
        RunContractFixtureCase("wrong-seat-count-failure");
    }

    [Test]
    public void UnsupportedDevVersionFailsWithExistingIssueCode()
    {
        RunContractFixtureCase("unsupported-dev-version-failure");
    }

    [Test]
    public void PreservingWriteKeepsUnrelatedTopLevelDevJsonFields()
    {
        RunContractFixtureCase("preserving-write-unrelated-top-level-fields");
    }

    private static void RunContractFixtureCase(string caseName)
    {
        var casePath = ResolveCasePath(caseName);
        var expected = LoadExpected(caseName, casePath);

        using (var fixture = new ContractFixture())
        {
            fixture.CopyCorpusFiles(casePath);

            var platformDataReadResult = fixture.PlatformDataStore.Read();
            var readResult = fixture.DevStore.Read(platformDataReadResult);

            AssertReadResult(readResult, expected);
            AssertCapture(fixture, readResult, expected.capture);
            AssertWrite(fixture, expected.write);
        }
    }

    private static string ResolveCasePath(string caseName)
    {
        var corpusRoot = ResolveCorpusRoot();
        var casePath = Path.Combine(corpusRoot, caseName);
        Assert.That(Directory.Exists(casePath), Is.True, "Missing contract fixture case: " + casePath);
        return casePath;
    }

    private static string ResolveCorpusRoot()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GCDevJsonContractFixtureTests).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            var packageCorpusRoot = Path.Combine(packageInfo.resolvedPath, CorpusRelativePath);
            if (Directory.Exists(packageCorpusRoot))
            {
                return packageCorpusRoot;
            }
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, CorpusRelativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate contract fixture corpus at " + CorpusRelativePath + ".");
        return null;
    }

    private static ExpectedFixture LoadExpected(string caseName, string casePath)
    {
        var expectedPath = Path.Combine(casePath, "expected.json");
        Assert.That(File.Exists(expectedPath), Is.True, "Missing expected.json for contract fixture case: " + caseName);

        var expected = JsonUtility.FromJson<ExpectedFixture>(File.ReadAllText(expectedPath, Encoding.UTF8));
        Assert.That(expected, Is.Not.Null, "expected.json could not be parsed for contract fixture case: " + caseName);
        Assert.That(expected.id, Is.EqualTo(caseName));
        return expected;
    }

    private static void AssertReadResult(GCDevJsonReadResult readResult, ExpectedFixture expected)
    {
        Assert.That(readResult, Is.Not.Null);
        Assert.That(readResult.IsValid, Is.EqualTo(expected.valid));
        Assert.That(readResult.validation, Is.Not.Null);

        var expectedIssues = expected.issues ?? Array.Empty<ExpectedIssue>();
        Assert.That(readResult.validation.ErrorCount, Is.EqualTo(CountIssues(expectedIssues, "Error")));
        Assert.That(readResult.validation.WarningCount, Is.EqualTo(CountIssues(expectedIssues, "Warning")));
        Assert.That(readResult.validation.issues, Has.Length.EqualTo(expectedIssues.Length));

        for (var index = 0; index < expectedIssues.Length; index++)
        {
            var expectedIssue = expectedIssues[index];
            var issue = FindIssue(readResult.validation, expectedIssue.code);
            Assert.That(issue, Is.Not.Null, "Expected issue code was not found: " + expectedIssue.code);
            Assert.That(issue.severity.ToString(), Is.EqualTo(expectedIssue.severity));
        }
    }

    private static int CountIssues(ExpectedIssue[] issues, string severity)
    {
        var count = 0;
        for (var index = 0; index < issues.Length; index++)
        {
            if (issues[index] != null && issues[index].severity == severity)
            {
                count++;
            }
        }

        return count;
    }

    private static void AssertCapture(ContractFixture fixture, GCDevJsonReadResult readResult, ExpectedCapture expected)
    {
        if (expected == null || !expected.assert)
        {
            return;
        }

        var capture = new GCDevJsonLocalPlaySessionProvider(fixture.DevStore).Capture(readResult);
        Assert.That(capture.success, Is.EqualTo(expected.success));

        if (!expected.success)
        {
            Assert.That(capture.validation, Is.Not.Null);
            Assert.That(capture.validation.ErrorCount, Is.EqualTo(readResult.validation.ErrorCount));
            Assert.That(capture.validation.WarningCount, Is.EqualTo(readResult.validation.WarningCount));
            return;
        }

        Assert.That(expected.entryKey, Is.Not.Null.And.Not.Empty, "Successful capture fixture must include an entryKey expectation.");
        Assert.That(expected.activePlayers, Is.Not.Null, "Successful capture fixture must include active player expectations.");
        Assert.That(capture.setupOptions, Is.Not.Null);
        Assert.That(capture.setupOptions.mode, Is.EqualTo(GCMode.Development));
        Assert.That(capture.setupOptions.isServer, Is.True);
        Assert.That(capture.setupOptions.gameModeId, Is.EqualTo(expected.entryKey));

        Assert.That(capture.playOptions, Is.Not.Null);
        Assert.That(capture.playOptions.seed, Is.EqualTo(expected.seed));
        AssertPlayers(capture.playOptions.players, expected.activePlayers);
        AssertSeatIdentities(capture.seatIdentities, expected.seatIdentities);
    }

    private static void AssertPlayers(GCPlayerOptions[] players, ExpectedPlayer[] expectedPlayers)
    {
        expectedPlayers = expectedPlayers ?? Array.Empty<ExpectedPlayer>();
        Assert.That(players, Has.Length.EqualTo(expectedPlayers.Length));

        for (var index = 0; index < expectedPlayers.Length; index++)
        {
            var expectedPlayer = expectedPlayers[index];
            var player = players[index];
            Assert.That(player.playerId, Is.EqualTo(expectedPlayer.playerId));
            Assert.That(player.name, Is.EqualTo(expectedPlayer.name));
            Assert.That(player.type, Is.EqualTo(expectedPlayer.type));
            Assert.That(player.color, Is.EqualTo(expectedPlayer.color));
        }
    }

    private static void AssertSeatIdentities(GCSeatIdentity[] identities, ExpectedSeatIdentity[] expectedIdentities)
    {
        expectedIdentities = expectedIdentities ?? Array.Empty<ExpectedSeatIdentity>();
        Assert.That(identities, Has.Length.EqualTo(expectedIdentities.Length));

        for (var index = 0; index < expectedIdentities.Length; index++)
        {
            var expectedIdentity = expectedIdentities[index];
            var identity = identities[index];
            Assert.That(identity.playerId, Is.EqualTo(expectedIdentity.playerId));
            Assert.That(identity.sourceSeatIndex, Is.EqualTo(expectedIdentity.sourceSeatIndex));
            Assert.That(identity.label, Is.EqualTo(expectedIdentity.label));
            Assert.That(identity.playerType.ToString(), Is.EqualTo(expectedIdentity.playerType));
            Assert.That(identity.playerColor.ToString(), Is.EqualTo(expectedIdentity.playerColor));
        }
    }

    private static void AssertWrite(ContractFixture fixture, ExpectedWrite expected)
    {
        if (expected == null || !expected.assert)
        {
            return;
        }

        Assert.That(expected.data, Is.Not.Null, "Write fixture must include canonical gc.dev.json data.");
        var writeResult = fixture.DevStore.Write(ToDevJsonFile(expected.data), fixture.PlatformDataStore.Read());
        Assert.That(writeResult.success, Is.EqualTo(expected.success));
        Assert.That(writeResult.validation, Is.Not.Null);
        Assert.That(writeResult.validation.IsValid, Is.EqualTo(expected.resultValid));

        var writtenText = File.ReadAllText(fixture.DevJsonPath, Encoding.UTF8);
        AssertWrittenText(writtenText, expected.writtenText);
        AssertReadBack(fixture, expected.readBack);
    }

    private static GCDevJsonFile ToDevJsonFile(ExpectedDevJson data)
    {
        var expectedSeats = data.seats ?? Array.Empty<ExpectedSeat>();
        var seats = new GCDevJsonSeat[expectedSeats.Length];
        for (var index = 0; index < expectedSeats.Length; index++)
        {
            var expectedSeat = expectedSeats[index];
            seats[index] = new GCDevJsonSeat(expectedSeat.name, expectedSeat.enabled, expectedSeat.isBot);
        }

        return new GCDevJsonFile(data.devVersion, data.entryKey, data.seed, seats);
    }

    private static void AssertWrittenText(string writtenText, ExpectedWrittenText expected)
    {
        if (expected == null)
        {
            return;
        }

        var expectedContains = expected.contains ?? Array.Empty<string>();
        for (var index = 0; index < expectedContains.Length; index++)
        {
            Assert.That(writtenText, Does.Contain(expectedContains[index]));
        }

        if (expected.endsWithNewline)
        {
            Assert.That(writtenText.EndsWith("\n", StringComparison.Ordinal), Is.True);
        }
    }

    private static void AssertReadBack(ContractFixture fixture, ExpectedReadBack expected)
    {
        if (expected == null || !expected.assert)
        {
            return;
        }

        var readResult = fixture.DevStore.Read(fixture.PlatformDataStore.Read());
        Assert.That(readResult.IsValid, Is.EqualTo(expected.valid));
        Assert.That(readResult.data, Is.Not.Null);
        Assert.That(readResult.data.devVersion, Is.EqualTo(expected.devVersion));
        Assert.That(readResult.data.entryKey, Is.EqualTo(expected.entryKey));
        Assert.That(readResult.data.seed, Is.EqualTo(expected.seed));
        Assert.That(readResult.data.seats, Has.Length.EqualTo(expected.seatCount));
        Assert.That(GetEnabledSeatIndexes(readResult.data.seats), Is.EqualTo(expected.enabledSeatIndexes));
    }

    private static int[] GetEnabledSeatIndexes(GCDevJsonSeat[] seats)
    {
        var enabledSeatIndexes = new List<int>();
        for (var index = 0; index < seats.Length; index++)
        {
            if (seats[index].enabled)
            {
                enabledSeatIndexes.Add(index + 1);
            }
        }

        return enabledSeatIndexes.ToArray();
    }

    private static GCDevJsonIssue FindIssue(GCDevJsonValidationResult validation, string code)
    {
        if (validation == null || validation.issues == null)
        {
            return null;
        }

        for (var index = 0; index < validation.issues.Length; index++)
        {
            var issue = validation.issues[index];
            if (issue != null && issue.code.ToString() == code)
            {
                return issue;
            }
        }

        return null;
    }

    private sealed class ContractFixture : IDisposable
    {
        private readonly string rootPath;

        internal ContractFixture()
        {
            rootPath = Path.Combine(Path.GetTempPath(), "GCDevJsonContractFixtureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            var resolver = new FakeLocalProjectRootResolver(rootPath);
            DevStore = new GCDevJsonStore(resolver);
            PlatformDataStore = new GCPlatformDataStore(resolver);
        }

        internal GCDevJsonStore DevStore { get; private set; }

        internal GCPlatformDataStore PlatformDataStore { get; private set; }

        internal string DevJsonPath
        {
            get { return Path.Combine(rootPath, GCDevJsonFile.FileName); }
        }

        private string PlatformDataJsonPath
        {
            get { return Path.Combine(rootPath, GCPlatformDataFile.FileName); }
        }

        internal void CopyCorpusFiles(string casePath)
        {
            CopyRequiredFile(Path.Combine(casePath, GCDevJsonFile.FileName), DevJsonPath);

            var platformDataSourcePath = Path.Combine(casePath, GCPlatformDataFile.FileName);
            if (File.Exists(platformDataSourcePath))
            {
                File.Copy(platformDataSourcePath, PlatformDataJsonPath, true);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, true);
            }
        }

        private static void CopyRequiredFile(string sourcePath, string destinationPath)
        {
            Assert.That(File.Exists(sourcePath), Is.True, "Missing contract fixture file: " + sourcePath);
            File.Copy(sourcePath, destinationPath, true);
        }
    }

    private sealed class FakeLocalProjectRootResolver : IGCLocalProjectRootResolver
    {
        private readonly string rootPath;

        internal FakeLocalProjectRootResolver(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public string ResolveProjectRootPath()
        {
            return rootPath;
        }

        public string ResolveProjectName()
        {
            return "Contract Fixture";
        }
    }

    [Serializable]
    private sealed class ExpectedFixture
    {
        public string id;
        public bool valid;
        public ExpectedIssue[] issues;
        public ExpectedCapture capture;
        public ExpectedWrite write;
    }

    [Serializable]
    private sealed class ExpectedIssue
    {
        public string code;
        public string severity;
    }

    [Serializable]
    private sealed class ExpectedCapture
    {
        public bool assert;
        public bool success;
        public string entryKey;
        public int seed;
        public ExpectedPlayer[] activePlayers;
        public ExpectedSeatIdentity[] seatIdentities;
    }

    [Serializable]
    private sealed class ExpectedPlayer
    {
        public int playerId;
        public string name;
        public string type;
        public string color;
    }

    [Serializable]
    private sealed class ExpectedSeatIdentity
    {
        public int playerId;
        public int sourceSeatIndex;
        public string label;
        public string playerType;
        public string playerColor;
    }

    [Serializable]
    private sealed class ExpectedWrite
    {
        public bool assert;
        public bool success;
        public bool resultValid;
        public ExpectedDevJson data;
        public ExpectedWrittenText writtenText;
        public ExpectedReadBack readBack;
    }

    [Serializable]
    private sealed class ExpectedDevJson
    {
        public int devVersion;
        public string entryKey;
        public string seed;
        public ExpectedSeat[] seats;
    }

    [Serializable]
    private sealed class ExpectedSeat
    {
        public string name;
        public bool enabled;
        public bool isBot;
    }

    [Serializable]
    private sealed class ExpectedWrittenText
    {
        public string[] contains;
        public bool endsWithNewline;
    }

    [Serializable]
    private sealed class ExpectedReadBack
    {
        public bool assert;
        public bool valid;
        public int devVersion;
        public string entryKey;
        public string seed;
        public int seatCount;
        public int[] enabledSeatIndexes;
    }
}
