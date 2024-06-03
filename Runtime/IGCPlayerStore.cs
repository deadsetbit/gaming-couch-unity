using System.Collections.Generic;

public interface IGCPlayerStoreOutput<out T> where T : IGCPlayer
{
    T GetPlayerById(int playerId);
    IEnumerable<T> GetPlayersEnumerable();
    int GetPlayerCount();
    T GetPlayerByIndex(int index);
    void Clear();
}

public interface IGCPlayerStoreInput<in T> where T : IGCPlayer
{
    void AddPlayer(T player);
}
