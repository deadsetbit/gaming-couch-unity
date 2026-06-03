using System;
using System.Collections.Generic;
using DSB.GC.Utils;
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
        public int UneliminatedPlayerCount => uneliminatedPlayers.Count;
        public IEnumerable<T> UneliminatedPlayersEnumerable => uneliminatedPlayers;
        private List<T> uneliminatedNonBotPlayers = new List<T>();
        public List<T> UneliminatedNonBotPlayers => uneliminatedNonBotPlayers;
        public IEnumerable<T> UneliminatedNonBotPlayersEnumerable => uneliminatedNonBotPlayers;
        private List<T> uneliminatedBotPlayers = new List<T>();
        public List<T> UneliminatedBotPlayers => uneliminatedBotPlayers;
        public IEnumerable<T> UneliminatedBotPlayersEnumerable => uneliminatedBotPlayers;
        private List<T> eliminatedPlayers = new List<T>();
        public List<T> EliminatedPlayers => eliminatedPlayers;
        public int EliminatedPlayerCount => eliminatedPlayers.Count;
        public IEnumerable<T> EliminatedPlayersEnumerable => eliminatedPlayers;
        private List<T> eliminatedNonBotPlayers = new List<T>();
        public List<T> EliminatedNonBotPlayers => eliminatedNonBotPlayers;
        public IEnumerable<T> EliminatedNonBotPlayersEnumerable => eliminatedNonBotPlayers;
        private List<T> eliminatedBotPlayers = new List<T>();
        public List<T> EliminatedBotPlayers => eliminatedBotPlayers;
        public IEnumerable<T> EliminatedBotPlayersEnumerable => eliminatedBotPlayers;
        private Dictionary<int, T> playerByIndex = new Dictionary<int, T>();

        public GCPlayerStore() { }

        private void HandlePlayerEliminated(T player)
        {
            if (!uneliminatedPlayers.Contains(player))
            {
                Debug.LogWarning($"Player index {player.Index} is not in the uneliminated players list when trying to eliminate them.");
                return;
            }

            uneliminatedPlayers.Remove(player);
            eliminatedPlayers.Add(player);

            if (player.IsBot)
            {
                if (uneliminatedBotPlayers.Contains(player))
                {
                    uneliminatedBotPlayers.Remove(player);
                }
                eliminatedBotPlayers.Add(player);
            }
            else
            {
                if (uneliminatedNonBotPlayers.Contains(player))
                {
                    uneliminatedNonBotPlayers.Remove(player);
                }
                eliminatedNonBotPlayers.Add(player);
            }

            Debug.Assert(uneliminatedPlayers.Count + eliminatedPlayers.Count == players.Count, "Player store out of sync");
        }

        private void HandlePlayerUneliminated(T player)
        {
            if (!eliminatedPlayers.Contains(player))
            {
                Debug.LogWarning($"Player index {player.Index} is not in the eliminated players list when trying to revoke elimination.");
                return;
            }

            eliminatedPlayers.Remove(player);
            uneliminatedPlayers.Add(player);

            if (player.IsBot)
            {
                if (eliminatedBotPlayers.Contains(player))
                {
                    eliminatedBotPlayers.Remove(player);
                }
                uneliminatedBotPlayers.Add(player);
            }
            else
            {
                if (eliminatedNonBotPlayers.Contains(player))
                {
                    eliminatedNonBotPlayers.Remove(player);
                }
                uneliminatedNonBotPlayers.Add(player);
            }

            Debug.Assert(uneliminatedPlayers.Count + eliminatedPlayers.Count == players.Count, "Player store out of sync");
        }

        [Obsolete("GetPlayerById has been removed from the game-facing runtime contract. Use GetPlayerByIndex.", true)]
        public T GetPlayerById(int playerId)
        {
            throw new InvalidOperationException("GetPlayerById has been removed. Use GetPlayerByIndex.");
        }

        public T GetPlayerByIndex(int playerIndex)
        {
            if (playerByIndex.TryGetValue(playerIndex, out var player))
            {
                return player;
            }

            return players[playerIndex];
        }

        public void AddPlayer(T player)
        {
            Assert.IsNotNull(player, "Trying to add null player to store. This could be due to invalid player type casting?");
            Assert.IsTrue(player.Index != -1, "Player not properly initialized before adding to store");

            players.Add(player);

            if (!player.IsEliminated)
            {
                uneliminatedPlayers.Add(player);
                if (player.IsBot)
                {
                    uneliminatedBotPlayers.Add(player);
                }
                else
                {
                    uneliminatedNonBotPlayers.Add(player);
                }
            }
            else
            {
                eliminatedPlayers.Add(player);
                if (player.IsBot)
                {
                    eliminatedBotPlayers.Add(player);
                }
                else
                {
                    eliminatedNonBotPlayers.Add(player);
                }
            }

            playerByIndex[player.Index] = player;

            player.OnEliminationStateChanged += args =>
            {
                if (args.oldState == GCPlayerEliminationState.None && args.newState != GCPlayerEliminationState.None)
                {
                    HandlePlayerEliminated(player);
                }
                else if (args.oldState != GCPlayerEliminationState.None && args.newState == GCPlayerEliminationState.None)
                {
                    HandlePlayerUneliminated(player);
                }
            };
        }

        public void Clear()
        {
            foreach (var player in players)
            {
                UnityEngine.Object.Destroy(player.gameObject);
            }

            players.Clear();
            uneliminatedPlayers.Clear();
            uneliminatedNonBotPlayers.Clear();
            uneliminatedBotPlayers.Clear();
            eliminatedPlayers.Clear();
            eliminatedNonBotPlayers.Clear();
            eliminatedBotPlayers.Clear();
            playerByIndex.Clear();
        }
    }
}
