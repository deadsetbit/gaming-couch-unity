using System;
using DSB.GC.Dev;

namespace DSB.GC
{
    internal sealed class GCActiveRunProjection
    {
        internal GCActivePlayerMapping ActivePlayerMapping { get; }
        internal GCPlayOptions GameFacingPlayOptions { get; }
        internal GCSeatIdentity[] MappedSeatIdentities { get; }
        internal GCPlatformRuntimeView PlatformData => GameFacingPlayOptions.platformData;

        private GCActiveRunProjection(
            GCActivePlayerMapping activePlayerMapping,
            GCPlayOptions gameFacingPlayOptions,
            GCSeatIdentity[] mappedSeatIdentities
        )
        {
            ActivePlayerMapping = activePlayerMapping;
            GameFacingPlayOptions = gameFacingPlayOptions;
            MappedSeatIdentities = mappedSeatIdentities ?? Array.Empty<GCSeatIdentity>();
        }

        internal static GCActiveRunProjection Create(GCPlayOptions options)
        {
            return Create(options, null);
        }

        internal static GCActiveRunProjection Create(GCPlayOptions options, GCSeatIdentity[] seatIdentities)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var playerCount = options.players?.Length ?? 0;
            var resolvedSeatIdentities = seatIdentities ?? CreateFallbackSeatIdentities(options);
            if (resolvedSeatIdentities.Length != playerCount)
            {
                throw new ArgumentException("[GamingCouch] Seat identity count must match play player count.");
            }

            var activePlayerMapping = GCActivePlayerMapping.Create(options, resolvedSeatIdentities);
            var gameFacingPlayOptions = activePlayerMapping.CreateGameFacingPlayOptions();
            gameFacingPlayOptions.runtimeOutput = options.runtimeOutput ?? new GCRuntimeOutputOptions();
            gameFacingPlayOptions.platformData = GCPlatformRuntimeView.CopyForRuntime(options.platformData);
#if UNITY_EDITOR
            gameFacingPlayOptions.runtimeOutput = GCDevAppRuntimeOutputSettings.Apply(gameFacingPlayOptions.runtimeOutput);
#endif

            return new GCActiveRunProjection(
                activePlayerMapping,
                gameFacingPlayOptions,
                CreateMappedSeatIdentities(resolvedSeatIdentities, activePlayerMapping)
            );
        }

        private static GCSeatIdentity[] CreateFallbackSeatIdentities(GCPlayOptions options)
        {
            if (options?.players == null)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var seatIdentities = new GCSeatIdentity[options.players.Length];
            for (var index = 0; index < options.players.Length; index++)
            {
                var playerOption = options.players[index];
                var sourceSeatIndex = index + 1;
                var participantIdentity = options.participantIdentities != null && index < options.participantIdentities.Length
                    ? options.participantIdentities[index]
                    : default;
                seatIdentities[index] = new GCSeatIdentity
                {
                    platformPlayerId = participantIdentity.platformPlayerId,
                    sourceSeatIndex = sourceSeatIndex,
                    stableKey = !string.IsNullOrWhiteSpace(participantIdentity.stableKey) ? participantIdentity.stableKey : sourceSeatIndex.ToString(),
                    label = "Seat " + sourceSeatIndex,
                    playerType = ResolvePlayerType(playerOption.type),
                    playerColor = ResolvePlayerColor(playerOption.color),
                };
            }

            return seatIdentities;
        }

        private static GCSeatIdentity[] CreateMappedSeatIdentities(
            GCSeatIdentity[] capturedSeatIdentities,
            GCActivePlayerMapping mapping
        )
        {
            if (mapping == null || capturedSeatIdentities == null || capturedSeatIdentities.Length == 0)
            {
                return Array.Empty<GCSeatIdentity>();
            }

            var mappedSeatIdentities = new GCSeatIdentity[capturedSeatIdentities.Length];
            for (var playerIndex = 0; playerIndex < capturedSeatIdentities.Length; playerIndex++)
            {
                var entry = mapping.GetByPlayerIndex(playerIndex);
                mappedSeatIdentities[playerIndex] = capturedSeatIdentities[entry.CapturedOrder];
            }

            return mappedSeatIdentities;
        }

        private static GCPlayerType ResolvePlayerType(string value)
        {
            return string.Equals(value, GCPlayerType.bot.ToString(), StringComparison.OrdinalIgnoreCase) ? GCPlayerType.bot : GCPlayerType.player;
        }

        private static GCPlayerColor ResolvePlayerColor(string value)
        {
            return !string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out GCPlayerColor playerColor) ? playerColor : GCPlayerColor.blue;
        }
    }
}
