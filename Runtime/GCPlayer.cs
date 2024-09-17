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
        public Action<string> OnEliminated;
        public Action<string> OnUneliminated;
        public Action<string> OnFinished;
        public Action<int, int, string> OnScoreChanged;
        public Action<int, int, string> OnLivesChanged;
        public Action<PlayerStatus, string, string> OnStatusChanged;
        private int id = -1;
        /// <summary>
        /// GamingCouch player id. Note that this can't be used as an index, as the number can be anything starting from 1.
        /// </summary>
        public int Id => id;
        private string playerName;
        /// <summary>
        /// GamingCouch player name.
        /// </summary>
        public string PlayerName => playerName;
        private Color color;
        /// <summary>
        /// GamingCouch player color.
        /// </summary>
        public Color Color => color;
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
        private bool isEliminated = false;
        /// <summary>
        /// Get the player's eliminated status. Use LastSetEliminatedTime/LastSetUneliminatedTime to get the time the player was last eliminated/uneliminated.
        /// </summary>
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
        /// <summary>
        /// Get the player's finished status. Use FinishedTime to get the time the player was set as finished.
        /// </summary>
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
        public void SetEliminated(string reason)
        {
            GCLog.LogInfo($"Player {id} eliminated - reason: " + reason);

            isEliminated = true;
            lastSetEliminatedTime = Time.time;
            OnEliminated?.Invoke(reason);
        }

        /// <summary>
        /// Clear the player's eliminated status.
        /// </summary>
        public void SetUneliminated(string reason)
        {
            GCLog.LogInfo($"Player {id} uneliminated - reason: " + reason);

            isEliminated = false;
            lastSetUneliminatedTime = Time.time;
            OnUneliminated?.Invoke(reason);
        }

        /// <summary>
        /// Set the player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void SetScore(int newScore, string reason)
        {
            GCLog.LogInfo($"Player {id} score set to {newScore} - reason: " + reason);

            if (this.score == newScore) return;

            var oldScore = this.score;
            score = newScore;

            OnScoreChanged?.Invoke(oldScore, newScore, reason);
        }

        /// <summary>
        /// Add to player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void AddScore(int score, string reason)
        {
            SetScore(this.score + score, reason);
        }

        /// <summary>
        /// Subtract from player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void SubtractScore(int score, string reason)
        {
            SetScore(this.score - score, reason);
        }

        /// <summary>
        /// Set the player as finished. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// </summary>
        public void SetFinished(string reason)
        {
            GCLog.LogInfo($"Player {id} finished - reason: " + reason);

            finishedTime = Time.time;

            OnFinished?.Invoke(reason);
        }

        /// <summary>
        /// Set the player's lives.
        /// </summary>
        public void SetLives(int newLives, string reason)
        {
            GCLog.LogInfo($"Player {id} lives set to {newLives} - reason: " + reason);

            if (newLives < 0)
            {
                newLives = 0;
                Debug.LogWarning("Player lives cannot be less than 0. Setting to 0.");
            }

            if (this.lives == newLives) return;

            var oldLives = this.lives;
            lives = newLives;

            OnLivesChanged?.Invoke(oldLives, newLives, reason);
        }

        /// <summary>
        /// Add to player's lives.
        /// </summary>
        /// <param name="lives"></param>
        public void AddLives(int lives, string reason)
        {
            SetLives(this.lives + lives, reason);
        }

        /// <summary>
        /// Subtract from player's lives.
        /// </summary>
        public void SubtractLives(int lives, string reason)
        {
            SetLives(this.lives - lives, reason);
        }

        /// <summary>
        /// Set the player's status and status text.
        /// This status is displayed in the Players HUD if the HUD is configured to display the status.
        /// Player status HUD color will be set based on the status and text can be anything game specific.
        /// Example:
        /// In parking game, while player is finding a spot:
        /// SetStatus(PlayerStatus.Pending, "Finding a spot");
        /// 
        /// When spot is found:
        /// SetStatus(PlayerStatus.Success, "Parked!");
        /// 
        /// If left without spot:
        /// SetStatus(PlayerStatus.Failure, "Sadge :(");
        /// </summary>
        public void SetStatus(PlayerStatus status, string statusText, string reason)
        {
            if (this.status == status && this.statusText == statusText) return;

            GCLog.LogInfo($"Player {id} status set to {status} with text {statusText} - reason: " + reason);

            this.status = status;
            this.statusText = statusText;

            OnStatusChanged?.Invoke(status, statusText, reason);
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