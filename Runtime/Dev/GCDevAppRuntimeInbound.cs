#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC.Dev
{
    internal sealed class GCDevAppRuntimeInboundContext
    {
        internal static readonly GCDevAppRuntimeInboundContext Empty = new GCDevAppRuntimeInboundContext();

        internal bool isPaused;
    }

    internal enum GCDevAppRuntimeInboundStatus
    {
        Unhandled,
        Ignored,
        Intent,
    }

    internal enum GCDevAppRuntimeInboundIntentKind
    {
        None,
        Restart,
        Input,
        TimescaleState,
        RuntimeOutputOptions,
    }

    public struct CompactControllerInputFrame
    {
        public int playerIndex;
        public uint seq;
        public uint timestampMs;
        public GCControllerInputsData inputs;
    }

    internal sealed class GCDevAppRuntimeInboundDecision
    {
        internal readonly GCDevAppRuntimeInboundStatus status;
        internal readonly GCDevAppRuntimeInboundIntentKind intentKind;
        internal readonly int activePlayerIndex;
        internal readonly GCControllerInputsData inputs;
        internal readonly bool hasInputSequence;
        internal readonly uint inputSequence;
        internal readonly float timescale;
        internal readonly bool paused;
        internal readonly bool shouldApplyPause;
        internal readonly string runtimeLogCaptureMode;
        internal readonly string reason;

        private GCDevAppRuntimeInboundDecision(
            GCDevAppRuntimeInboundStatus status,
            GCDevAppRuntimeInboundIntentKind intentKind,
            int activePlayerIndex,
            GCControllerInputsData inputs,
            bool hasInputSequence,
            uint inputSequence,
            float timescale,
            bool paused,
            bool shouldApplyPause,
            string runtimeLogCaptureMode,
            string reason
        )
        {
            this.status = status;
            this.intentKind = intentKind;
            this.activePlayerIndex = activePlayerIndex;
            this.inputs = inputs;
            this.hasInputSequence = hasInputSequence;
            this.inputSequence = inputSequence;
            this.timescale = timescale;
            this.paused = paused;
            this.shouldApplyPause = shouldApplyPause;
            this.runtimeLogCaptureMode = runtimeLogCaptureMode;
            this.reason = reason;
        }

        internal static GCDevAppRuntimeInboundDecision Unhandled(string reason)
        {
            return new GCDevAppRuntimeInboundDecision(
                GCDevAppRuntimeInboundStatus.Unhandled,
                GCDevAppRuntimeInboundIntentKind.None,
                -1,
                default,
                false,
                0,
                1f,
                false,
                false,
                null,
                reason
            );
        }

        internal static GCDevAppRuntimeInboundDecision Ignored(string reason)
        {
            return new GCDevAppRuntimeInboundDecision(
                GCDevAppRuntimeInboundStatus.Ignored,
                GCDevAppRuntimeInboundIntentKind.None,
                -1,
                default,
                false,
                0,
                1f,
                false,
                false,
                null,
                reason
            );
        }

        internal static GCDevAppRuntimeInboundDecision Restart()
        {
            return new GCDevAppRuntimeInboundDecision(
                GCDevAppRuntimeInboundStatus.Intent,
                GCDevAppRuntimeInboundIntentKind.Restart,
                -1,
                default,
                false,
                0,
                1f,
                false,
                false,
                null,
                null
            );
        }

        internal static GCDevAppRuntimeInboundDecision Input(
            int activePlayerIndex,
            GCControllerInputsData inputs,
            bool hasInputSequence,
            uint inputSequence
        )
        {
            return new GCDevAppRuntimeInboundDecision(
                GCDevAppRuntimeInboundStatus.Intent,
                GCDevAppRuntimeInboundIntentKind.Input,
                activePlayerIndex,
                inputs,
                hasInputSequence,
                inputSequence,
                1f,
                false,
                false,
                null,
                null
            );
        }

        internal static GCDevAppRuntimeInboundDecision TimescaleState(
            float timescale,
            bool paused,
            bool shouldApplyPause
        )
        {
            return new GCDevAppRuntimeInboundDecision(
                GCDevAppRuntimeInboundStatus.Intent,
                GCDevAppRuntimeInboundIntentKind.TimescaleState,
                -1,
                default,
                false,
                0,
                timescale,
                paused,
                shouldApplyPause,
                null,
                null
            );
        }

        internal static GCDevAppRuntimeInboundDecision RuntimeOutputOptions(string runtimeLogCaptureMode)
        {
            return new GCDevAppRuntimeInboundDecision(
                GCDevAppRuntimeInboundStatus.Intent,
                GCDevAppRuntimeInboundIntentKind.RuntimeOutputOptions,
                -1,
                default,
                false,
                0,
                1f,
                false,
                false,
                runtimeLogCaptureMode,
                null
            );
        }
    }

    internal sealed class GCDevAppRuntimeInbound
    {
        internal const byte CompactControllerInputTypeByte = 0x44;
        internal const int CompactControllerInputByteLength = 16;
        private const float CompactControllerInputAxisScale = 1000f;

        private readonly Dictionary<int, uint> lastInputSeqByActivePlayerIndex = new Dictionary<int, uint>();

        internal void ResetInputSequences()
        {
            lastInputSeqByActivePlayerIndex.Clear();
        }

        internal GCDevAppRuntimeInboundDecision RouteTextMessage(
            string message,
            GCDevAppRuntimeInboundContext context
        )
        {
            context = context ?? GCDevAppRuntimeInboundContext.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled("empty_message");
            }

            if (!message.Contains("\"type\":\"gcdevtool\""))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled("unsupported_message_type");
            }

            var data = JsonUtility.FromJson<GCDevAppRuntimeDevToolMessage>(message);
            if (data == null || !string.Equals(data.type, "gcdevtool", StringComparison.Ordinal))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled("unsupported_message_type");
            }

            return RouteDevToolAction(data, context);
        }

        internal GCDevAppRuntimeInboundDecision RouteBinaryMessage(byte[] message)
        {
            if (!TryParseCompactControllerInputFrame(message, out var inputFrame))
            {
                return GCDevAppRuntimeInboundDecision.Unhandled("unsupported_binary_message");
            }

            return RouteValidatedCompactControllerInputFrame(inputFrame);
        }

        internal GCDevAppRuntimeInboundDecision RouteValidatedCompactControllerInputFrame(CompactControllerInputFrame inputFrame)
        {
            if (lastInputSeqByActivePlayerIndex.TryGetValue(inputFrame.playerIndex, out var lastSeq) &&
                inputFrame.seq <= lastSeq)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("stale_input_sequence");
            }

            lastInputSeqByActivePlayerIndex[inputFrame.playerIndex] = inputFrame.seq;
            return GCDevAppRuntimeInboundDecision.Input(
                inputFrame.playerIndex,
                inputFrame.inputs,
                true,
                inputFrame.seq
            );
        }

        internal static bool TryParseCompactControllerInputFrame(
            byte[] message,
            out CompactControllerInputFrame inputFrame
        )
        {
            inputFrame = default;
            if (message == null || message.Length != CompactControllerInputByteLength)
            {
                return false;
            }

            if (message[0] != CompactControllerInputTypeByte)
            {
                return false;
            }

            var offset = 1;
            var playerIndex = ReadUInt16LittleEndian(message, offset);
            offset += 2;
            var seq = ReadUInt32LittleEndian(message, offset);
            offset += 4;
            var timestampMs = ReadUInt32LittleEndian(message, offset);
            offset += 4;
            var a0 = Mathf.Clamp(ReadInt16LittleEndian(message, offset), -1000, 1000) / CompactControllerInputAxisScale;
            offset += 2;
            var a1 = Mathf.Clamp(ReadInt16LittleEndian(message, offset), -1000, 1000) / CompactControllerInputAxisScale;
            offset += 2;
            var buttons = message[offset];

            inputFrame = new CompactControllerInputFrame
            {
                playerIndex = playerIndex,
                seq = seq,
                timestampMs = timestampMs,
                inputs = new GCControllerInputsData
                {
                    a0 = a0,
                    a1 = a1,
                    a2 = 0f,
                    a3 = 0f,
                    b0 = (buttons & 1) != 0 ? 1 : 0,
                    b1 = (buttons & 2) != 0 ? 1 : 0,
                    b2 = (buttons & 4) != 0 ? 1 : 0,
                    b3 = 0,
                    b12 = 0,
                    b13 = 0,
                    b14 = 0,
                    b15 = 0
                }
            };
            return true;
        }

        private static GCDevAppRuntimeInboundDecision RouteDevToolAction(
            GCDevAppRuntimeDevToolMessage message,
            GCDevAppRuntimeInboundContext context
        )
        {
            switch (message.action)
            {
                case "restart":
                    return GCDevAppRuntimeInboundDecision.Restart();
                case "input":
                    return RouteTextInput(message.payload);
                case "timescale_state":
                    return RouteTimescaleState(message.payload, context);
                case "runtime_output_options":
                    return RouteRuntimeOutputOptions(message.payload);
                default:
                    return GCDevAppRuntimeInboundDecision.Unhandled("unsupported_devtool_action");
            }
        }

        private static GCDevAppRuntimeInboundDecision RouteTextInput(
            GCDevAppRuntimeDevToolPayload payload
        )
        {
            if (payload == null || payload.inputs == null)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("missing_input_payload");
            }

            if (payload.playerIndex < 0 && payload.playerId > 0)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("legacy_player_id_unsupported");
            }

            if (payload.playerIndex < 0)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("missing_active_player_index");
            }

            return GCDevAppRuntimeInboundDecision.Input(
                payload.playerIndex,
                BuildControllerInputs(payload.inputs),
                false,
                0
            );
        }

        private static GCDevAppRuntimeInboundDecision RouteTimescaleState(
            GCDevAppRuntimeDevToolPayload payload,
            GCDevAppRuntimeInboundContext context
        )
        {
            if (payload == null)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("missing_timescale_payload");
            }

            return GCDevAppRuntimeInboundDecision.TimescaleState(
                payload.timescale,
                payload.paused,
                context.isPaused != payload.paused
            );
        }

        private static GCDevAppRuntimeInboundDecision RouteRuntimeOutputOptions(
            GCDevAppRuntimeDevToolPayload payload
        )
        {
            if (payload == null || payload.runtimeOutput == null)
            {
                return GCDevAppRuntimeInboundDecision.Ignored("missing_runtime_output_payload");
            }

            return GCDevAppRuntimeInboundDecision.RuntimeOutputOptions(payload.runtimeOutput.runtimeLogCapture);
        }

        private static GCControllerInputsData BuildControllerInputs(GCDevAppRuntimeInputData inputs)
        {
            return new GCControllerInputsData
            {
                a0 = inputs.a0,
                a1 = inputs.a1,
                a2 = 0f,
                a3 = 0f,
                b0 = inputs.b0 > 0.5f ? 1 : 0,
                b1 = inputs.b1 > 0.5f ? 1 : 0,
                b2 = inputs.b2 > 0.5f ? 1 : 0,
                b3 = 0,
                b12 = 0,
                b13 = 0,
                b14 = 0,
                b15 = 0
            };
        }

        private static ushort ReadUInt16LittleEndian(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static short ReadInt16LittleEndian(byte[] bytes, int offset)
        {
            return (short)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
        {
            return (uint)(
                bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24)
            );
        }
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeDevToolMessage
    {
        public string type;
        public string action;
        public GCDevAppRuntimeDevToolPayload payload;
        public long timestamp;
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeDevToolPayload
    {
        public float timescale;
        public bool paused;
        public int playerIndex = -1;
        public int playerId;
        public GCDevAppRuntimeInputData inputs;
        public GCDevAppRuntimeOutputOptionsMessage runtimeOutput;
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeInputData
    {
        public float a0;
        public float a1;
        public float b0;
        public float b1;
        public float b2;
    }

    [Serializable]
    internal sealed class GCDevAppRuntimeOutputOptionsMessage
    {
        public string runtimeLogCapture;
    }
}
#endif
