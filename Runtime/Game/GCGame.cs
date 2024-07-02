using System;
using System.Collections.Generic;
using System.Linq;
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

    public class GCGameSetupOptions
    {
        public GCGamePlacementOrder placementOrder;
    }

    public class GCGame
    {
        private GCGameSetupOptions options;

        public GCGame(GCGameSetupOptions options)
        {
            this.options = options;
        }

        public List<GCPlayer> GetPlayersInPlacementOrder(List<GCPlayer> players)
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
    }
}
