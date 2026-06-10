using System;
using UnityEngine;

namespace DSB.GC
{
    internal enum GCPlayerTransitionKind
    {
        PlayerStatusChanged = 1,
    }

    internal enum GCPlayerTransitionRejectionReason
    {
        None = 0,
        DuplicateValue = 1,
        InvalidTransition = 2,
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
    }
}
