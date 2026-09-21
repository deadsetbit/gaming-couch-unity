#if UNITY_EDITOR
namespace DSB.GC.Dev
{
    /// <summary>
    /// The seat number DevApp shows for a game-facing player index, and its inverse.
    ///
    /// A run's game-facing player indices are a seed-dependent permutation of its seats, so an
    /// index says nothing about which seat a developer is looking at. DevApp's run configuration
    /// numbers seats from the list the runtime emits, and that list is built from this same rule,
    /// so anything in Unity that has to agree with what the developer reads goes through here.
    /// </summary>
    internal static class GCSeatNumbering
    {
        /// <summary>
        /// The seat number for a player index. A run that carries no local seat identity, which is
        /// how a hosted-shaped payload arrives, is numbered by position instead.
        /// </summary>
        internal static int GetSeatNumber(GCSeatIdentity[] mappedSeatIdentities, int playerIndex)
        {
            var sourceSeatIndex = mappedSeatIdentities[playerIndex].sourceSeatIndex;
            return sourceSeatIndex > 0 ? sourceSeatIndex : playerIndex + 1;
        }

        /// <summary>
        /// The player index behind a seat number, or false when the run has no such seat.
        /// </summary>
        internal static bool TryGetPlayerIndex(
            GCSeatIdentity[] mappedSeatIdentities,
            int seatNumber,
            out int playerIndex
        )
        {
            if (mappedSeatIdentities != null)
            {
                for (var index = 0; index < mappedSeatIdentities.Length; index++)
                {
                    if (GetSeatNumber(mappedSeatIdentities, index) == seatNumber)
                    {
                        playerIndex = index;
                        return true;
                    }
                }
            }

            playerIndex = -1;
            return false;
        }
    }
}
#endif
