#if UNITY_EDITOR
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal sealed class GCMetadataJsonFile
    {
        internal const string FileName = "gc.metadata.json";
        internal const string UnityPlatformId = "unity";

        internal readonly string gameKey;
        internal readonly string gameName;
        internal readonly string platformId;
        internal readonly Dictionary<string, GCMetadataJsonEntry> entries;
        internal readonly Dictionary<string, GCMetadataJsonColorVariants> playerColors;

        internal GCMetadataJsonFile(
            string gameKey,
            string gameName,
            string platformId,
            Dictionary<string, GCMetadataJsonEntry> entries,
            Dictionary<string, GCMetadataJsonColorVariants> playerColors
        )
        {
            this.gameKey = gameKey;
            this.gameName = gameName;
            this.platformId = platformId;
            this.entries = CloneEntries(entries);
            this.playerColors = ClonePlayerColors(playerColors);
        }

        internal bool TryGetEntry(string entryKey, out GCMetadataJsonEntry entry)
        {
            entry = null;
            return entries != null && entryKey != null && entries.TryGetValue(entryKey, out entry);
        }

        private static Dictionary<string, GCMetadataJsonEntry> CloneEntries(Dictionary<string, GCMetadataJsonEntry> sourceEntries)
        {
            var clonedEntries = new Dictionary<string, GCMetadataJsonEntry>();
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

        private static Dictionary<string, GCMetadataJsonColorVariants> ClonePlayerColors(Dictionary<string, GCMetadataJsonColorVariants> sourceColors)
        {
            var clonedColors = new Dictionary<string, GCMetadataJsonColorVariants>();
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

    internal sealed class GCMetadataJsonEntry
    {
        internal readonly string entryKey;
        internal readonly string name;
        internal readonly int minPlayers;
        internal readonly int maxPlayers;
        internal readonly bool botSupport;

        internal GCMetadataJsonEntry(string entryKey, string name, int minPlayers, int maxPlayers, bool botSupport)
        {
            this.entryKey = entryKey;
            this.name = name;
            this.minPlayers = minPlayers;
            this.maxPlayers = maxPlayers;
            this.botSupport = botSupport;
        }

        internal GCMetadataJsonEntry Clone()
        {
            return new GCMetadataJsonEntry(entryKey, name, minPlayers, maxPlayers, botSupport);
        }
    }

    internal sealed class GCMetadataJsonColorVariants
    {
        internal readonly GCMetadataJsonRgbColor baseColor;
        internal readonly GCMetadataJsonRgbColor mutedColor;
        internal readonly GCMetadataJsonRgbColor mutedDarkerColor;

        internal GCMetadataJsonColorVariants(
            GCMetadataJsonRgbColor baseColor,
            GCMetadataJsonRgbColor mutedColor,
            GCMetadataJsonRgbColor mutedDarkerColor
        )
        {
            this.baseColor = baseColor;
            this.mutedColor = mutedColor;
            this.mutedDarkerColor = mutedDarkerColor;
        }

        internal GCMetadataJsonColorVariants Clone()
        {
            return new GCMetadataJsonColorVariants(baseColor, mutedColor, mutedDarkerColor);
        }
    }

    internal struct GCMetadataJsonRgbColor
    {
        internal readonly int r;
        internal readonly int g;
        internal readonly int b;

        internal GCMetadataJsonRgbColor(int r, int g, int b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }

    internal enum GCMetadataJsonParseState
    {
        MissingFile,
        InvalidJson,
        InvalidRoot,
        ParsedObject,
        ReadError,
    }

    internal sealed class GCMetadataJsonParsedFile
    {
        internal readonly string path;
        internal readonly GCMetadataJsonParseState state;
        internal readonly JObject jsonObject;
        internal readonly string message;

        private GCMetadataJsonParsedFile(string path, GCMetadataJsonParseState state, JObject jsonObject, string message)
        {
            this.path = path;
            this.state = state;
            this.jsonObject = jsonObject;
            this.message = message;
        }

        internal static GCMetadataJsonParsedFile Missing(string path)
        {
            return new GCMetadataJsonParsedFile(path, GCMetadataJsonParseState.MissingFile, null, "gc.metadata.json was not found at " + path + ".");
        }

        internal static GCMetadataJsonParsedFile InvalidJson(string path, string message)
        {
            return new GCMetadataJsonParsedFile(path, GCMetadataJsonParseState.InvalidJson, null, message);
        }

        internal static GCMetadataJsonParsedFile InvalidRoot(string path)
        {
            return new GCMetadataJsonParsedFile(path, GCMetadataJsonParseState.InvalidRoot, null, "gc.metadata.json must be a JSON object.");
        }

        internal static GCMetadataJsonParsedFile Parsed(string path, JObject jsonObject)
        {
            return new GCMetadataJsonParsedFile(path, GCMetadataJsonParseState.ParsedObject, jsonObject, null);
        }

        internal static GCMetadataJsonParsedFile ReadError(string path, string message)
        {
            return new GCMetadataJsonParsedFile(path, GCMetadataJsonParseState.ReadError, null, message);
        }
    }

    internal sealed class GCMetadataJsonReadResult
    {
        internal readonly GCMetadataJsonParsedFile parsedFile;
        internal readonly GCDevJsonValidationResult validation;
        internal readonly GCMetadataJsonFile data;

        internal GCMetadataJsonReadResult(
            GCMetadataJsonParsedFile parsedFile,
            GCDevJsonValidationResult validation,
            GCMetadataJsonFile data
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
