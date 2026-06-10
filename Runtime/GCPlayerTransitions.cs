using System;
using UnityEngine;

namespace DSB.GC
{
    internal enum GCPlayerTransitionKind
    {
        PlayerStatusChanged = 1,
        PlayerEliminationStateChanged = 2,
        PlayerFinishStateChanged = 3,
    }

    internal enum GCPlayerTransitionRejectionReason
    {
        None = 0,
        DuplicateValue = 1,
        InvalidTransition = 2,
        InvalidRevoke = 3,
    }

    internal enum GCPlayerTransitionOutcome
    {
        Accepted = 1,
        NoOp = 2,
        Rejected = 3,
    }

    [Flags]
    internal enum GCPlayerLatestStateDirtyFlags
    {
        None = 0,
        RuntimeStateSnapshot = 1,
        PlayersHud = 2,
    }

    internal readonly struct GCPlayerStatusValue : IEquatable<GCPlayerStatusValue>
    {
        internal GCPlayerStatusValue(GCPlayerStatus status, string statusText)
        {
            Status = status;
            StatusText = statusText ?? "";
        }

        internal GCPlayerStatus Status { get; }
        internal string StatusText { get; }

        public bool Equals(GCPlayerStatusValue other)
        {
            return Status == other.Status &&
                string.Equals(StatusText, other.StatusText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GCPlayerStatusValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Status * 397) ^ StringComparer.Ordinal.GetHashCode(StatusText);
            }
        }
    }

    internal readonly struct GCPlayerTransitionResult<TValue>
    {
        private GCPlayerTransitionResult(
            GCPlayerTransitionOutcome outcome,
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue previousValue,
            TValue value,
            string reasonText,
            float changedAtGameTime,
            bool emitsSemanticTransition,
            GCPlayerLatestStateDirtyFlags latestStateDirtyFlags,
            GCPlayerTransitionRejectionReason rejectionReason
        )
        {
            Outcome = outcome;
            Kind = kind;
            PlayerIndex = playerIndex;
            PreviousValue = previousValue;
            Value = value;
            ReasonText = reasonText;
            ChangedAtGameTime = changedAtGameTime;
            EmitsSemanticTransition = emitsSemanticTransition;
            LatestStateDirtyFlags = latestStateDirtyFlags;
            RejectionReason = rejectionReason;
        }

        internal GCPlayerTransitionOutcome Outcome { get; }
        internal bool Accepted => Outcome == GCPlayerTransitionOutcome.Accepted;
        internal bool IsNoOp => Outcome == GCPlayerTransitionOutcome.NoOp;
        internal bool IsRejected => Outcome == GCPlayerTransitionOutcome.Rejected;
        internal GCPlayerTransitionKind Kind { get; }
        internal int PlayerIndex { get; }
        internal TValue PreviousValue { get; }
        internal TValue Value { get; }
        internal string ReasonText { get; }
        internal float ChangedAtGameTime { get; }
        internal bool EmitsSemanticTransition { get; }
        internal GCPlayerLatestStateDirtyFlags LatestStateDirtyFlags { get; }
        internal GCPlayerTransitionRejectionReason RejectionReason { get; }
        internal bool MarksLatestStateDirty => LatestStateDirtyFlags != GCPlayerLatestStateDirtyFlags.None;
        internal bool MarksRuntimeStateSnapshotDirty =>
            (LatestStateDirtyFlags & GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot) != 0;
        internal bool MarksPlayersHudDirty =>
            (LatestStateDirtyFlags & GCPlayerLatestStateDirtyFlags.PlayersHud) != 0;

        internal static GCPlayerTransitionResult<TValue> Accept(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue previousValue,
            TValue value,
            string reasonText,
            float changedAtGameTime,
            bool emitsSemanticTransition,
            GCPlayerLatestStateDirtyFlags latestStateDirtyFlags
        )
        {
            return new GCPlayerTransitionResult<TValue>(
                GCPlayerTransitionOutcome.Accepted,
                kind,
                playerIndex,
                previousValue,
                value,
                reasonText,
                changedAtGameTime,
                emitsSemanticTransition,
                latestStateDirtyFlags,
                GCPlayerTransitionRejectionReason.None
            );
        }

        internal static GCPlayerTransitionResult<TValue> NoOp(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue currentValue,
            TValue requestedValue,
            string reasonText,
            GCPlayerTransitionRejectionReason rejectionReason
        )
        {
            return new GCPlayerTransitionResult<TValue>(
                GCPlayerTransitionOutcome.NoOp,
                kind,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                -1f,
                false,
                GCPlayerLatestStateDirtyFlags.None,
                rejectionReason
            );
        }

        internal static GCPlayerTransitionResult<TValue> Reject(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue currentValue,
            TValue requestedValue,
            string reasonText,
            GCPlayerTransitionRejectionReason rejectionReason
        )
        {
            return new GCPlayerTransitionResult<TValue>(
                GCPlayerTransitionOutcome.Rejected,
                kind,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                -1f,
                false,
                GCPlayerLatestStateDirtyFlags.None,
                rejectionReason
            );
        }
    }

    internal static class GCPlayerTransitions
    {
        internal static GCPlayerTransitionResult<GCPlayerEliminationState> SetEliminatedPermanent(
            int playerIndex,
            GCPlayerEliminationState currentState,
            string reasonText
        )
        {
            const GCPlayerEliminationState requestedState = GCPlayerEliminationState.Permanent;

            if (currentState == requestedState)
            {
                return GCPlayerTransitionResult<GCPlayerEliminationState>.NoOp(
                    GCPlayerTransitionKind.PlayerEliminationStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            return AcceptEliminationStateChange(playerIndex, currentState, requestedState, reasonText);
        }

        internal static GCPlayerTransitionResult<GCPlayerEliminationState> SetEliminatedRevokable(
            int playerIndex,
            GCPlayerEliminationState currentState,
            string reasonText
        )
        {
            const GCPlayerEliminationState requestedState = GCPlayerEliminationState.Revokable;

            if (currentState == requestedState)
            {
                return GCPlayerTransitionResult<GCPlayerEliminationState>.NoOp(
                    GCPlayerTransitionKind.PlayerEliminationStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            if (currentState == GCPlayerEliminationState.Permanent)
            {
                return GCPlayerTransitionResult<GCPlayerEliminationState>.Reject(
                    GCPlayerTransitionKind.PlayerEliminationStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.InvalidTransition
                );
            }

            return AcceptEliminationStateChange(playerIndex, currentState, requestedState, reasonText);
        }

        internal static GCPlayerTransitionResult<GCPlayerEliminationState> SetRevokeEliminated(
            int playerIndex,
            GCPlayerEliminationState currentState,
            string reasonText
        )
        {
            const GCPlayerEliminationState requestedState = GCPlayerEliminationState.None;

            if (currentState != GCPlayerEliminationState.Revokable)
            {
                return GCPlayerTransitionResult<GCPlayerEliminationState>.Reject(
                    GCPlayerTransitionKind.PlayerEliminationStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.InvalidRevoke
                );
            }

            return AcceptEliminationStateChange(playerIndex, currentState, requestedState, reasonText);
        }

        internal static GCPlayerTransitionResult<GCPlayerFinishState> SetFinishedPermanent(
            int playerIndex,
            GCPlayerFinishState currentState,
            string reasonText
        )
        {
            const GCPlayerFinishState requestedState = GCPlayerFinishState.Permanent;

            if (currentState == requestedState)
            {
                return GCPlayerTransitionResult<GCPlayerFinishState>.NoOp(
                    GCPlayerTransitionKind.PlayerFinishStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            return AcceptFinishStateChange(playerIndex, currentState, requestedState, reasonText);
        }

        internal static GCPlayerTransitionResult<GCPlayerFinishState> SetFinishedRevokable(
            int playerIndex,
            GCPlayerFinishState currentState,
            string reasonText
        )
        {
            const GCPlayerFinishState requestedState = GCPlayerFinishState.Revokable;

            if (currentState == requestedState)
            {
                return GCPlayerTransitionResult<GCPlayerFinishState>.NoOp(
                    GCPlayerTransitionKind.PlayerFinishStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            if (currentState == GCPlayerFinishState.Permanent)
            {
                return GCPlayerTransitionResult<GCPlayerFinishState>.Reject(
                    GCPlayerTransitionKind.PlayerFinishStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.InvalidTransition
                );
            }

            return AcceptFinishStateChange(playerIndex, currentState, requestedState, reasonText);
        }

        internal static GCPlayerTransitionResult<GCPlayerFinishState> SetRevokeFinished(
            int playerIndex,
            GCPlayerFinishState currentState,
            string reasonText
        )
        {
            const GCPlayerFinishState requestedState = GCPlayerFinishState.None;

            if (currentState != GCPlayerFinishState.Revokable)
            {
                return GCPlayerTransitionResult<GCPlayerFinishState>.Reject(
                    GCPlayerTransitionKind.PlayerFinishStateChanged,
                    playerIndex,
                    currentState,
                    requestedState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.InvalidRevoke
                );
            }

            return AcceptFinishStateChange(playerIndex, currentState, requestedState, reasonText);
        }

        internal static GCPlayerTransitionResult<GCPlayerStatusValue> SetStatus(
            int playerIndex,
            GCPlayerStatus currentStatus,
            string currentStatusText,
            GCPlayerStatus requestedStatus,
            string requestedStatusText,
            string reasonText
        )
        {
            var currentValue = new GCPlayerStatusValue(currentStatus, currentStatusText);
            var requestedValue = new GCPlayerStatusValue(requestedStatus, requestedStatusText);

            if (currentValue.Equals(requestedValue))
            {
                return GCPlayerTransitionResult<GCPlayerStatusValue>.NoOp(
                    GCPlayerTransitionKind.PlayerStatusChanged,
                    playerIndex,
                    currentValue,
                    requestedValue,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            return GCPlayerTransitionResult<GCPlayerStatusValue>.Accept(
                GCPlayerTransitionKind.PlayerStatusChanged,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                Time.time,
                emitsSemanticTransition: true,
                latestStateDirtyFlags: GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot |
                    GCPlayerLatestStateDirtyFlags.PlayersHud
            );
        }

        private static GCPlayerTransitionResult<GCPlayerEliminationState> AcceptEliminationStateChange(
            int playerIndex,
            GCPlayerEliminationState currentState,
            GCPlayerEliminationState requestedState,
            string reasonText
        )
        {
            return GCPlayerTransitionResult<GCPlayerEliminationState>.Accept(
                GCPlayerTransitionKind.PlayerEliminationStateChanged,
                playerIndex,
                currentState,
                requestedState,
                reasonText,
                Time.time,
                emitsSemanticTransition: true,
                latestStateDirtyFlags: GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot |
                    GCPlayerLatestStateDirtyFlags.PlayersHud
            );
        }

        private static GCPlayerTransitionResult<GCPlayerFinishState> AcceptFinishStateChange(
            int playerIndex,
            GCPlayerFinishState currentState,
            GCPlayerFinishState requestedState,
            string reasonText
        )
        {
            return GCPlayerTransitionResult<GCPlayerFinishState>.Accept(
                GCPlayerTransitionKind.PlayerFinishStateChanged,
                playerIndex,
                currentState,
                requestedState,
                reasonText,
                Time.time,
                emitsSemanticTransition: true,
                latestStateDirtyFlags: GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot |
                    GCPlayerLatestStateDirtyFlags.PlayersHud
            );
        }
    }
}
