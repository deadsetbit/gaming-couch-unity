using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    public class GCPlayer : MonoBehaviour
    {
        private int playerId = -1;
        private string playerName;
        private Color color;
        private bool isEliminated = false;
        private float lastEliminationTime = -1;
        private float lastUneliminatedTime = -1;
        private float score = 0;
        private float finishedTime = -1;
        public float FinishedTime => finishedTime;

        /// <summary>
        /// Do not call this in your game script. This is called by the GamingCouch script.
        /// If you want to do something custom with the options on player setup, override this method.
        /// NOTE: Don't forget to call base.GamingCouchSetup(options) when overriding.
        /// </summary>
        /// <param name="options">Options provided by the platform </param>
        public virtual void GamingCouchSetup(GCPlayerSetupOptions options)
        {
            playerId = options.playerId;
            playerName = options.name;
            color = options.color;
        }

        /// <summary>
        /// Get the player id provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public virtual int GetId()
        {
            if (playerId == -1)
            {
                throw new System.Exception("Player id is not set. Wait for GamingCouchSetup to be called first. If you are overriding GamingCouchSetup, make sure to call base.GamingCouchSetup(options) in your override.");
            }

            return playerId;
        }

        /// <summary>
        /// Get the player name provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public virtual string GetName()
        {
            return playerName;
        }

        /// <summary>
        /// Get the player color provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public virtual Color GetColor()
        {
            return color;
        }

        /// <summary>
        /// Mark the player as eliminated. Depending on the GC placement pattern used, this can be used to determine the player's placement.
        /// </summary>
        public void SetEliminated()
        {
            isEliminated = true;
            lastEliminationTime = Time.time;
        }

        /// <summary>
        /// Get the time the player was last eliminated. Note: This will not reset when player is set uneliminated.
        /// </summary>
        public float GetLastEliminationTime()
        {
            return lastEliminationTime;
        }

        /// <summary>
        /// Clear the player's eliminated status. Note: This will not reset the last elimination time.
        /// </summary>
        public void SetUneliminated()
        {
            isEliminated = false;
            lastUneliminatedTime = Time.time;
        }

        /// <summary>
        /// Get the time the player was last set uneliminated. Note: This will not reset when player is set eliminated.
        /// </summary>
        public float GetUneliminatedTime()
        {
            return lastUneliminatedTime;
        }

        /// <summary>
        /// Get the player's eliminated status.
        /// </summary>
        public bool GetIsEliminated()
        {
            return isEliminated;
        }

        /// <summary>
        /// Set the player's score. Depending on the GC placement pattern used, this can be used to determine the player's placement.
        /// </summary>
        public void SetScore(float score)
        {
            this.score = score;
        }

        /// <summary>
        /// Get the player's score.
        /// </summary>
        public float GetScore()
        {
            return score;
        }

        /// <summary>
        /// Set the player as finished. Depending on the GC placement pattern used, this can be used to determine the player's placement.
        /// </summary>
        public void SetIsFinished()
        {
            finishedTime = Time.time;
        }

        /// <summary>
        /// Get the player's finished status.
        /// </summary>
        public bool GetIsFinished()
        {
            return finishedTime != -1;
        }

        /// <summary>
        /// Get the time the player finished. Note: This will not reset when the player finished status is cleared.
        /// </summary>
        public float GetFinishedTime()
        {
            return finishedTime;
        }
    }
}
