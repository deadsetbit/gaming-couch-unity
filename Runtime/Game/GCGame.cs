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
        public int maxScore = -1;
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

            ValidateOptions(options);

            this.gamingCouch = gamingCouch;
            this.playerStore = playerStore;
            this.options = options;

            if (options.hud != null)
            {
                this.isPlayersHudAutoUpdateEnabled = options.hud.isPlayersAutoUpdateEnabled;
                this.gamingCouch.Hud.Setup(
                    new GCHudConfig
                    {
                        players = options.hud.players
                    }
                );
                AutoUpdatePlayersHud();

                foreach (var player in playerStore.PlayersEnumerable)
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

        private void ValidateOptions(GCGameSetupOptions options)
        {
            if (options.hud != null)
            {
                if (options.hud.players.valueTypeEnum == PlayersHudValueType.PointsSmall)
                {
                    if (options.maxScore < 0)
                    {
                        throw new Exception("Game options maxScore must be defined when using 'PlayersHudValueType.PointsSmall'");
                    }
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
            // TODO: Do the sorting by using components
            switch (options.placementOrder)
            {
                case GCGamePlacementOrder.Eliminated:
                    return players.OrderByDescending(x => x.LastSetEliminatedTime > 0.0f ? x.LastSetEliminatedTime : Time.time)
                        .ToList();
                case GCGamePlacementOrder.EliminatedScore:
                    return players.OrderByDescending(x => x.LastSetEliminatedTime > 0.0f ? x.LastSetEliminatedTime : Time.time)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                case GCGamePlacementOrder.EliminatedScoreFinished:
                    return players.OrderByDescending(x => x.LastSetEliminatedTime > 0.0f ? x.LastSetEliminatedTime : Time.time)
                        .ThenByDescending(x => x.Score)
                        .ThenBy(x => x.GetFinishedTime() > 0.0f ? x.GetFinishedTime() : Time.time)
                        .ToList();
                case GCGamePlacementOrder.EliminatedFinished:
                    return players.OrderByDescending(x => x.LastSetEliminatedTime > 0.0f ? x.LastSetEliminatedTime : Time.time)
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
            var valueType = options.hud.players.valueTypeEnum;

            if (valueType == PlayersHudValueType.None)
            {
                return null;
            }

            switch (valueType)
            {
                case PlayersHudValueType.PointsSmall:
                    return player.Score.ToString() + "/" + options.maxScore;
                case PlayersHudValueType.Text:
                    return player.GetHudValueText();
                case PlayersHudValueType.Lives:
                    return player.Lives.ToString();
                default:
                    throw new Exception($"Unhandled player hud value type '{valueType}'");
            }
        }

        public void AutoUpdatePlayersHud()
        {
            var playersByPlacement = GetPlayersInPlacementOrder(playerStore.PlayersEnumerable);

            gamingCouch.Hud.UpdatePlayers(new GCPlayersHudData
            {
                players = playersByPlacement.Select((player, index) => new GCPlayersHudDataPlayer
                {
                    playerId = player.Id,
                    eliminated = player.IsEliminated,
                    placement = index,
                    value = GetPlayerHudValue(player)
                }).ToArray()
            });
        }
    }
}
