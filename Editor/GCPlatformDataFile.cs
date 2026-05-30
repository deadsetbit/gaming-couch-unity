#if UNITY_EDITOR
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal sealed class GCPlatformDataFile
    {
        internal const string FileName = "gc.platform.json";
        internal const string UnityPlatformId = "unity";

        internal readonly string gameKey;
        internal readonly string gameName;
        internal readonly string platformId;
        internal readonly Dictionary<string, GCPlatformDataEntry> entries;
        internal readonly Dictionary<string, GCPlatformDataColorVariants> playerColors;

        internal GCPlatformDataFile(
            string gameKey,
            string gameName,
            string platformId,
            Dictionary<string, GCPlatformDataEntry> entries,
            Dictionary<string, GCPlatformDataColorVariants> playerColors
        )
        {
            this.gameKey = gameKey;
            this.gameName = gameName;
            this.platformId = platformId;
            this.entries = CloneEntries(entries);
            this.playerColors = ClonePlayerColors(playerColors);
        }

        internal bool TryGetEntry(string entryKey, out GCPlatformDataEntry entry)
        {
            entry = null;
            return entries != null && entryKey != null && entries.TryGetValue(entryKey, out entry);
        }

        private static Dictionary<string, GCPlatformDataEntry> CloneEntries(Dictionary<string, GCPlatformDataEntry> sourceEntries)
        {
            var clonedEntries = new Dictionary<string, GCPlatformDataEntry>();
            if (sourceEntries == null)
            {
                return clonedEntries;
            }

            foreach (var pair in sourceEntries)
            {
                if (pair.Value != null)
                {
                    clonedEntries[pair.Key] = pair.Value.Clone();
                }
            }

            return clonedEntries;
        }

        private static Dictionary<string, GCPlatformDataColorVariants> ClonePlayerColors(Dictionary<string, GCPlatformDataColorVariants> sourceColors)
        {
            var clonedColors = new Dictionary<string, GCPlatformDataColorVariants>();
            if (sourceColors == null)
            {
                return clonedColors;
            }

            foreach (var pair in sourceColors)
            {
                if (pair.Value != null)
                {
                    clonedColors[pair.Key] = pair.Value.Clone();
                }
            }

            return clonedColors;
        }
    }

    internal sealed class GCPlatformDataEntry
    {
        internal readonly string entryKey;
        internal readonly string name;
        internal readonly int minPlayers;
        internal readonly int maxPlayers;
        internal readonly bool botSupport;

        internal GCPlatformDataEntry(string entryKey, string name, int minPlayers, int maxPlayers, bool botSupport)
        {
            this.entryKey = entryKey;
            this.name = name;
            this.minPlayers = minPlayers;
            this.maxPlayers = maxPlayers;
            this.botSupport = botSupport;
        }

        internal GCPlatformDataEntry Clone()
        {
            return new GCPlatformDataEntry(entryKey, name, minPlayers, maxPlayers, botSupport);
        }
    }

    internal sealed class GCPlatformDataColorVariants
    {
        internal readonly GCPlatformDataRgbColor baseColor;
        internal readonly GCPlatformDataRgbColor mutedColor;
        internal readonly GCPlatformDataRgbColor mutedDarkerColor;

        internal GCPlatformDataColorVariants(
            GCPlatformDataRgbColor baseColor,
            GCPlatformDataRgbColor mutedColor,
            GCPlatformDataRgbColor mutedDarkerColor
        )
        {
            this.baseColor = baseColor;
            this.mutedColor = mutedColor;
            this.mutedDarkerColor = mutedDarkerColor;
        }

        internal GCPlatformDataColorVariants Clone()
        {
            return new GCPlatformDataColorVariants(baseColor, mutedColor, mutedDarkerColor);
        }
    }

    internal struct GCPlatformDataRgbColor
    {
        internal readonly int r;
        internal readonly int g;
        internal readonly int b;

        internal GCPlatformDataRgbColor(int r, int g, int b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }

    internal enum GCPlatformDataParseState
    {
        MissingFile,
        InvalidJson,
        InvalidRoot,
        ParsedObject,
        ReadError,
    }

    internal sealed class GCPlatformDataParsedFile
    {
        internal readonly string path;
        internal readonly GCPlatformDataParseState state;
        internal readonly JObject jsonObject;
        internal readonly string message;

        private GCPlatformDataParsedFile(string path, GCPlatformDataParseState state, JObject jsonObject, string message)
        {
            this.path = path;
            this.state = state;
            this.jsonObject = jsonObject;
            this.message = message;
        }

        internal static GCPlatformDataParsedFile Missing(string path)
        {
            return new GCPlatformDataParsedFile(path, GCPlatformDataParseState.MissingFile, null, "gc.platform.json was not found at " + path + ".");
        }

        internal static GCPlatformDataParsedFile InvalidJson(string path, string message)
        {
            return new GCPlatformDataParsedFile(path, GCPlatformDataParseState.InvalidJson, null, message);
        }

        internal static GCPlatformDataParsedFile InvalidRoot(string path)
        {
            return new GCPlatformDataParsedFile(path, GCPlatformDataParseState.InvalidRoot, null, "gc.platform.json must be a JSON object.");
        }

        internal static GCPlatformDataParsedFile Parsed(string path, JObject jsonObject)
        {
            return new GCPlatformDataParsedFile(path, GCPlatformDataParseState.ParsedObject, jsonObject, null);
        }

        internal static GCPlatformDataParsedFile ReadError(string path, string message)
        {
            return new GCPlatformDataParsedFile(path, GCPlatformDataParseState.ReadError, null, message);
        }
    }

    internal sealed class GCPlatformDataReadResult
    {
        internal readonly GCPlatformDataParsedFile parsedFile;
        internal readonly GCDevJsonValidationResult validation;
        internal readonly GCPlatformDataFile data;

        internal GCPlatformDataReadResult(
            GCPlatformDataParsedFile parsedFile,
            GCDevJsonValidationResult validation,
            GCPlatformDataFile data
        )
        {
            this.parsedFile = parsedFile;
            this.validation = validation ?? GCDevJsonValidationResult.Valid();
            this.data = data;
        }

        internal bool IsValid
        {
            get { return data != null && validation != null && validation.IsValid && validation.WarningCount == 0; }
        }
    }
}
#endif
