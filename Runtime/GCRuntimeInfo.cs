using System;
using System.Globalization;
using System.Text;

namespace DSB.GC
{
    [Serializable]
    internal sealed class GCRuntimeInfo
    {
        public string platform;
        public string packageName;
        public string packageVersion;
        public int gameProtocolVersion;
    }

    internal static class GCRuntimeInfoJson
    {
        internal static string Serialize(GCRuntimeInfo runtimeInfo)
        {
            if (runtimeInfo == null)
            {
                throw new ArgumentNullException(nameof(runtimeInfo));
            }

            var builder = new StringBuilder();
            builder.Append('{');
            AppendJsonProperty(builder, "platform", runtimeInfo.platform);
            builder.Append(',');
            AppendJsonProperty(builder, "packageName", runtimeInfo.packageName);
            builder.Append(',');
            AppendJsonProperty(builder, "packageVersion", runtimeInfo.packageVersion);
            builder.Append(',');
            AppendJsonProperty(builder, "gameProtocolVersion", runtimeInfo.gameProtocolVersion);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendJsonProperty(StringBuilder builder, string propertyName, string value)
        {
            AppendJsonString(builder, propertyName);
            builder.Append(':');
            AppendJsonString(builder, value);
        }

        private static void AppendJsonProperty(StringBuilder builder, string propertyName, int value)
        {
            AppendJsonString(builder, propertyName);
            builder.Append(':');
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            foreach (var character in value)
            {
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
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
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
