using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    public class GCPlayerStore<T> : GCPlayerStoreOutput<T>, GCPlayerStoreInput<T> where T : GCPlayer
    {
        private List<T> players = new List<T>();
        public List<T> Players => players;
        public IEnumerable<T> PlayersEnumerable => players;
        public int PlayerCount => players.Count;
        private List<T> uneliminatedPlayers = new List<T>();
        public List<T> UneliminatedPlayers => uneliminatedPlayers;
        public IEnumerable<T> UneliminatedPlayersEnumerable => uneliminatedPlayers;
        private List<T> eliminatedPlayers = new List<T>();
        public List<T> EliminatedPlayers => eliminatedPlayers;
        public IEnumerable<T> EliminatedPlayersEnumerable => eliminatedPlayers;
        private Dictionary<int, T> playerById = new Dictionary<int, T>();

        public GCPlayerStore() { }

        private void HandlePlayerEliminated(T player)
        {
            uneliminatedPlayers.Remove(player);
            eliminatedPlayers.Add(player);

            Debug.Assert(uneliminatedPlayers.Count + eliminatedPlayers.Count == players.Count);
        }

        private void HandlePlayerUneliminated(T player)
        {
            eliminatedPlayers.Remove(player);
            uneliminatedPlayers.Add(player);

            Debug.Assert(uneliminatedPlayers.Count + eliminatedPlayers.Count == players.Count);
        }

        public T GetPlayerById(int playerId)
        {
            return playerById[playerId];
        }

        public List<T> GetUneliminatedPlayers()
        {
            return uneliminatedPlayers;
        }

        public int GetUneliminatedPlayerCount()
        {
            return GetUneliminatedPlayers().Count;
        }

        public IEnumerable<T> GetEliminatedPlayersEnumerable()
        {
            return eliminatedPlayers;
        }

        public List<T> GetEliminatedPlayers()
        {
            return eliminatedPlayers;
        }

        public int GetEliminatedPlayerCount()
        {
            return GetEliminatedPlayers().Count;
        }

        public T GetPlayerByIndex(int index)
        {
            return players[index];
        }

        public void AddPlayer(T player)
        {
            players.Add(player);

            if (!player.IsEliminated)
            {
                uneliminatedPlayers.Add(player);
            }
            else
            {
                eliminatedPlayers.Add(player);
            }

            playerById[player.Id] = player;

            player.OnEliminated += () => HandlePlayerEliminated(player);
            player.OnUneliminated += () => HandlePlayerUneliminated(player);
        }

        public void Clear()
        {
            foreach (var player in players)
            {
                UnityEngine.Object.Destroy(player.gameObject);
            }

            players.Clear();
            playerById.Clear();
        }
    }
}
