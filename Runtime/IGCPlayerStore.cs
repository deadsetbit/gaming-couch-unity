using System.Collections.Generic;
using DSB.GC;

public interface GCPlayerStoreOutput<out T> where T : GCPlayer
{
    T GetPlayerById(int playerId);
    IEnumerable<T> GetPlayersEnumerable();
    IEnumerable<T> GetUneliminatedPlayersEnumerable();
    IEnumerable<T> GetEliminatedPlayersEnumerable();
    int GetPlayerCount();
    T GetPlayerByIndex(int index);
    void Clear();
}

public interface GCPlayerStoreInput<in T> where T : GCPlayer
{
    void AddPlayer(T player);
}
