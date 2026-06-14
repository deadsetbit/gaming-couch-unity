using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DSB.GC.Dev;
using DSB.GC.RuntimeMessages;

namespace DSB.GC
{
    internal sealed class GCActivePlayerMapping
    {
        private readonly GCActivePlayerMappingEntry[] entriesByIndex;
        private readonly Dictionary<int, int> playerIndexBySourceSeatIndex = new Dictionary<int, int>();

        internal string MappingId { get; }
        internal int Seed { get; }
        internal int ParticipantCount => entriesByIndex.Length;

        private GCActivePlayerMapping(string mappingId, int seed, GCActivePlayerMappingEntry[] entriesByIndex)
        {
            MappingId = mappingId;
            Seed = seed;
            this.entriesByIndex = entriesByIndex ?? Array.Empty<GCActivePlayerMappingEntry>();

            for (var index = 0; index < this.entriesByIndex.Length; index++)
            {
                var entry = this.entriesByIndex[index];
                if (entry.SourceSeatIndex > 0)
                {
                    playerIndexBySourceSeatIndex[entry.SourceSeatIndex] = entry.PlayerIndex;
                }

            }
        }

        internal static GCActivePlayerMapping Create(GCPlayOptions options, GCSeatIdentity[] seatIdentities)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var players = options.players ?? Array.Empty<GCActivePlayerOptions>();
            var identities = seatIdentities ?? Array.Empty<GCSeatIdentity>();
            if (identities.Length != players.Length)
            {
                throw new ArgumentException("[GamingCouch] Seat identity count must match active player count.", nameof(seatIdentities));
            }

            if (options.usesMappedActivePlayers)
            {
                return CreateFromMappedActivePlayers(options, identities);
            }

            var participants = new List<Participant>(players.Length);
            for (var capturedOrder = 0; capturedOrder < players.Length; capturedOrder++)
            {
                var identity = identities[capturedOrder];
                var stableKey = !string.IsNullOrWhiteSpace(identity.stableKey)
                    ? identity.stableKey
                    : identity.sourceSeatIndex > 0
                        ? identity.sourceSeatIndex.ToString()
                        : capturedOrder.ToString();

                participants.Add(new Participant
                {
                    CapturedOrder = capturedOrder,
                    Hash = ComputeFnv1A32(options.seed.ToString() + ":" + stableKey),
                    SourceSeatIndex = identity.sourceSeatIndex,
                    StableKey = stableKey,
                    PlayerSeed = GCPlayerSeed.NormalizeOrFallback(players[capturedOrder].playerSeed, null, capturedOrder),
                    Type = identity.playerType != GCPlayerType.unset ? identity.playerType : ResolvePlayerType(players[capturedOrder].type),
                    ColorName = identity.playerColor.ToString(),
                });
            }

            var entries = participants
                .OrderBy(participant => participant.Hash)
                .ThenBy(participant => participant.CapturedOrder)
                .Select((participant, playerIndex) => new GCActivePlayerMappingEntry(
                    playerIndex,
                    participant.CapturedOrder,
                    participant.SourceSeatIndex,
                    participant.StableKey,
                    participant.PlayerSeed,
                    participant.Hash,
                    participant.Type,
                    ResolvePlayerColor(participant.ColorName)
                ))
                .ToArray();

            return new GCActivePlayerMapping(BuildMappingId(options.seed, entries), options.seed, entries);
        }

        private static GCActivePlayerMapping CreateFromMappedActivePlayers(GCPlayOptions options, GCSeatIdentity[] identities)
        {
            var players = options.players ?? Array.Empty<GCActivePlayerOptions>();
            var entries = new GCActivePlayerMappingEntry[players.Length];
            var seenPlayerIndices = new bool[players.Length];

            for (var capturedOrder = 0; capturedOrder < players.Length; capturedOrder++)
            {
                var player = players[capturedOrder];
                var playerIndex = player.playerIndex;
                if (playerIndex < 0 || playerIndex >= players.Length)
                {
                    throw new ArgumentException("[GamingCouch] activePlayers must use dense zero-based playerIndex values.", nameof(options));
                }

                if (seenPlayerIndices[playerIndex])
                {
                    throw new ArgumentException("[GamingCouch] activePlayers must not contain duplicate playerIndex values.", nameof(options));
                }

                seenPlayerIndices[playerIndex] = true;

                var identity = identities[capturedOrder];
                var stableKey = !string.IsNullOrWhiteSpace(identity.stableKey)
                    ? identity.stableKey
                    : playerIndex.ToString();

                entries[playerIndex] = new GCActivePlayerMappingEntry(
                    playerIndex,
                    capturedOrder,
                    identity.sourceSeatIndex,
                    stableKey,
                    GCPlayerSeed.NormalizeOrFallback(player.playerSeed, null, playerIndex),
                    ComputeFnv1A32(options.seed.ToString() + ":" + stableKey),
                    ResolvePlayerType(player.type),
                    ResolvePlayerColor(player.color)
                );
            }

            return new GCActivePlayerMapping(BuildMappingId(options.seed, entries), options.seed, entries);
        }

        internal static uint ComputeFnv1A32(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                for (var index = 0; index < bytes.Length; index++)
                {
                    hash ^= bytes[index];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        internal GCPlayOptions CreateGameFacingPlayOptions()
        {
            var players = new GCActivePlayerOptions[entriesByIndex.Length];
            for (var index = 0; index < entriesByIndex.Length; index++)
            {
                var entry = entriesByIndex[index];
                players[index] = new GCActivePlayerOptions
                {
                    playerIndex = entry.PlayerIndex,
                    playerSeed = entry.PlayerSeed,
                    type = entry.PlayerType.ToString(),
                    color = entry.PlayerColor.ToString(),
                };
            }

            return new GCPlayOptions
            {
                players = players,
                seed = Seed,
            };
        }

        internal bool IsValidPlayerIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < entriesByIndex.Length;
        }

        internal bool TryGetPlayerIndexForSourceSeat(int sourceSeatIndex, out int playerIndex)
        {
            return playerIndexBySourceSeatIndex.TryGetValue(sourceSeatIndex, out playerIndex);
        }

        internal GCActivePlayerMappingEntry GetByPlayerIndex(int playerIndex)
        {
            if (!IsValidPlayerIndex(playerIndex))
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            return entriesByIndex[playerIndex];
        }

        internal bool TryValidatePlayerIndex(int playerIndex, string source, out GCActivePlayerMappingEntry entry)
        {
            if (IsValidPlayerIndex(playerIndex))
            {
                entry = entriesByIndex[playerIndex];
                return true;
            }

            entry = default;
            EmitInvalidPlayerIndex(playerIndex, source);
            return false;
        }

        internal bool TryValidatePlacement(int[] playerIndicesByPlacement, string source)
        {
            if (playerIndicesByPlacement == null || playerIndicesByPlacement.Length != entriesByIndex.Length)
            {
                EmitInvalidPlayerIndex(-1, source + ":length");
                return false;
            }

            var seen = new bool[entriesByIndex.Length];
            for (var index = 0; index < playerIndicesByPlacement.Length; index++)
            {
                var playerIndex = playerIndicesByPlacement[index];
                if (!IsValidPlayerIndex(playerIndex))
                {
                    EmitInvalidPlayerIndex(playerIndex, source + "[" + index + "]");
                    return false;
                }

                if (seen[playerIndex])
                {
                    EmitInvalidPlayerIndex(playerIndex, source + "[" + index + "]:duplicate");
                    return false;
                }

                seen[playerIndex] = true;
            }

            return true;
        }

        internal GCDiagnosticMappingContext CreateDiagnosticContext(string offendingReference)
        {
            var context = new GCDiagnosticMappingContext()
                .WithMappingId(MappingId)
                .WithSeed(Seed)
                .WithParticipantCount(ParticipantCount);

            if (!string.IsNullOrWhiteSpace(offendingReference))
            {
                context.WithOffendingReference(offendingReference);
            }

            return context;
        }

        private void EmitInvalidPlayerIndex(int playerIndex, string source)
        {
            var context = new GCDiagnosticContext()
                .WithMapping(CreateDiagnosticContext(source + ":playerIndex:" + playerIndex));

            if (playerIndex >= 0)
            {
                context.WithPlayerIndex(playerIndex);
            }

            GCDiagnostics.Emit(
                GCDiagnosticCodes.InvalidPlayerIndex,
                GCDiagnosticSeverity.Warning,
                GCDiagnosticSourceAreas.Mapping,
                "Player index is outside the active mapping.",
                context
            );
        }

        private static string BuildMappingId(int seed, GCActivePlayerMappingEntry[] entries)
        {
            var builder = new StringBuilder();
            builder.Append(seed);
            builder.Append(":");
            for (var index = 0; index < entries.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(entries[index].Hash);
            }

            return "map-" + ComputeFnv1A32(builder.ToString()).ToString("x8");
        }

        private static GCPlayerType ResolvePlayerType(string value)
        {
            return string.Equals(value, GCPlayerType.bot.ToString(), StringComparison.OrdinalIgnoreCase) ? GCPlayerType.bot : GCPlayerType.player;
        }

        private static GCPlayerColor ResolvePlayerColor(string value)
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out GCPlayerColor playerColor) ? playerColor : GCPlayerColor.blue;
        }

        private struct Participant
        {
            internal int CapturedOrder;
            internal uint Hash;
            internal int SourceSeatIndex;
            internal string StableKey;
            internal int PlayerSeed;
            internal GCPlayerType Type;
            internal string ColorName;
        }
    }

    internal readonly struct GCActivePlayerMappingEntry
    {
        internal readonly int PlayerIndex;
        internal readonly int CapturedOrder;
        internal readonly int SourceSeatIndex;
        internal readonly string StableKey;
        internal readonly int PlayerSeed;
        internal readonly uint Hash;
        internal readonly GCPlayerType PlayerType;
        internal readonly GCPlayerColor PlayerColor;

        internal GCActivePlayerMappingEntry(
            int playerIndex,
            int capturedOrder,
            int sourceSeatIndex,
            string stableKey,
            int playerSeed,
            uint hash,
            GCPlayerType playerType,
            GCPlayerColor playerColor
        )
        {
            PlayerIndex = playerIndex;
            CapturedOrder = capturedOrder;
            SourceSeatIndex = sourceSeatIndex;
            StableKey = stableKey;
            PlayerSeed = playerSeed;
            Hash = hash;
            PlayerType = playerType;
            PlayerColor = playerColor;
        }
    }
}
