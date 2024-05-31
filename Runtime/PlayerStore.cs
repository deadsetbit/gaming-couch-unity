using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    public class PlayerStore<T> : IPlayerStoreOutput<T>, IPlayerStoreInput<T> where T : IGamingCouchPlayer
    {
        private List<T> players = new List<T>();
        private Dictionary<int, T> playerById = new Dictionary<int, T>();

        public PlayerStore() { }

        public T GetPlayerById(int playerId)
        {
            return playerById[playerId];
        }

        public IEnumerable<T> GetPlayersEnumerable()
        {
            return players;
        }

        public List<T> GetPlayers()
        {
            return players;
        }

        public int GetPlayerCount()
        {
            return players.Count;
        }

        public T GetPlayerByIndex(int index)
        {
            return players[index];
        }

        public void AddPlayer(T player)
        {
            players.Add(player);
            playerById[player.GetId()] = player;
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
