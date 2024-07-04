using System.Collections.Generic;
using DSB.GC;

public interface GCPlayerStoreOutput<out T> where T : GCPlayer
{
    int PlayerCount { get; }
    IEnumerable<T> PlayersEnumerable { get; }
    IEnumerable<T> UneliminatedPlayersEnumerable { get; }
    int UneliminatedPlayerCount { get; }
    IEnumerable<T> EliminatedPlayersEnumerable { get; }
    int EliminatedPlayerCount { get; }
    T GetPlayerById(int playerId);
    T GetPlayerByIndex(int index);
    void Clear();
}

public interface GCPlayerStoreInput<in T> where T : GCPlayer
{
    void AddPlayer(T player);
}
