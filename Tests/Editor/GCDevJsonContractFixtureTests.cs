using System;
using System.IO;
using System.Text;
using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCDevJsonContractFixtureTests
{
    [Test]
    public void ValidSparseRosterCapturesDensePlayersAndStableSeatIdentities()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 3, 8)));

            var readResult = fixture.DevStore.Read();
            var capture = GCLocalPlaySession.Capture(readResult);

            Assert.That(readResult.IsValid, Is.True);
            Assert.That(capture.success, Is.True);
            Assert.That(capture.setupOptions.mode, Is.EqualTo(GCMode.Development));
            Assert.That(capture.setupOptions.isServer, Is.True);
            Assert.That(capture.setupOptions.gameModeId, Is.EqualTo("duel"));
            Assert.That(capture.playOptions.seed, Is.EqualTo(12345));
            Assert.That(capture.playOptions.players, Has.Length.EqualTo(3));
            AssertPlayer(capture.playOptions.players[0], 1, "P1", GCPlayerType.player, GCPlayerColor.blue);
            AssertPlayer(capture.playOptions.players[1], 2, "P3", GCPlayerType.player, GCPlayerColor.green);
            AssertPlayer(capture.playOptions.players[2], 3, "P8", GCPlayerType.player, GCPlayerColor.brown);
            Assert.That(capture.seatIdentities, Has.Length.EqualTo(3));
            AssertSeatIdentity(capture.seatIdentities[0], 1, 1, GCPlayerType.player, GCPlayerColor.blue);
            AssertSeatIdentity(capture.seatIdentities[1], 2, 3, GCPlayerType.player, GCPlayerColor.green);
            AssertSeatIdentity(capture.seatIdentities[2], 3, 8, GCPlayerType.player, GCPlayerColor.brown);
        }
    }

    [Test]
    public void MissingMetadataKeepsValidDevJsonReadableWithWarningOnlyIssue()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1)));

            var readResult = fixture.DevStore.Read();
            var issue = FindIssue(readResult.validation, GCDevJsonIssueCode.MissingMetadataFile);

            Assert.That(readResult.IsValid, Is.True);
            Assert.That(readResult.validation.ErrorCount, Is.EqualTo(0));
            Assert.That(readResult.validation.WarningCount, Is.EqualTo(1));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Warning));
        }
    }

    [Test]
    public void MetadataMaxPlayerGateFailsValidationAndCapture()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 2, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 3, 8)));

            var readResult = fixture.DevStore.Read();
            var capture = GCLocalPlaySession.Capture(readResult);
            var issue = FindIssue(readResult.validation, GCDevJsonIssueCode.MetadataEnabledSeatsAboveMaximum);

            Assert.That(readResult.IsValid, Is.False);
            Assert.That(capture.success, Is.False);
            Assert.That(capture.validation, Is.SameAs(readResult.validation));
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Error));
        }
    }

    [Test]
    public void WrongSeatCountFailsWithExistingIssueCode()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeatsWithCount(7, 1, 3)));

            var readResult = fixture.DevStore.Read();
            var issue = FindIssue(readResult.validation, GCDevJsonIssueCode.InvalidSeatCount);

            Assert.That(readResult.IsValid, Is.False);
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Error));
        }
    }

    [Test]
    public void UnsupportedDevVersionFailsWithExistingIssueCode()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1), 3));

            var readResult = fixture.DevStore.Read();
            var issue = FindIssue(readResult.validation, GCDevJsonIssueCode.UnsupportedDevVersion);

            Assert.That(readResult.IsValid, Is.False);
            Assert.That(issue, Is.Not.Null);
            Assert.That(issue.severity, Is.EqualTo(GCDevJsonIssueSeverity.Error));
        }
    }

    [Test]
    public void PreservingWriteKeepsUnrelatedTopLevelDevJsonFields()
    {
        using (var fixture = new ContractFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "coop", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 3, 8), 2, true));

            var writeResult = fixture.DevStore.Write(new GCDevJsonFile("coop", "98765", BuildSeatData(2, 4)));
            var writtenText = File.ReadAllText(fixture.DevJsonPath, Encoding.UTF8);
            var rewrittenReadResult = fixture.DevStore.Read();

            Assert.That(writeResult.success, Is.True);
            Assert.That(rewrittenReadResult.IsValid, Is.True);
            Assert.That(rewrittenReadResult.data.devVersion, Is.EqualTo(GCDevJsonFile.SupportedDevVersion));
            Assert.That(rewrittenReadResult.data.entryKey, Is.EqualTo("coop"));
            Assert.That(rewrittenReadResult.data.seed, Is.EqualTo("98765"));
            Assert.That(rewrittenReadResult.data.seats, Has.Length.EqualTo(GCDevJsonFile.SeatCount));
            Assert.That(writtenText, Does.Contain("\"unrelatedTopLevel\": \"keep-me\""));
            Assert.That(writtenText, Does.Contain("\"nestedUnrelated\": {"));
            Assert.That(writtenText, Does.Contain("\"id\": \"contract-fixture\""));
            Assert.That(writtenText.EndsWith("\n", StringComparison.Ordinal), Is.True);
        }
    }

    private static void AssertPlayer(
        GCPlayerOptions player,
        int playerId,
        string name,
        GCPlayerType type,
        GCPlayerColor color
    )
    {
        Assert.That(player.playerId, Is.EqualTo(playerId));
        Assert.That(player.name, Is.EqualTo(name));
        Assert.That(player.type, Is.EqualTo(type.ToString()));
        Assert.That(player.color, Is.EqualTo(color.ToString()));
    }

    private static void AssertSeatIdentity(
        GCSeatIdentity identity,
        int playerId,
        int sourceSeatIndex,
        GCPlayerType type,
        GCPlayerColor color
    )
    {
        Assert.That(identity.playerId, Is.EqualTo(playerId));
        Assert.That(identity.sourceSeatIndex, Is.EqualTo(sourceSeatIndex));
        Assert.That(identity.label, Is.EqualTo("Seat " + sourceSeatIndex));
        Assert.That(identity.playerType, Is.EqualTo(type));
        Assert.That(identity.playerColor, Is.EqualTo(color));
    }

    private static GCDevJsonIssue FindIssue(GCDevJsonValidationResult validation, GCDevJsonIssueCode code)
    {
        if (validation == null || validation.issues == null)
        {
            return null;
        }

        for (var index = 0; index < validation.issues.Length; index++)
        {
            var issue = validation.issues[index];
            if (issue != null && issue.code == code)
            {
                return issue;
            }
        }

        return null;
    }

    private static string BuildDevJson(string entryKey, string seed, string seatsJson, int devVersion = 2, bool includeUnknownFields = false)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        if (includeUnknownFields)
        {
            builder.AppendLine("  \"unrelatedTopLevel\": \"keep-me\",");
            builder.AppendLine("  \"nestedUnrelated\": {");
            builder.AppendLine("    \"id\": \"contract-fixture\"");
            builder.AppendLine("  },");
        }

        builder.AppendLine("  \"devVersion\": " + devVersion + ",");
        builder.AppendLine("  \"entryKey\": \"" + entryKey + "\",");
        builder.AppendLine("  \"seed\": \"" + seed + "\",");
        builder.AppendLine("  \"seats\": " + seatsJson);
        builder.Append("}");
        return builder.ToString();
    }

    private static string BuildMetadataJson(
        string platformId,
        string entryKey,
        int minPlayers,
        int maxPlayers,
        bool botSupport
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"game\": {");
        builder.AppendLine("    \"key\": \"contract-game\",");
        builder.AppendLine("    \"name\": \"Contract Game\",");
        builder.AppendLine("    \"entries\": {");
        builder.AppendLine("      \"" + entryKey + "\": {");
        builder.AppendLine("        \"name\": \"Contract Entry\",");
        builder.AppendLine("        \"minPlayers\": " + minPlayers + ",");
        builder.AppendLine("        \"maxPlayers\": " + maxPlayers + ",");
        builder.AppendLine("        \"botSupport\": " + FormatBool(botSupport));
        builder.AppendLine("      }");
        builder.AppendLine("    }");
        builder.AppendLine("  },");
        builder.AppendLine("  \"platform\": {");
        builder.AppendLine("    \"id\": \"" + platformId + "\"");
        builder.AppendLine("  },");
        builder.AppendLine("  \"properties\": {");
        builder.AppendLine("    \"colors\": {");
        builder.AppendLine("      \"players\": {}");
        builder.AppendLine("    }");
        builder.AppendLine("  }");
        builder.Append("}");
        return builder.ToString();
    }

    private static string BuildSeats(params int[] enabledSeats)
    {
        return BuildSeatsWithCount(GCDevJsonFile.SeatCount, enabledSeats);
    }

    private static string BuildSeatsWithCount(int seatCount, params int[] enabledSeats)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[");
        for (var seatIndex = 1; seatIndex <= seatCount; seatIndex++)
        {
            builder.AppendLine("  {");
            builder.AppendLine("    \"name\": \"P" + seatIndex + "\",");
            builder.AppendLine("    \"enabled\": " + FormatBool(Contains(enabledSeats, seatIndex)) + ",");
            builder.AppendLine("    \"isBot\": false");
            builder.Append("  }");
            if (seatIndex < seatCount)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        builder.Append("]");
        return builder.ToString();
    }

    private static GCDevJsonSeat[] BuildSeatData(params int[] enabledSeats)
    {
        var seats = new GCDevJsonSeat[GCDevJsonFile.SeatCount];
        for (var seatIndex = 1; seatIndex <= GCDevJsonFile.SeatCount; seatIndex++)
        {
            seats[seatIndex - 1] = new GCDevJsonSeat("P" + seatIndex, Contains(enabledSeats, seatIndex), false);
        }

        return seats;
    }

    private static bool Contains(int[] values, int expected)
    {
        if (values == null)
        {
            return false;
        }

        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == expected)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
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
            MetadataStore = new GCMetadataJsonStore(resolver);
        }

        internal GCDevJsonStore DevStore { get; private set; }

        internal GCMetadataJsonStore MetadataStore { get; private set; }

        internal string DevJsonPath
        {
            get { return Path.Combine(rootPath, GCDevJsonFile.FileName); }
        }

        internal string MetadataJsonPath
        {
            get { return Path.Combine(rootPath, GCMetadataJsonFile.FileName); }
        }

        internal void WriteDevJson(string contents)
        {
            File.WriteAllText(DevJsonPath, contents + "\n", new UTF8Encoding(false));
        }

        internal void WriteMetadataJson(string contents)
        {
            File.WriteAllText(MetadataJsonPath, contents + "\n", new UTF8Encoding(false));
        }

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, true);
            }
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
}
