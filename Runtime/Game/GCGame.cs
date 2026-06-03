using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using DSB.GC.Hud;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;

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
            GCLog.LogDebug("SetupPlayer - playerIndex:" + player.Index + " playerStore count:" + playerStore.Players.Count);

            var self = this;

            if (isPlayersHudAutoUpdateEnabled)
            {
                isPlayersHudAutoUpdatePending = true;
            }

            player.OnEliminationStateChanged += args =>
            {
                self.isPlayersHudAutoUpdatePending = true;
                self.QueueRuntimeTransition(
                    GCRuntimeMessageTypes.PlayerEliminationStateChanged,
                    GCRuntimeTransitionPayload.BuildStringJson(
                        args.playerIndex,
                        args.oldState.ToString(),
                        args.newState.ToString(),
                        args.reason
                    )
                );
            };

            player.OnFinishStateChanged += args =>
            {
                self.isPlayersHudAutoUpdatePending = true;
                self.QueueRuntimeTransition(
                    GCRuntimeMessageTypes.PlayerFinishStateChanged,
                    GCRuntimeTransitionPayload.BuildStringJson(
                        args.playerIndex,
                        args.oldState.ToString(),
                        args.newState.ToString(),
                        args.reason
                    )
                );
            };

            player.OnScoreChanged += (oldScore, newScore, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
                self.QueueRuntimeTransition(
                    GCRuntimeMessageTypes.PlayerScoreChanged,
                    GCRuntimeTransitionPayload.BuildIntJson(player.Index, oldScore, newScore, reason)
                );
            };

            player.OnLivesChanged += (oldLives, newLives, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
                self.QueueRuntimeTransition(
                    GCRuntimeMessageTypes.PlayerLivesChanged,
                    GCRuntimeTransitionPayload.BuildIntJson(player.Index, oldLives, newLives, reason)
                );
            };

            player.OnStatusChanged += (status, statusText, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
            };

            player.OnStatusTransitionChanged += (oldStatus, oldStatusText, status, statusText, reason) =>
            {
                self.QueueRuntimeTransition(
                    GCRuntimeMessageTypes.PlayerStatusChanged,
                    GCRuntimeTransitionPayload.BuildStatusJson(
                        player.Index,
                        oldStatus,
                        oldStatusText,
                        status,
                        statusText,
                        reason
                    )
                );
            };

            player.OnMeterChanged += (oldMeter, newMeter, reason) =>
            {
                self.isPlayersHudAutoUpdatePending = true;
                self.QueueRuntimeTransition(
                    GCRuntimeMessageTypes.PlayerMeterChanged,
                    GCRuntimeTransitionPayload.BuildIntJson(player.Index, oldMeter, newMeter, reason)
                );
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

            gamingCouch?.QueueRuntimeStateSnapshot();
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
                    return p => p.IsEliminated ? p.LastSetEliminatedGameTime : float.MaxValue;
                case GCPlacementSortCriteria.Score:
                case GCPlacementSortCriteria.ScoreDescending:
                    return p => p.Score;
                case GCPlacementSortCriteria.Finished:
                case GCPlacementSortCriteria.FinishedDescending:
                    return p => p.IsFinished ? p.LastSetFinishedGameTime : float.MaxValue;
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

        internal GCRuntimeStateSnapshotPayload BuildRuntimeStateSnapshotPayload(GCStatus gameStatus)
        {
            var playersByPlacement = GetPlayersInPlacementOrder(playerStore.Players);
            return GCRuntimeStateSnapshotBuilder.BuildPayload(gameStatus, playerStore.Players, playersByPlacement);
        }

        private void QueueRuntimeTransition(string messageType, string payloadJson)
        {
            gamingCouch?.QueueRuntimePlayerTransition(messageType, payloadJson);
            gamingCouch?.QueueRuntimeStateSnapshot();
        }

        private string GetPlayerHudValue(GCPlayer player)
        {
            if (options.hud == null)
            {
                return null;
            }

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
            GCLog.LogDebug("UpdatePlayersHud - player count:" + playerStore.Players.Count);

            gamingCouch.Hud.UpdatePlayers(BuildPlayersHudData());
        }

        internal GCPlayersHudData BuildPlayersHudData()
        {
            var snapshot = BuildRuntimeStateSnapshotPayload(gamingCouch?.Status ?? GCStatus.Playing);
            var playersByIndex = playerStore.Players.ToDictionary(player => player.Index);

            return new GCPlayersHudData
            {
                players = snapshot.players.Select(playerState =>
                {
                    var player = playersByIndex[playerState.playerIndex];

                    return new GCPlayersHudDataPlayer
                    {
                        playerIndex = playerState.playerIndex,
                        score = playerState.score,
                        lives = playerState.lives,
                        status = playerState.status,
                        statusText = playerState.statusText,
                        eliminationState = playerState.eliminationState,
                        finishState = playerState.finishState,
                        eliminated = playerState.eliminationState != GCPlayerEliminationState.None.ToString(),
                        placement = playerState.placement,
                        value = GetPlayerHudValue(player),
                        meter = playerState.meter,
                    };
                }).ToArray()
            };
        }
    }
}
