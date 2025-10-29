using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using DSB.GC.Hud;
using DSB.GC.Log;

namespace DSB.GC.Game
{
    public enum GCPlacementSortCriteria
    {
        Eliminated,
        EliminatedDescending,
        Score,
        ScoreDescending,
        Finished,
        FinishedDescending,
    }

    public class GCGameHudOptions
    {
        public bool isPlayersAutoUpdateEnabled = true;
        public GCHudPlayersConfig players;
    }

    public class GCGameSetupOptions
    {
        public int maxScore = -1;
        public GCPlacementSortCriteria[] placementCriteria = new GCPlacementSortCriteria[] {
            GCPlacementSortCriteria.EliminatedDescending,
            GCPlacementSortCriteria.ScoreDescending,
            GCPlacementSortCriteria.Finished
        };
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
            GCLog.LogInfo("GCGame constructor");

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
                UpdatePlayersHud();
            }
        }

        public void SetupPlayer(GCPlayer player)
        {
            Debug.Log("SetupPlayer - playerId:" + player.name + " playerStore count:" + playerStore.PlayersEnumerable.Count());

            var self = this;

            if (isPlayersHudAutoUpdateEnabled)
            {
                isPlayersHudAutoUpdatePending = true;
            }

            player.OnEliminated += reason =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnUneliminated += reason =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnFinished += reason =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnScoreChanged += (playerId, score, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnLivesChanged += (playerId, lives, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnStatusChanged += (status, statusText, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnMeterValueChanged += (playerId, meterValue, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };
        }

        public void SetMaxScore(int maxScore)
        {
            options.maxScore = maxScore;
            ValidateOptions(options);

            if (isPlayersHudAutoUpdateEnabled)
            {
                isPlayersHudAutoUpdatePending = true;
            }
        }

        private void ValidateOptions(GCGameSetupOptions options)
        {
            if (options.hud != null)
            {
                if (options.hud.players.valueTypeEnum == PlayersHudValueType.PointsSmall)
                {
                    if (options.maxScore <= 0)
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
                UpdatePlayersHud();
                isPlayersHudAutoUpdatePending = false;
            }
        }

        private Func<GCPlayer, IComparable> GetPlacementCriteriaKeySelector(GCPlacementSortCriteria criteria)
        {
            switch (criteria)
            {
                case GCPlacementSortCriteria.Eliminated:
                case GCPlacementSortCriteria.EliminatedDescending:
                    return p => p.IsEliminated ? p.LastSetEliminatedTime : float.MaxValue;
                case GCPlacementSortCriteria.Score:
                case GCPlacementSortCriteria.ScoreDescending:
                    return p => p.Score;
                case GCPlacementSortCriteria.Finished:
                case GCPlacementSortCriteria.FinishedDescending:
                    return p => p.IsFinished ? p.FinishedTime : float.MaxValue;
                default:
                    throw new Exception($"Unhandled placement sort criteria '{criteria}'");
            }
        }

        public IEnumerable<GCPlayer> GetPlayersInPlacementOrder(IEnumerable<GCPlayer> players)
        {
            if (options.placementCriteria.Length == 0)
            {
                throw new Exception("GCGameSetupOptions.placementCriteria not defined");
            }

            IOrderedEnumerable<GCPlayer> sortedPlayers = null;

            foreach (var criteria in options.placementCriteria)
            {
                var keySelector = GetPlacementCriteriaKeySelector(criteria);
                switch (criteria)
                {
                    case GCPlacementSortCriteria.Eliminated:
                    case GCPlacementSortCriteria.Score:
                    case GCPlacementSortCriteria.Finished:
                        sortedPlayers = sortedPlayers == null ? players.OrderBy(keySelector) : sortedPlayers.ThenBy(keySelector);
                        break;
                    case GCPlacementSortCriteria.EliminatedDescending:
                    case GCPlacementSortCriteria.ScoreDescending:
                    case GCPlacementSortCriteria.FinishedDescending:
                        sortedPlayers = sortedPlayers == null ? players.OrderByDescending(keySelector) : sortedPlayers.ThenByDescending(keySelector);
                        break;
                    default:
                        throw new Exception($"Unhandled placement sort criteria '{criteria}'");
                }
            }

            return sortedPlayers;
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
                case PlayersHudValueType.Status:
                    return player.GetHudStatusText();
                case PlayersHudValueType.Text:
                    return player.GetHudValueText();
                case PlayersHudValueType.Lives:
                    return player.Lives.ToString();
                default:
                    throw new Exception($"Unhandled player hud value type '{valueType}'");
            }
        }

        private void UpdatePlayersHud()
        {
            Debug.Log("UpdatePlayersHud - player count:" + playerStore.PlayersEnumerable.Count());

            var playersByPlacement = GetPlayersInPlacementOrder(playerStore.PlayersEnumerable);

            gamingCouch.Hud.UpdatePlayers(new GCPlayersHudData
            {
                players = playersByPlacement.Select((player, index) => new GCPlayersHudDataPlayer
                {
                    playerId = player.Id,
                    eliminated = player.IsEliminated,
                    placement = index,
                    value = GetPlayerHudValue(player),
                    meterValue = player.MeterValue
                }).ToArray()
            });
        }
    }
}
