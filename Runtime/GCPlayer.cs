using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    public class GCPlayer : MonoBehaviour
    {
        public Action OnEliminated;
        public Action OnUneliminated;

        private int playerId = -1;
        private string playerName;
        private Color color;
        private bool isEliminated = false;
        private float lastSetEliminatedTime = -1;
        private float lastSetUneliminatedTime = -1;
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
            lastSetEliminatedTime = Time.time;
            Debug.Log("Player " + playerName + " eliminated");
            OnEliminated?.Invoke();
        }

        /// <summary>
        /// Get the time the player was last eliminated.
        /// </summary>
        public float GetLastEliminatedTime()
        {
            return lastSetEliminatedTime;
        }

        /// <summary>
        /// Clear the player's eliminated status.
        /// </summary>
        public void SetUneliminated()
        {
            isEliminated = false;
            lastSetUneliminatedTime = Time.time;
            OnUneliminated?.Invoke();
        }

        /// <summary>
        /// Get the time the player was last set uneliminated.
        /// </summary>
        public float GetUneliminatedTime()
        {
            return lastSetUneliminatedTime;
        }

        /// <summary>
        /// Get the player's eliminated status.
        /// </summary>
        public bool IsEliminated()
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
        public void SetFinished()
        {
            finishedTime = Time.time;
        }

        /// <summary>
        /// Get the player's finished status.
        /// </summary>
        public bool IsFinished()
        {
            return finishedTime != -1;
        }

        /// <summary>
        /// Get the time the player finished.
        /// </summary>
        public float GetFinishedTime()
        {
            return finishedTime;
        }
    }
}
