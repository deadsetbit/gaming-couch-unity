using System;
using System.Collections.Generic;
using System.Linq;
using DSB.GC.Hud;
using UnityEngine;

namespace DSB.GC.Game
{
    public enum GCGamePlacementOrder
    {
        Eliminated,
        EliminatedScore,
        EliminatedScoreFinished,
        EliminatedFinished,
    }

    public class GCGameHudOptions
    {
        public bool isPlayersAutoUpdateEnabled = true;
        public GCHudPlayersConfig players;
    }

    public class GCGameSetupOptions
    {
        public GCGamePlacementOrder placementOrder;
        public GCGameHudOptions hud;
    }

    public class GCGame
    {
        private GamingCouch gamingCouch;
        private GCPlayerStoreOutput<GCPlayer> playerStore;
        private GCGameSetupOptions options;
        private bool isPlayersHudAutoUpdateEnabled;
        private bool isPlayersHudAutoUpdatePending = false;

        public GCGame(GamingCouch gamingCouch, GCPlayerStoreOutput<GCPlayer> playerStore, GCGameSetupOptions options)
        {
            Debug.Log("Setting up game");

            this.gamingCouch = gamingCouch;
            this.playerStore = playerStore;
            this.options = options;

            if (options.hud != null)
            {
                Debug.Log("Setting up game HUD");
                this.isPlayersHudAutoUpdateEnabled = options.hud.isPlayersAutoUpdateEnabled;
                this.gamingCouch.Hud.Setup(
                    new GCHudConfig
                    {
                        players = options.hud.players
                    }
                );
                AutoUpdatePlayersHud();

                foreach (var player in playerStore.GetPlayersEnumerable())
                {
                    player.OnEliminated += () =>
                    {
                        isPlayersHudAutoUpdatePending = true;
                    };

                    player.OnUneliminated += () =>
                    {
                        isPlayersHudAutoUpdatePending = true;
                    };

                    player.OnFinished += () =>
                    {
                        isPlayersHudAutoUpdatePending = true;
                    };

                    player.OnScoreChanged += (int playerId, int score) =>
                    {
                        isPlayersHudAutoUpdatePending = true;
                    };

                    player.OnLivesChanged += (int playerId, int lives) =>
                    {
                        isPlayersHudAutoUpdatePending = true;
                    };
                }
            }
        }

        public void HandlePlayersHudAutoUpdate()
        {
            if (isPlayersHudAutoUpdateEnabled && isPlayersHudAutoUpdatePending)
            {
                AutoUpdatePlayersHud();
                isPlayersHudAutoUpdatePending = false;
            }
        }

        public IEnumerable<GCPlayer> GetPlayersInPlacementOrder(IEnumerable<GCPlayer> players)
        {
            switch (options.placementOrder)
            {
                case GCGamePlacementOrder.Eliminated:
                    return players.OrderByDescending(x => x.GetLastEliminatedTime() > 0.0f ? x.GetLastEliminatedTime() : Time.time)
                        .ToList();
                case GCGamePlacementOrder.EliminatedScore:
                    return players.OrderByDescending(x => x.GetLastEliminatedTime() > 0.0f ? x.GetLastEliminatedTime() : Time.time)
                        .ThenByDescending(x => x.GetScore())
                        .ToList();
                case GCGamePlacementOrder.EliminatedScoreFinished:
                    return players.OrderByDescending(x => x.GetLastEliminatedTime() > 0.0f ? x.GetLastEliminatedTime() : Time.time)
                        .ThenByDescending(x => x.GetScore())
                        .ThenBy(x => x.GetFinishedTime() > 0.0f ? x.GetFinishedTime() : Time.time)
                        .ToList();
                case GCGamePlacementOrder.EliminatedFinished:
                    return players.OrderByDescending(x => x.GetLastEliminatedTime() > 0.0f ? x.GetLastEliminatedTime() : Time.time)
                        .ThenBy(x => x.GetFinishedTime() > 0.0f ? x.GetFinishedTime() : Time.time)
                        .ToList();
                default:
                    return players;
            }
        }

        public bool IsPlayersAutoUpdateEnabled()
        {
            return isPlayersHudAutoUpdateEnabled;
        }

        private string GetPlayerHudValue(GCPlayer player)
        {
            var valueType = options.hud.players.valueType;

            if (valueType == null)
            {
                return null;
            }

            switch (valueType)
            {
                case "pointsSmall": // TODO: these needs to be enums etc.
                    return player.GetScore().ToString();
                case "text":
                    return player.GetHudValueText();
                case "lives":
                    return player.GetLives().ToString();
                default:
                    throw new Exception($"Invalid player hud value type '{valueType}'");
            }
        }

        public void AutoUpdatePlayersHud()
        {
            var playersByPlacement = GetPlayersInPlacementOrder(playerStore.GetPlayersEnumerable());

            gamingCouch.Hud.UpdatePlayers(new GCPlayersHudData
            {
                players = playersByPlacement.Select((player, index) => new GCPlayersHudDataPlayer
                {
                    playerId = player.GetId(),
                    eliminated = player.IsEliminated(),
                    placement = index,
                    value = GetPlayerHudValue(player)
                }).ToArray()
            });
        }
    }
}
