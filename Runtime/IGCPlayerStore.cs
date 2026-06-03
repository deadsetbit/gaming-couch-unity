using System.Collections.Generic;
using DSB.GC;

public interface GCPlayerStoreOutput<T> where T : GCPlayer
{
    List<T> Players { get; }
    List<T> PlayersBot { get; }
    List<T> PlayersNonBot { get; }
    List<T> PlayersUneliminated { get; }
    List<T> PlayersUneliminatedBot { get; }
    List<T> PlayersUneliminatedNonBot { get; }
    List<T> PlayersEliminated { get; }
    List<T> PlayersEliminatedBot { get; }
    List<T> PlayersEliminatedNonBot { get; }
    List<T> PlayersEliminatedPermanent { get; }
    List<T> PlayersEliminatedPermanentBot { get; }
    List<T> PlayersEliminatedPermanentNonBot { get; }
    List<T> PlayersEliminatedRevokable { get; }
    List<T> PlayersEliminatedRevokableBot { get; }
    List<T> PlayersEliminatedRevokableNonBot { get; }
    List<T> PlayersFinished { get; }
    List<T> PlayersFinishedBot { get; }
    List<T> PlayersFinishedNonBot { get; }
    List<T> PlayersFinishedPermanent { get; }
    List<T> PlayersFinishedPermanentBot { get; }
    List<T> PlayersFinishedPermanentNonBot { get; }
    List<T> PlayersFinishedRevokable { get; }
    List<T> PlayersFinishedRevokableBot { get; }
    List<T> PlayersFinishedRevokableNonBot { get; }
    [System.Obsolete("Use Players.Count.", true)]
    int PlayerCount { get; }
    [System.Obsolete("Use Players.", true)]
    IEnumerable<T> PlayersEnumerable { get; }
    [System.Obsolete("Use PlayersUneliminated; broad uneliminated means eliminationState is None.", true)]
    IEnumerable<T> UneliminatedPlayersEnumerable { get; }
    [System.Obsolete("Use PlayersUneliminated.Count.", true)]
    int UneliminatedPlayerCount { get; }
    [System.Obsolete("Use PlayersEliminated; broad eliminated includes permanent and revokable elimination.", true)]
    IEnumerable<T> EliminatedPlayersEnumerable { get; }
    [System.Obsolete("Use PlayersEliminated.Count.", true)]
    int EliminatedPlayerCount { get; }
    [System.Obsolete("GetPlayerById has been removed from the game-facing runtime contract. Use GetPlayerByIndex.", true)]
    T GetPlayerById(int playerId);
    T GetPlayerByIndex(int playerIndex);
    void Clear();
}

public interface GCPlayerStoreInput<in T> where T : GCPlayer
{
    void AddPlayer(T player);
}
