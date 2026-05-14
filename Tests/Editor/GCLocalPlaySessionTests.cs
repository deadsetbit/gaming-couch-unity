using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GCLocalPlaySessionTests
{
    [Test]
    public void ValidCaptureIsCachedAndReusedForSetupAndPlayAccess()
    {
        using (var fixture = new LocalPlayFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 3)));

            var readCount = 0;
            using (GCLocalPlaySession.OverrideForTests(
                () =>
                {
                    readCount++;
                    return fixture.DevStore.Read();
                },
                null,
                null
            ))
            {
                Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
                Assert.That(readCount, Is.EqualTo(1));

                Assert.That(
                    GCLocalPlaySession.TryRequireCapturedSetupOptions("Test setup", out var setupOptions),
                    Is.True
                );
                Assert.That(setupOptions.mode, Is.EqualTo(GCMode.Development));
                Assert.That(setupOptions.isServer, Is.True);
                Assert.That(setupOptions.gameModeId, Is.EqualTo("duel"));

                Assert.That(
                    GCLocalPlaySession.TryRequireCapturedPlayOptions("Test play", out var playOptions, out var seatIdentities),
                    Is.True
                );
                Assert.That(playOptions.seed, Is.EqualTo(12345));
                Assert.That(playOptions.players, Has.Length.EqualTo(2));
                Assert.That(seatIdentities, Has.Length.EqualTo(2));
                Assert.That(seatIdentities[0].sourceSeatIndex, Is.EqualTo(1));
                Assert.That(seatIdentities[1].sourceSeatIndex, Is.EqualTo(3));
                Assert.That(readCount, Is.EqualTo(1));
            }
        }
    }

    [Test]
    public void FailedCaptureBlocksSetupAndPlayAccessWithValidationDetails()
    {
        using (var fixture = new LocalPlayFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 1, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 2)));

            using (GCLocalPlaySession.OverrideForTests(() => fixture.DevStore.Read(), null, null))
            {
                LogAssert.Expect(LogType.Error, new Regex("MetadataEnabledSeatsAboveMaximum"));
                Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.False);

                var activeCapture = GCLocalPlaySession.GetActiveCaptureForTests();
                Assert.That(activeCapture, Is.Not.Null);
                Assert.That(activeCapture.success, Is.False);
                Assert.That(
                    FindIssue(activeCapture.validation, GCDevJsonIssueCode.MetadataEnabledSeatsAboveMaximum),
                    Is.Not.Null
                );

                LogAssert.Expect(LogType.Error, new Regex("Test setup blocked because root gc\\.dev\\.json"));
                Assert.That(
                    GCLocalPlaySession.TryRequireCapturedSetupOptions("Test setup", out _),
                    Is.False
                );

                LogAssert.Expect(LogType.Error, new Regex("Test play blocked because root gc\\.dev\\.json"));
                Assert.That(
                    GCLocalPlaySession.TryRequireCapturedPlayOptions("Test play", out _, out _),
                    Is.False
                );
            }
        }
    }

    [Test]
    public void RestartPreflightFailureDoesNotClearOrReplaceActiveCapture()
    {
        using (var fixture = new LocalPlayFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "111", BuildSeats(1)));

            using (GCLocalPlaySession.OverrideForTests(
                () => fixture.DevStore.Read(),
                _ => GCLocalPlaySessionPreflightResult.Failed(
                    "Gaming Couch restart blocked by test.",
                    fixture.DevJsonPath,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(
                        GCDevJsonIssueCode.WriteError,
                        "test failure",
                        fixture.DevJsonPath
                    ))
                ),
                null
            ))
            {
                Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
                var activeBeforePreflight = GCLocalPlaySession.GetActiveCaptureForTests();

                var preflightResult = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.GamingCouchRestart);

                Assert.That(preflightResult.success, Is.False);
                Assert.That(GCLocalPlaySession.GetActiveCaptureForTests(), Is.SameAs(activeBeforePreflight));
                Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(111));
            }
        }
    }

    [Test]
    public void RootValidationStillRunsAfterRegisteredPreflightSucceeds()
    {
        using (var fixture = new LocalPlayFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 1, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 2)));

            var preflightCount = 0;
            var readCount = 0;
            using (GCLocalPlaySession.OverrideForTests(
                () =>
                {
                    readCount++;
                    return fixture.DevStore.Read();
                },
                _ =>
                {
                    preflightCount++;
                    return GCLocalPlaySessionPreflightResult.Succeeded();
                },
                null
            ))
            {
                var result = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.UnityPlayModeEntry);

                Assert.That(preflightCount, Is.EqualTo(1));
                Assert.That(readCount, Is.EqualTo(1));
                Assert.That(result.success, Is.False);
                Assert.That(
                    FindIssue(result.validation, GCDevJsonIssueCode.MetadataEnabledSeatsAboveMaximum),
                    Is.Not.Null
                );
            }
        }
    }

    [Test]
    public void SuccessfulRestartPreflightFollowedByRecaptureReadsLatestLocalPlaySettings()
    {
        using (var fixture = new LocalPlayFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
            fixture.WriteDevJson(BuildDevJson("duel", "111", BuildSeats(1)));

            using (GCLocalPlaySession.OverrideForTests(
                () => fixture.DevStore.Read(),
                _ => GCLocalPlaySessionPreflightResult.Succeeded(),
                null
            ))
            {
                Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
                Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(111));

                fixture.WriteDevJson(BuildDevJson("duel", "222", BuildSeats(1, 2)));
                var preflightResult = GCLocalPlaySession.RunPreflight(GCLocalPlaySessionBoundary.GamingCouchRestart);

                Assert.That(preflightResult.success, Is.True);
                Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(111));
                Assert.That(GCLocalPlaySession.CaptureForRestart(), Is.True);
                Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.seed, Is.EqualTo(222));
                Assert.That(GCLocalPlaySession.GetActiveCaptureForTests().playOptions.players, Has.Length.EqualTo(2));
            }
        }
    }

    [Test]
    public void CaptureSuccessNotificationFiresOnlyAfterSuccessfulCapture()
    {
        using (var fixture = new LocalPlayFixture())
        {
            fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 1, true));
            fixture.WriteDevJson(BuildDevJson("duel", "12345", BuildSeats(1, 2)));

            var captureSucceededCount = 0;
            using (GCLocalPlaySession.OverrideForTests(
                () => fixture.DevStore.Read(),
                null,
                () => captureSucceededCount++
            ))
            {
                LogAssert.Expect(LogType.Error, new Regex("MetadataEnabledSeatsAboveMaximum"));
                Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.False);
                Assert.That(captureSucceededCount, Is.EqualTo(0));

                fixture.WriteMetadataJson(BuildMetadataJson("unity", "duel", 1, 4, true));
                Assert.That(GCLocalPlaySession.CaptureForRuntimeEntry(), Is.True);
                Assert.That(captureSucceededCount, Is.EqualTo(1));
            }
        }
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

    private static string BuildDevJson(string entryKey, string seed, string seatsJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.AppendLine("  \"devVersion\": 2,");
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
        builder.AppendLine("    \"key\": \"local-play-session-test\",");
        builder.AppendLine("    \"name\": \"Local Play Session Test\",");
        builder.AppendLine("    \"entries\": {");
        builder.AppendLine("      \"" + entryKey + "\": {");
        builder.AppendLine("        \"name\": \"Duel\",");
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
        var builder = new StringBuilder();
        builder.AppendLine("[");
        for (var seatIndex = 1; seatIndex <= GCDevJsonFile.SeatCount; seatIndex++)
        {
            builder.AppendLine("  {");
            builder.AppendLine("    \"name\": \"P" + seatIndex + "\",");
            builder.AppendLine("    \"enabled\": " + FormatBool(Contains(enabledSeats, seatIndex)) + ",");
            builder.AppendLine("    \"isBot\": false");
            builder.Append("  }");
            if (seatIndex < GCDevJsonFile.SeatCount)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        builder.Append("]");
        return builder.ToString();
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

    private sealed class LocalPlayFixture : IDisposable
    {
        private readonly string rootPath;

        internal LocalPlayFixture()
        {
            rootPath = Path.Combine(Path.GetTempPath(), "GCLocalPlaySessionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            var resolver = new FakeLocalProjectRootResolver(rootPath);
            DevStore = new GCDevJsonStore(resolver);
        }

        internal GCDevJsonStore DevStore { get; private set; }

        internal string DevJsonPath
        {
            get { return Path.Combine(rootPath, GCDevJsonFile.FileName); }
        }

        private string MetadataJsonPath
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
            return "Local Play Session Test";
        }
    }
}
