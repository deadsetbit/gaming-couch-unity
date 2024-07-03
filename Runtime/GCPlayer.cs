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
        public Action OnFinished;
        public Action<int, int> OnScoreChanged;
        public Action<int, int> OnLivesChanged;

        private int playerId = -1;
        private string playerName;
        private Color color;
        private bool isEliminated = false;
        private float lastSetEliminatedTime = -1; // can be used for placement order
        private float lastSetUneliminatedTime = -1; // can be used for respawn cooldown etc.
        private int score = 0;
        private int lives = 0;
        private float finishedTime = -1;

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
        /// Mark the player as eliminated. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// </summary>
        public void SetEliminated()
        {
            isEliminated = true;
            lastSetEliminatedTime = Time.time;
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
        /// Set the player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void SetScore(int newScore)
        {
            if (this.score == newScore) return;

            var oldScore = this.score;
            score = newScore;

            OnScoreChanged?.Invoke(oldScore, newScore);
        }

        /// <summary>
        /// Add to player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void AddScore(int score)
        {
            SetScore(this.score + score);
        }

        /// <summary>
        /// Subtract from player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void SubtractScore(int score)
        {
            SetScore(this.score - score);
        }

        /// <summary>
        /// Get the player's score.
        /// </summary>
        public float GetScore()
        {
            return score;
        }

        /// <summary>
        /// Set the player as finished. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// </summary>
        public void SetFinished()
        {
            finishedTime = Time.time;

            OnFinished?.Invoke();
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

        /// <summary>
        /// Set the player's lives.
        /// </summary>
        public void SetLives(int newLives)
        {
            if (newLives < 0)
            {
                newLives = 0;
                Debug.LogWarning("Player lives cannot be less than 0. Setting to 0.");
            }

            if (this.lives == newLives) return;

            var oldLives = this.lives;
            lives = newLives;

            OnLivesChanged?.Invoke(oldLives, newLives);
        }

        /// <summary>
        /// Add to player's lives.
        /// </summary>
        /// <param name="lives"></param>
        public void AddLives(int lives)
        {
            SetLives(this.lives + lives);
        }

        /// <summary>
        /// Subtract from player's lives.
        /// </summary>
        public void SubtractLives(int lives)
        {
            SetLives(this.lives - lives);
        }

        /// <summary>
        /// Set the player's lives. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public int GetLives()
        {
            return lives;
        }

        /// <summary>
        /// Get the player's HUD value to be displayed in the HUD.
        /// </summary>
        virtual public string GetHudValueText()
        {
            throw new Exception("GetHudValueText not implemented. Implement this in your GCPlayer subclass to display a custom value in the HUD.");
        }
    }
}
