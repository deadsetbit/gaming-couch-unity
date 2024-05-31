using System.Collections.Generic;

public interface IPlayerStoreOutput<out T> where T : IGamingCouchPlayer
{
    T GetPlayerById(int playerId);
    IEnumerable<T> GetPlayersEnumerable();
    int GetPlayerCount();
    T GetPlayerByIndex(int index);
    void Clear();
}

public interface IPlayerStoreInput<in T> where T : IGamingCouchPlayer
{
    void AddPlayer(T player);
}
