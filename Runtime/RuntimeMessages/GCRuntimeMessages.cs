using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DSB.GC.RuntimeMessages
{
    internal static class GCRuntimeMessagePath
    {
        internal const string RuntimeMessages = "runtime_messages";
    }

    internal static class GCRuntimeMessageTypes
    {
        internal const string Diagnostic = "gc.diagnostic";
    }

    internal sealed class GCRuntimeMessageRecord
    {
        internal const int SchemaVersion = 1;

        internal readonly string messageType;
        internal readonly long sequence;
        internal readonly long runtimeTimeMs;
        internal readonly string payloadJson;

        internal GCRuntimeMessageRecord(string messageType, long sequence, long runtimeTimeMs, string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(messageType))
            {
                throw new ArgumentException("Runtime message type is required.", nameof(messageType));
            }

            if (sequence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), "Runtime message sequence must be one-based.");
            }

            if (runtimeTimeMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeTimeMs), "Runtime message time must be non-negative.");
            }

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new ArgumentException("Runtime message payload JSON is required.", nameof(payloadJson));
            }

            this.messageType = messageType;
            this.sequence = sequence;
            this.runtimeTimeMs = runtimeTimeMs;
            this.payloadJson = payloadJson;
        }

        internal string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"schemaVersion\":").Append(SchemaVersion);
            builder.Append(",\"messageType\":");
            GCRuntimeJson.AppendString(builder, messageType);
            builder.Append(",\"sequence\":").Append(sequence.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"runtimeTimeMs\":").Append(runtimeTimeMs.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"payload\":").Append(payloadJson);
            builder.Append("}");
            return builder.ToString();
        }
    }

    internal static class GCRuntimeMessageOutput
    {
        internal const int EnvelopeSchemaVersion = 1;

        private static long sequence;
        private static bool hasActiveRun;
        private static double activeRunStartSeconds;
        private static Func<double> realtimeSecondsProvider = DefaultRealtimeSecondsProvider;

        internal static event Action<string> RuntimeMessagesEmitted;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GamingCouchRuntimeMessages(string runtimeMessagesJson);
#endif

        internal static void BeginActiveRun()
        {
            activeRunStartSeconds = NowSeconds();
            sequence = 0;
            hasActiveRun = true;
        }

        internal static GCRuntimeMessageRecord CreateRecord(string messageType, string payloadJson)
        {
            EnsureActiveRun();
            return new GCRuntimeMessageRecord(
                messageType,
                ++sequence,
                ResolveRuntimeTimeMs(),
                payloadJson
            );
        }

        internal static string EmitSingle(string messageType, string payloadJson)
        {
            return EmitBatch(new[] { CreateRecord(messageType, payloadJson) });
        }

        internal static string EmitBatch(IReadOnlyList<GCRuntimeMessageRecord> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Runtime message batches must contain at least one message.", nameof(messages));
            }

            var json = BuildEnvelopeJson(messages);
            RuntimeMessagesEmitted?.Invoke(json);

#if UNITY_WEBGL && !UNITY_EDITOR
            GamingCouchRuntimeMessages(json);
#endif

            return json;
        }

        internal static string BuildEnvelopeJson(IReadOnlyList<GCRuntimeMessageRecord> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Runtime message batches must contain at least one message.", nameof(messages));
            }

            var builder = new StringBuilder();
            builder.Append("{\"type\":");
            GCRuntimeJson.AppendString(builder, GCRuntimeMessagePath.RuntimeMessages);
            builder.Append(",\"schemaVersion\":").Append(EnvelopeSchemaVersion);
            builder.Append(",\"messages\":[");

            for (var index = 0; index < messages.Count; index++)
            {
                if (messages[index] == null)
                {
                    throw new ArgumentException("Runtime message batches cannot contain null messages.", nameof(messages));
                }

                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(messages[index].ToJson());
            }

            builder.Append("]}");
            return builder.ToString();
        }

        internal static void ResetForTests(Func<double> testRealtimeSecondsProvider)
        {
            sequence = 0;
            hasActiveRun = false;
            activeRunStartSeconds = 0;
            realtimeSecondsProvider = testRealtimeSecondsProvider ?? DefaultRealtimeSecondsProvider;
            RuntimeMessagesEmitted = null;
        }

        private static void EnsureActiveRun()
        {
            if (!hasActiveRun)
            {
                BeginActiveRun();
            }
        }

        private static long ResolveRuntimeTimeMs()
        {
            var elapsedSeconds = Math.Max(0, NowSeconds() - activeRunStartSeconds);
            return (long)Math.Floor(elapsedSeconds * 1000.0);
        }

        private static double NowSeconds()
        {
            return realtimeSecondsProvider();
        }

        private static double DefaultRealtimeSecondsProvider()
        {
            return Time.realtimeSinceStartupAsDouble;
        }
    }

    internal static class GCRuntimeJson
    {
        internal static void AppendString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
        }
    }
}
