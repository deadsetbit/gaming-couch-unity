using System;
using System.Collections;
using System.Collections.Generic;
using DSB.GC.Log;
using DSB.GC.RuntimeMessages;
using UnityEngine;

namespace DSB.GC
{
    public enum GCPlayerStatus
    {
        Neutral = 0,
        Pending = 1,
        Success = 2,
        Failure = 3,
        Warning = 4,
        Alert = 5
    }

    public enum GCPlayerEliminationState
    {
        None = 0,
        Revokable = 1,
        Permanent = 2
    }

    public enum GCPlayerFinishState
    {
        None = 0,
        Revokable = 1,
        Permanent = 2
    }

    public struct GCPlayerEliminationStateChangedEventArgs
    {
        public int playerIndex;
        public GCPlayerEliminationState oldState;
        public GCPlayerEliminationState newState;
        public string reason;
        public float changedAtGameTime;

        public GCPlayerEliminationStateChangedEventArgs(
            int playerIndex,
            GCPlayerEliminationState oldState,
            GCPlayerEliminationState newState,
            string reason,
            float changedAtGameTime
        )
        {
            this.playerIndex = playerIndex;
            this.oldState = oldState;
            this.newState = newState;
            this.reason = reason;
            this.changedAtGameTime = changedAtGameTime;
        }
    }

    public struct GCPlayerFinishStateChangedEventArgs
    {
        public int playerIndex;
        public GCPlayerFinishState oldState;
        public GCPlayerFinishState newState;
        public string reason;
        public float changedAtGameTime;

        public GCPlayerFinishStateChangedEventArgs(
            int playerIndex,
            GCPlayerFinishState oldState,
            GCPlayerFinishState newState,
            string reason,
            float changedAtGameTime
        )
        {
            this.playerIndex = playerIndex;
            this.oldState = oldState;
            this.newState = newState;
            this.reason = reason;
            this.changedAtGameTime = changedAtGameTime;
        }
    }

    public class GCPlayer : MonoBehaviour
    {
        public Action<GCPlayerEliminationStateChangedEventArgs> OnEliminationStateChanged;
        public Action<GCPlayerFinishStateChangedEventArgs> OnFinishStateChanged;
        public Action<int, int, string> OnScoreChanged;
        public Action<int, int, string> OnLivesChanged;
        public Action<int, int, string> OnMeterChanged;
        public Action<GCPlayerStatus, string, string> OnStatusChanged;
        public GCPlayerType PlayerType = GCPlayerType.unset;
        public bool IsBot => PlayerType == GCPlayerType.bot;
        private int index = -1;
        public int Index => index;
        private int id = -1;
        /// <summary>
        /// Removed. Use Index for game-facing player identity.
        /// </summary>
        [Obsolete("GCPlayer.Id has been removed from the game-facing runtime contract. Use GCPlayer.Index.", true)]
        public int Id => id;
        private string playerName;
        /// <summary>
        /// Removed. Player names are platform-owned and are not exposed to Unity game code.
        /// </summary>
        [Obsolete("GCPlayer.PlayerName has been removed from the game-facing runtime contract. Player names are platform-owned.", true)]
        public string PlayerName => playerName;
        /// <summary>
        /// GamingCouch player color "base" variant.
        /// </summary>
        public Color ColorBase => GCPlayerColorData.Variants[colorEnum].BaseColor;
        /// <summary>
        /// GamingCouch player color "dark" variant.
        /// </summary>
        public Color ColorDark => GCPlayerColorData.Variants[colorEnum].Dark;
        /// <summary>
        /// GamingCouch player color "light" variant.
        /// </summary>
        public Color ColorLight => GCPlayerColorData.Variants[colorEnum].Light;
        /// <summary>
        /// GamingCouch player color "off-white" variant.
        /// </summary>
        public Color ColorOffWhite => GCPlayerColorData.Variants[colorEnum].OffWhite;
        private GCPlayerStatus status = GCPlayerStatus.Neutral;
        /// <summary>
        /// GamingCouch player color name in enum format.
        /// </summary>
        private GCPlayerColor colorEnum;
        public GCPlayerColor ColorEnum => colorEnum;
        /// <summary>
        /// GamingCouch player color name in string format.
        /// </summary>
        private string colorName;
        public string ColorName => colorName;
        /// <summary>
        /// GamingCouch player color in hex format.
        /// </summary>
        private string colorHex;
        public string ColorHex => colorHex;
        /// <summary>
        /// Player status.
        /// This can be utilized in different ways to indicate the player's status in the game.
        /// If Players HUD is set to display status text, this will be reflected there as well.
        /// </summary>
        public GCPlayerStatus Status => status;
        private string statusText = "";
        /// <summary>
        /// Players status text.
        /// This can be utilized in different ways to indicate the player's status in the game.
        /// If Players HUD is set to display status text, this will be reflected there as well.
        /// </summary>
        public string StatusText => statusText;
        private GCPlayerEliminationState eliminationState = GCPlayerEliminationState.None;
        public GCPlayerEliminationState EliminationState => eliminationState;
        private GCPlayerFinishState finishState = GCPlayerFinishState.None;
        public GCPlayerFinishState FinishState => finishState;
        /// <summary>
        /// Get the player's broad eliminated status.
        /// </summary>
        public bool IsEliminated => eliminationState != GCPlayerEliminationState.None;
        public bool IsEliminatedPermanent => eliminationState == GCPlayerEliminationState.Permanent;
        public bool IsEliminatedRevokable => eliminationState == GCPlayerEliminationState.Revokable;
        public bool IsFinished => finishState != GCPlayerFinishState.None;
        public bool IsFinishedPermanent => finishState == GCPlayerFinishState.Permanent;
        public bool IsFinishedRevokable => finishState == GCPlayerFinishState.Revokable;
        private float lastSetEliminatedPermanentGameTime = -1;
        public float LastSetEliminatedPermanentGameTime => lastSetEliminatedPermanentGameTime;
        private float lastSetEliminatedRevokableGameTime = -1;
        public float LastSetEliminatedRevokableGameTime => lastSetEliminatedRevokableGameTime;
        private float lastSetRevokeEliminatedGameTime = -1;
        public float LastSetRevokeEliminatedGameTime => lastSetRevokeEliminatedGameTime;
        private float lastSetFinishedPermanentGameTime = -1;
        public float LastSetFinishedPermanentGameTime => lastSetFinishedPermanentGameTime;
        private float lastSetFinishedRevokableGameTime = -1;
        public float LastSetFinishedRevokableGameTime => lastSetFinishedRevokableGameTime;
        private float lastSetRevokeFinishedGameTime = -1;
        public float LastSetRevokeFinishedGameTime => lastSetRevokeFinishedGameTime;
        private float lastSetEliminatedGameTime = -1;
        public float LastSetEliminatedGameTime => lastSetEliminatedGameTime;
        private float lastSetFinishedGameTime = -1;
        public float LastSetFinishedGameTime => lastSetFinishedGameTime;
        private float lastSetRevokeGameTime = -1;
        public float LastSetRevokeGameTime => lastSetRevokeGameTime;
        /// <summary>
        /// Removed. Use LastSetEliminatedGameTime or the explicit permanent/revokable timestamp properties.
        /// </summary>
        [Obsolete("Use LastSetEliminatedGameTime, LastSetEliminatedPermanentGameTime, or LastSetEliminatedRevokableGameTime.", false)]
        public float LastSetEliminatedTime => lastSetEliminatedGameTime;
        /// <summary>
        /// Removed. Use LastSetRevokeEliminatedGameTime.
        /// </summary>
        [Obsolete("Use LastSetRevokeEliminatedGameTime.", false)]
        public float LastSetUneliminatedTime => lastSetRevokeEliminatedGameTime;
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
        /// <summary>
        /// Removed. Use LastSetFinishedGameTime or the explicit permanent/revokable timestamp properties.
        /// </summary>
        [Obsolete("Use LastSetFinishedGameTime, LastSetFinishedPermanentGameTime, or LastSetFinishedRevokableGameTime.", false)]
        public float FinishedTime => lastSetFinishedGameTime;
        private int meter = -1;
        /// <summary>
        /// Get the player's meter value (-1-100).
        /// </summary>
        public int Meter => meter;

        /// <summary>
        /// This is called by the GamingCouch script.
        /// You can access all the properties set by this method, such as Index, Color, etc. in your subclasses Start().
        /// </summary>
        /// <param name="options">Options provided by the platform</param>
        internal void _InternalGamingCouchSetup(GCPlayerSetupOptions options)
        {
            index = options.playerIndex;
            PlayerType = options.type;
            colorEnum = options.colorEnum;
            colorName = options.colorName;
        }

        [Obsolete("SetEliminated has been removed from the game-facing runtime contract. Choose SetEliminatedPermanent(reason) or SetEliminatedRevokable(reason).", true)]
        public void SetEliminated(string reason)
        {
            throw new InvalidOperationException("SetEliminated has been removed. Choose SetEliminatedPermanent or SetEliminatedRevokable.");
        }

        [Obsolete("SetUneliminated has been removed from the game-facing runtime contract. Use SetRevokeEliminated(reason) for revokable elimination.", true)]
        public void SetUneliminated(string reason)
        {
            throw new InvalidOperationException("SetUneliminated has been removed. Use SetRevokeEliminated.");
        }

        public void SetEliminatedPermanent(string reason)
        {
            if (!TryAllowMutation("SetEliminatedPermanent")) return;

            if (eliminationState == GCPlayerEliminationState.Permanent)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.DuplicateElimination,
                    "Player is already permanently eliminated.",
                    "SetEliminatedPermanent",
                    eliminationState.ToString(),
                    GCPlayerEliminationState.Permanent.ToString()
                );
                return;
            }

            var oldState = eliminationState;
            var changedAtGameTime = Time.time;
            eliminationState = GCPlayerEliminationState.Permanent;
            lastSetEliminatedPermanentGameTime = changedAtGameTime;
            lastSetEliminatedGameTime = changedAtGameTime;
            GCLog.LogInfo($"Player index {index} permanently eliminated - reason: " + reason);
            OnEliminationStateChanged?.Invoke(new GCPlayerEliminationStateChangedEventArgs(
                index,
                oldState,
                eliminationState,
                reason,
                changedAtGameTime
            ));
        }

        public void SetEliminatedRevokable(string reason)
        {
            if (!TryAllowMutation("SetEliminatedRevokable")) return;

            if (eliminationState == GCPlayerEliminationState.Revokable)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.DuplicateElimination,
                    "Player is already revokably eliminated.",
                    "SetEliminatedRevokable",
                    eliminationState.ToString(),
                    GCPlayerEliminationState.Revokable.ToString()
                );
                return;
            }

            if (eliminationState == GCPlayerEliminationState.Permanent)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.InvalidTransition,
                    "Permanent elimination cannot transition back to revokable elimination.",
                    "SetEliminatedRevokable",
                    eliminationState.ToString(),
                    GCPlayerEliminationState.Revokable.ToString()
                );
                return;
            }

            var oldState = eliminationState;
            var changedAtGameTime = Time.time;
            eliminationState = GCPlayerEliminationState.Revokable;
            lastSetEliminatedRevokableGameTime = changedAtGameTime;
            lastSetEliminatedGameTime = changedAtGameTime;
            GCLog.LogInfo($"Player index {index} revokably eliminated - reason: " + reason);
            OnEliminationStateChanged?.Invoke(new GCPlayerEliminationStateChangedEventArgs(
                index,
                oldState,
                eliminationState,
                reason,
                changedAtGameTime
            ));
        }

        public void SetRevokeEliminated(string reason)
        {
            if (!TryAllowMutation("SetRevokeEliminated")) return;

            if (eliminationState != GCPlayerEliminationState.Revokable)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.InvalidRevoke,
                    "Only revokable elimination can be revoked.",
                    "SetRevokeEliminated",
                    eliminationState.ToString(),
                    GCPlayerEliminationState.None.ToString()
                );
                return;
            }

            var oldState = eliminationState;
            var changedAtGameTime = Time.time;
            eliminationState = GCPlayerEliminationState.None;
            lastSetRevokeEliminatedGameTime = changedAtGameTime;
            lastSetRevokeGameTime = changedAtGameTime;
            GCLog.LogInfo($"Player index {index} elimination revoked - reason: " + reason);
            OnEliminationStateChanged?.Invoke(new GCPlayerEliminationStateChangedEventArgs(
                index,
                oldState,
                eliminationState,
                reason,
                changedAtGameTime
            ));
        }

        /// <summary>
        /// Set the player's score. Depending on the GCGamePlacementOrder used, this can be used to determine the player's placement.
        /// If hudAutoUpdate is true, the changes will be reflected in the HUD.
        /// </summary>
        public void SetScore(int newScore, string reason)
        {
            if (!TryAllowMutation("SetScore")) return;

            if (this.score == newScore) return;

            GCLog.LogInfo($"Player index {index} score set to {newScore} - reason: " + reason);

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

        [Obsolete("SetFinished has been removed from the game-facing runtime contract. Choose SetFinishedPermanent(reason) or SetFinishedRevokable(reason).", true)]
        public void SetFinished(string reason)
        {
            throw new InvalidOperationException("SetFinished has been removed. Choose SetFinishedPermanent or SetFinishedRevokable.");
        }

        public void SetFinishedPermanent(string reason)
        {
            if (!TryAllowMutation("SetFinishedPermanent")) return;

            if (finishState == GCPlayerFinishState.Permanent)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.DuplicateFinish,
                    "Player is already permanently finished.",
                    "SetFinishedPermanent",
                    finishState.ToString(),
                    GCPlayerFinishState.Permanent.ToString()
                );
                return;
            }

            var oldState = finishState;
            var changedAtGameTime = Time.time;
            finishState = GCPlayerFinishState.Permanent;
            lastSetFinishedPermanentGameTime = changedAtGameTime;
            lastSetFinishedGameTime = changedAtGameTime;
            GCLog.LogInfo($"Player index {index} permanently finished - reason: " + reason);
            OnFinishStateChanged?.Invoke(new GCPlayerFinishStateChangedEventArgs(
                index,
                oldState,
                finishState,
                reason,
                changedAtGameTime
            ));
        }

        public void SetFinishedRevokable(string reason)
        {
            if (!TryAllowMutation("SetFinishedRevokable")) return;

            if (finishState == GCPlayerFinishState.Revokable)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.DuplicateFinish,
                    "Player is already revokably finished.",
                    "SetFinishedRevokable",
                    finishState.ToString(),
                    GCPlayerFinishState.Revokable.ToString()
                );
                return;
            }

            if (finishState == GCPlayerFinishState.Permanent)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.InvalidTransition,
                    "Permanent finish cannot transition back to revokable finish.",
                    "SetFinishedRevokable",
                    finishState.ToString(),
                    GCPlayerFinishState.Revokable.ToString()
                );
                return;
            }

            var oldState = finishState;
            var changedAtGameTime = Time.time;
            finishState = GCPlayerFinishState.Revokable;
            lastSetFinishedRevokableGameTime = changedAtGameTime;
            lastSetFinishedGameTime = changedAtGameTime;
            GCLog.LogInfo($"Player index {index} revokably finished - reason: " + reason);
            OnFinishStateChanged?.Invoke(new GCPlayerFinishStateChangedEventArgs(
                index,
                oldState,
                finishState,
                reason,
                changedAtGameTime
            ));
        }

        public void SetRevokeFinished(string reason)
        {
            if (!TryAllowMutation("SetRevokeFinished")) return;

            if (finishState != GCPlayerFinishState.Revokable)
            {
                EmitStateDiagnostic(
                    GCDiagnosticCodes.InvalidRevoke,
                    "Only revokable finish can be revoked.",
                    "SetRevokeFinished",
                    finishState.ToString(),
                    GCPlayerFinishState.None.ToString()
                );
                return;
            }

            var oldState = finishState;
            var changedAtGameTime = Time.time;
            finishState = GCPlayerFinishState.None;
            lastSetRevokeFinishedGameTime = changedAtGameTime;
            lastSetRevokeGameTime = changedAtGameTime;
            GCLog.LogInfo($"Player index {index} finish revoked - reason: " + reason);
            OnFinishStateChanged?.Invoke(new GCPlayerFinishStateChangedEventArgs(
                index,
                oldState,
                finishState,
                reason,
                changedAtGameTime
            ));
        }

        /// <summary>
        /// Set the player's lives.
        /// </summary>
        public void SetLives(int newLives, string reason)
        {
            if (!TryAllowMutation("SetLives")) return;

            if (newLives < 0)
            {
                newLives = 0;
                EmitStateDiagnostic(
                    GCDiagnosticCodes.ClampedValue,
                    "Lives were clamped.",
                    "SetLives",
                    null,
                    null
                );
            }

            if (this.lives == newLives) return;

            GCLog.LogInfo($"Player index {index} lives set to {newLives} - reason: " + reason);

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
        public void SetStatus(GCPlayerStatus status, string statusText, string reason)
        {
            if (!TryAllowMutation("SetStatus")) return;

            if (this.status == status && this.statusText == statusText) return;

            GCLog.LogInfo($"Player index {index} status set to {status} with text {statusText} - reason: " + reason);

            this.status = status;
            this.statusText = statusText;

            OnStatusChanged?.Invoke(status, statusText, reason);
        }

        /// <summary>
        /// Set the player's meter value ranging from -1 to 100. Depending on the HUD configuration, this will be displayed in the HUD.
        /// Set value to -1 to hide the meter from the HUD (if meter is configured to be displayed in the HUD). 
        /// </summary>
        public void SetMeter(int newMeter, string reason)
        {
            if (!TryAllowMutation("SetMeter")) return;

            if (newMeter < -1)
            {
                newMeter = -1;
                EmitStateDiagnostic(
                    GCDiagnosticCodes.ClampedValue,
                    "Meter was clamped.",
                    "SetMeter",
                    null,
                    null
                );
            }
            else if (newMeter > 100)
            {
                newMeter = 100;
                EmitStateDiagnostic(
                    GCDiagnosticCodes.ClampedValue,
                    "Meter was clamped.",
                    "SetMeter",
                    null,
                    null
                );
            }

            if (this.meter == newMeter) return;

            var oldMeter = this.meter;
            meter = newMeter;

            OnMeterChanged?.Invoke(oldMeter, newMeter, reason);
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

        private bool TryAllowMutation(string mutatorName)
        {
            if (GamingCouch.Instance == null || GamingCouch.Instance.Status != GCStatus.GameOver)
            {
                return true;
            }

            EmitStateDiagnostic(
                GCDiagnosticCodes.PostGameOverMutation,
                "Player mutation after game over was ignored.",
                mutatorName,
                null,
                null
            );
            return false;
        }

        private void EmitStateDiagnostic(
            string code,
            string message,
            string mutatorName,
            string oldState,
            string requestedState
        )
        {
            var context = new GCDiagnosticContext()
                .AddDetail("mutator", mutatorName);

            if (index >= 0)
            {
                context.WithPlayerIndex(index);
            }

            if (oldState != null)
            {
                context.AddDetail("oldState", oldState);
            }

            if (requestedState != null)
            {
                context.AddDetail("requestedState", requestedState);
            }

            GCDiagnostics.Emit(
                code,
                GCDiagnosticSeverity.Warning,
                GCDiagnosticSourceAreas.State,
                message,
                context
            );
        }
    }
}
