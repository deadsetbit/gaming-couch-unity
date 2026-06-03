using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DSB.GC.RuntimeMessages
{
    internal static class GCRuntimeMessagePath
    {
        internal const string RuntimeMessages = "runtime_messages";
    }

    internal static class GCRuntimeMessageTypes
    {
        internal const string Diagnostic = "gc.diagnostic";
        internal const string StateSnapshot = "gc.state.snapshot";
    }

    internal sealed class GCRuntimeStateSnapshotPayload
    {
        internal GCRuntimeStateSnapshotGame game;
        internal GCRuntimeStateSnapshotPlayer[] players;

        internal string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"game\":{\"status\":");
            GCRuntimeJson.AppendString(builder, game.status);
            builder.Append("},\"players\":[");

            for (var index = 0; index < players.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                players[index].AppendJson(builder);
            }

            builder.Append("]}");
            return builder.ToString();
        }
    }

    internal sealed class GCRuntimeStateSnapshotGame
    {
        internal string status;
    }

    internal sealed class GCRuntimeStateSnapshotPlayer
    {
        internal int playerIndex;
        internal int score;
        internal int lives;
        internal string status;
        internal string statusText;
        internal int meter;
        internal int placement;
        internal string eliminationState;
        internal string finishState;

        internal void AppendJson(StringBuilder builder)
        {
            builder.Append("{\"playerIndex\":").Append(playerIndex);
            builder.Append(",\"score\":").Append(score);
            builder.Append(",\"lives\":").Append(lives);
            builder.Append(",\"status\":");
            GCRuntimeJson.AppendString(builder, status);
            builder.Append(",\"statusText\":");
            GCRuntimeJson.AppendString(builder, statusText);
            builder.Append(",\"meter\":").Append(meter);
            builder.Append(",\"placement\":").Append(placement);
            builder.Append(",\"eliminationState\":");
            GCRuntimeJson.AppendString(builder, eliminationState);
            builder.Append(",\"finishState\":");
            GCRuntimeJson.AppendString(builder, finishState);
            builder.Append("}");
        }
    }

    internal static class GCRuntimeStateSnapshotBuilder
    {
        internal static GCRuntimeStateSnapshotPayload BuildPayload(
            GCStatus gameStatus,
            IReadOnlyList<GCPlayer> players,
            IEnumerable<GCPlayer> playersByPlacement
        )
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            var placementsByPlayer = BuildPlacementsByPlayer(players, playersByPlacement);
            var snapshotPlayers = new GCRuntimeStateSnapshotPlayer[players.Count];
            var seenPlayerIndices = new bool[players.Count];

            for (var index = 0; index < players.Count; index++)
            {
                var player = players[index] ?? throw new ArgumentException("Runtime state snapshots cannot contain null players.", nameof(players));
                if (player.Index < 0 || player.Index >= players.Count)
                {
                    throw new ArgumentException("Runtime state snapshot playerIndex must be within the active player range.", nameof(players));
                }

                if (seenPlayerIndices[player.Index])
                {
                    throw new ArgumentException("Runtime state snapshots cannot contain duplicate playerIndex values.", nameof(players));
                }

                seenPlayerIndices[player.Index] = true;

                if (!placementsByPlayer.TryGetValue(player, out var placement))
                {
                    throw new ArgumentException("Runtime state snapshot placements must contain every active player exactly once.", nameof(playersByPlacement));
                }

                snapshotPlayers[index] = new GCRuntimeStateSnapshotPlayer
                {
                    playerIndex = player.Index,
                    score = player.Score,
                    lives = player.Lives,
                    status = player.Status.ToString(),
                    statusText = player.StatusText ?? "",
                    meter = player.Meter,
                    placement = placement,
                    eliminationState = player.EliminationState.ToString(),
                    finishState = player.FinishState.ToString(),
                };
            }

            return new GCRuntimeStateSnapshotPayload
            {
                game = new GCRuntimeStateSnapshotGame
                {
                    status = ToSnapshotGameStatus(gameStatus),
                },
                players = snapshotPlayers,
            };
        }

        internal static string BuildPayloadJson(
            GCStatus gameStatus,
            IReadOnlyList<GCPlayer> players,
            IEnumerable<GCPlayer> playersByPlacement
        )
        {
            return BuildPayload(gameStatus, players, playersByPlacement).ToJson();
        }

        private static Dictionary<GCPlayer, int> BuildPlacementsByPlayer(
            IReadOnlyList<GCPlayer> players,
            IEnumerable<GCPlayer> playersByPlacement
        )
        {
            if (playersByPlacement == null)
            {
                throw new ArgumentNullException(nameof(playersByPlacement));
            }

            var placementsByPlayer = new Dictionary<GCPlayer, int>();
            var placement = 1;
            foreach (var player in playersByPlacement)
            {
                if (player == null)
                {
                    throw new ArgumentException("Runtime state snapshot placements cannot contain null players.", nameof(playersByPlacement));
                }

                if (placementsByPlayer.ContainsKey(player))
                {
                    throw new ArgumentException("Runtime state snapshot placements cannot contain duplicate players.", nameof(playersByPlacement));
                }

                placementsByPlayer[player] = placement;
                placement++;
            }

            if (placementsByPlayer.Count != players.Count)
            {
                throw new ArgumentException("Runtime state snapshot placements must match the active player count.", nameof(playersByPlacement));
            }

            return placementsByPlayer;
        }

        private static string ToSnapshotGameStatus(GCStatus status)
        {
            switch (status)
            {
                case GCStatus.PendingSetup:
                    return "pending_setup";
                case GCStatus.SetupDone:
                    return "setup_done";
                case GCStatus.Playing:
                    return "playing";
                case GCStatus.GameOver:
                    return "game_over";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown GamingCouch status.");
            }
        }
    }

    internal sealed class GCRuntimeMessageRecord
    {
        internal const int SchemaVersion = 1;

        internal readonly string messageType;
        internal readonly long sequence;
        internal readonly long runtimeTimeMs;
        internal readonly string payloadJson;

        internal GCRuntimeMessageRecord(string messageType, long sequence, long runtimeTimeMs, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                throw new ArgumentException("Runtime message type is required.", nameof(messageType));
            }

            if (sequence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), "Runtime message sequence must be one-based.");
            }

            if (runtimeTimeMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeTimeMs), "Runtime message time must be non-negative.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new ArgumentException("Runtime message payload JSON is required.", nameof(payloadJson));
            }

            this.messageType = messageType;
            this.sequence = sequence;
            this.runtimeTimeMs = runtimeTimeMs;
            this.payloadJson = payloadJson;
        }

        internal string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"schemaVersion\":").Append(SchemaVersion);
            builder.Append(",\"messageType\":");
            GCRuntimeJson.AppendString(builder, messageType);
            builder.Append(",\"sequence\":").Append(sequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"runtimeTimeMs\":").Append(runtimeTimeMs.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"payload\":").Append(payloadJson);
            builder.Append("}");
            return builder.ToString();
        }
    }

    internal static class GCRuntimeMessageOutput
    {
        internal const int EnvelopeSchemaVersion = 1;

        private static long sequence;
        private static bool hasActiveRun;
        private static double activeRunStartSeconds;
        private static Func<double> realtimeSecondsProvider = DefaultRealtimeSecondsProvider;

        internal static event Action<string> RuntimeMessagesEmitted;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GamingCouchRuntimeMessages(string runtimeMessagesJson);
#endif

        internal static void BeginActiveRun()
        {
            activeRunStartSeconds = NowSeconds();
            sequence = 0;
            hasActiveRun = true;
        }

        internal static GCRuntimeMessageRecord CreateRecord(string messageType, string payloadJson)
        {
            EnsureActiveRun();
            return new GCRuntimeMessageRecord(
                messageType,
                ++sequence,
                ResolveRuntimeTimeMs(),
                payloadJson
            );
        }

        internal static string EmitSingle(string messageType, string payloadJson)
        {
            return EmitBatch(new[] { CreateRecord(messageType, payloadJson) });
        }

        internal static string EmitBatch(IReadOnlyList<GCRuntimeMessageRecord> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Runtime message batches must contain at least one message.", nameof(messages));
            }

            var json = BuildEnvelopeJson(messages);
            RuntimeMessagesEmitted?.Invoke(json);

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchRuntimeMessages(json);
#endif

            return json;
        }

        internal static string BuildEnvelopeJson(IReadOnlyList<GCRuntimeMessageRecord> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Runtime message batches must contain at least one message.", nameof(messages));
            }

            var builder = new StringBuilder();
            builder.Append("{\"type\":");
            GCRuntimeJson.AppendString(builder, GCRuntimeMessagePath.RuntimeMessages);
            builder.Append(",\"schemaVersion\":").Append(EnvelopeSchemaVersion);
            builder.Append(",\"messages\":[");

            for (var index = 0; index < messages.Count; index++)
            {
                if (messages[index] == null)
                {
                    throw new ArgumentException("Runtime message batches cannot contain null messages.", nameof(messages));
                }

                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(messages[index].ToJson());
            }

            builder.Append("]}");
            return builder.ToString();
        }

        internal static void ResetForTests(Func<double> testRealtimeSecondsProvider)
        {
            sequence = 0;
            hasActiveRun = false;
            activeRunStartSeconds = 0;
            realtimeSecondsProvider = testRealtimeSecondsProvider ?? DefaultRealtimeSecondsProvider;
            RuntimeMessagesEmitted = null;
        }

        private static void EnsureActiveRun()
        {
            if (!hasActiveRun)
            {
                BeginActiveRun();
            }
        }

        private static long ResolveRuntimeTimeMs()
        {
            var elapsedSeconds = Math.Max(0, NowSeconds() - activeRunStartSeconds);
            return (long)Math.Floor(elapsedSeconds * 1000.0);
        }

        private static double NowSeconds()
        {
            return realtimeSecondsProvider();
        }

        private static double DefaultRealtimeSecondsProvider()
        {
            return Time.realtimeSinceStartupAsDouble;
        }
    }

    internal static class GCRuntimeJson
    {
        internal static void AppendString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
        }
    }
}
