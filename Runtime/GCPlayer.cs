using System;
using System.Collections;
using System.Collections.Generic;
using DSB.GC.Log;
using UnityEngine;

namespace DSB.GC
{
    public enum PlayerStatus
    {
        Neutral,
        Pending,
        Success,
        Failure
    }

    public class GCPlayer : MonoBehaviour
    {
        public Action OnEliminated;
        public Action OnUneliminated;
        public Action OnFinished;
        public Action<int, int> OnScoreChanged;
        public Action<int, int> OnLivesChanged;
        public Action<PlayerStatus, string> OnStatusChanged;
        private int id = -1;
        /// <summary>
        /// Get the player id provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public int Id => id;
        private string playerName;
        /// <summary>
        /// Get the player name provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public string PlayerName => playerName;
        private Color color;
        private PlayerStatus status = PlayerStatus.Neutral;
        /// <summary>
        /// Player status.
        /// This can be utilized in different ways to indicate the player's status in the game.
        /// If Players HUD is set to display status text, this will be reflected there as well.
        /// </summary>
        public PlayerStatus Status => status;
        private string statusText = "";
        /// <summary>
        /// Players status text.
        /// This can be utilized in different ways to indicate the player's status in the game.
        /// If Players HUD is set to display status text, this will be reflected there as well.
        /// </summary>
        public string StatusText => statusText;
        /// <summary>
        /// Get the player color provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public Color Color => color;
        private bool isEliminated = false;
        public bool IsEliminated => isEliminated;
        private float lastSetEliminatedTime = -1;
        /// <summary>
        /// Get the time the player was last eliminated.
        /// </summary>
        public float LastSetEliminatedTime => lastSetEliminatedTime;
        private float lastSetUneliminatedTime = -1;
        /// <summary>
        /// Get the time the player was last set uneliminated.
        /// </summary>
        public float LastSetUneliminatedTime => lastSetUneliminatedTime;
        private int score = 0;
        /// <summary>
        /// Get the player's score.
        /// </summary>
        public int Score => score;
        private int lives = 0;
        /// <summary>
        /// Set the player's lives. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public int Lives => lives;
        private float finishedTime = -1;
        /// <summary>
        /// The time player was set as finished. eg. when they reach the finish line.
        /// </summary>
        public float FinishedTime => finishedTime;
        public bool IsFinished => finishedTime != -1;

        /// <summary>
        /// Do not call this in your game script. This is called by the GamingCouch script.
        /// You can access all the properties set by this method, such as Id, PlayerName, Color, etc. in your subclasses Start().
        /// </summary>
        /// <param name="options">Options provided by the platform</param>
        public void GamingCouchSetup(GCPlayerSetupOptions options)
        {
            id = options.playerId;
            playerName = options.name;
            color = options.color;
        }

        /// <summary>
        /// Mark the player as eliminated. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// </summary>
        public void SetEliminated()
        {
            GCLog.LogInfo($"Player {id} eliminated.");

            isEliminated = true;
            lastSetEliminatedTime = Time.time;
            OnEliminated?.Invoke();
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
        /// Set the player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void SetScore(int newScore)
        {
            GCLog.LogInfo($"Player {id} score set to {newScore}.");

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
        /// Set the player as finished. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// </summary>
        public void SetFinished()
        {
            GCLog.LogInfo($"Player {id} finished.");

            finishedTime = Time.time;

            OnFinished?.Invoke();
        }

        /// <summary>
        /// Set the player's lives.
        /// </summary>
        public void SetLives(int newLives)
        {
            GCLog.LogInfo($"Player {id} lives set to {newLives}.");

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
        /// Set the player's status and status text.
        /// This status is displayed in the Players HUD if the HUD is configured to display the status.
        /// </summary>
        public void SetStatus(PlayerStatus status, string statusText)
        {
            if (this.status == status && this.statusText == statusText) return;

            GCLog.LogInfo($"Player {id} status set to {status} with text {statusText}.");

            this.status = status;
            this.statusText = statusText;

            OnStatusChanged?.Invoke(status, statusText);
        }

        /// <summary>
        /// Get the player's HUD status text to be displayed in the HUD.
        /// This will include the status text and status.
        /// </summary>
        public string GetHudStatusText()
        {
            var str = status.ToString();
            return statusText + "/" + char.ToLower(str[0]) + str[1..]; // set first letter to lowercase
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