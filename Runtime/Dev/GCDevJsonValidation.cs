#if UNITY_EDITOR
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DSB.GC.Dev
{
    internal enum GCDevJsonIssueSeverity
    {
        Error,
        Warning,
    }

    internal enum GCDevJsonIssueCode
    {
        MissingFile,
        InvalidJson,
        InvalidRoot,
        UnsupportedDevVersion,
        InvalidEntryKey,
        InvalidSeed,
        InvalidSeatCount,
        InvalidSeatFields,
        NoEnabledSeats,
        ReadError,
        WriteError,
    }

    internal sealed class GCDevJsonIssue
    {
        internal readonly GCDevJsonIssueSeverity severity;
        internal readonly GCDevJsonIssueCode code;
        internal readonly string message;
        internal readonly string path;
        internal readonly int seatIndex;
        internal readonly string fieldName;

        private GCDevJsonIssue(
            GCDevJsonIssueSeverity severity,
            GCDevJsonIssueCode code,
            string message,
            string path,
            int seatIndex,
            string fieldName
        )
        {
            this.severity = severity;
            this.code = code;
            this.message = message;
            this.path = path;
            this.seatIndex = seatIndex;
            this.fieldName = fieldName;
        }

        internal static GCDevJsonIssue Error(GCDevJsonIssueCode code, string message, string path, int seatIndex = 0, string fieldName = null)
        {
            return new GCDevJsonIssue(GCDevJsonIssueSeverity.Error, code, message, path, seatIndex, fieldName);
        }

        internal static GCDevJsonIssue Warning(GCDevJsonIssueCode code, string message, string path, int seatIndex = 0, string fieldName = null)
        {
            return new GCDevJsonIssue(GCDevJsonIssueSeverity.Warning, code, message, path, seatIndex, fieldName);
        }
    }

    internal sealed class GCDevJsonValidationResult
    {
        internal readonly GCDevJsonIssue[] issues;

        private GCDevJsonValidationResult(GCDevJsonIssue[] issues)
        {
            this.issues = issues ?? new GCDevJsonIssue[0];
        }

        internal bool IsValid
        {
            get { return ErrorCount == 0; }
        }

        internal int ErrorCount
        {
            get
            {
                var errorCount = 0;
                for (var index = 0; index < issues.Length; index++)
                {
                    if (issues[index] != null && issues[index].severity == GCDevJsonIssueSeverity.Error)
                    {
                        errorCount++;
                    }
                }

                return errorCount;
            }
        }

        internal static GCDevJsonValidationResult Valid()
        {
            return new GCDevJsonValidationResult(new GCDevJsonIssue[0]);
        }

        internal static GCDevJsonValidationResult FromIssue(GCDevJsonIssue issue)
        {
            if (issue == null)
            {
                return Valid();
            }

            return new GCDevJsonValidationResult(new[] { issue });
        }

        internal static GCDevJsonValidationResult FromIssues(List<GCDevJsonIssue> issues)
        {
            return new GCDevJsonValidationResult(issues != null ? issues.ToArray() : new GCDevJsonIssue[0]);
        }
    }

    internal static class GCDevJsonValidation
    {
        internal static GCDevJsonReadResult BuildReadResult(GCDevJsonParsedFile parsedFile)
        {
            if (parsedFile == null)
            {
                parsedFile = GCDevJsonParsedFile.ReadError(null, "gc.dev.json could not be read because parser state was missing.");
            }

            if (parsedFile.state == GCDevJsonParseState.MissingFile)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.MissingFile, parsedFile.message);
            }

            if (parsedFile.state == GCDevJsonParseState.InvalidJson)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidJson, parsedFile.message);
            }

            if (parsedFile.state == GCDevJsonParseState.InvalidRoot)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidRoot, parsedFile.message);
            }

            if (parsedFile.state == GCDevJsonParseState.ReadError)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.ReadError, parsedFile.message);
            }

            return ValidateParsedObject(parsedFile);
        }

        internal static GCDevJsonValidationResult ValidateData(GCDevJsonFile data, string path = null)
        {
            var issues = new List<GCDevJsonIssue>();
            if (data == null)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json data is missing.", path));
                return GCDevJsonValidationResult.FromIssues(issues);
            }

            if (data.devVersion != GCDevJsonFile.SupportedDevVersion)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.UnsupportedDevVersion, "gc.dev.json must use devVersion " + GCDevJsonFile.SupportedDevVersion + ".", path));
            }

            if (!IsValidEntryKey(data.entryKey))
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidEntryKey, GetInvalidEntryKeyMessage(), path));
            }

            if (!IsValidSeed(data.seed))
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidSeed, GetInvalidSeedMessage(), path));
            }

            AddSeatIssues(data.seats, path, issues);
            return GCDevJsonValidationResult.FromIssues(issues);
        }

        private static GCDevJsonReadResult ValidateParsedObject(GCDevJsonParsedFile parsedFile)
        {
            var jsonObject = parsedFile.jsonObject;
            var path = parsedFile.path;
            if (jsonObject == null)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidRoot, "gc.dev.json must be a JSON object.");
            }

            if (!TryReadSupportedDevVersion(jsonObject["devVersion"]))
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.UnsupportedDevVersion, "gc.dev.json must use devVersion " + GCDevJsonFile.SupportedDevVersion + ".");
            }

            string entryKey;
            if (!TryReadString(jsonObject["entryKey"], out entryKey) || !IsValidEntryKey(entryKey))
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidEntryKey, GetInvalidEntryKeyMessage());
            }

            string seed;
            if (!TryReadString(jsonObject["seed"], out seed) || !IsValidSeed(seed))
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidSeed, GetInvalidSeedMessage());
            }

            var seatsToken = jsonObject["seats"];
            var seatsArray = seatsToken as JArray;
            if (seatsArray == null || seatsArray.Count != GCDevJsonFile.SeatCount)
            {
                return InvalidReadResult(parsedFile, GCDevJsonIssueCode.InvalidSeatCount, "gc.dev.json must contain exactly " + GCDevJsonFile.SeatCount + " seats.");
            }

            var seats = new GCDevJsonSeat[GCDevJsonFile.SeatCount];
            for (var index = 0; index < seatsArray.Count; index++)
            {
                string name;
                bool enabled;
                bool isBot;
                if (!TryReadSeat(seatsArray[index], index + 1, path, out name, out enabled, out isBot, out var issue))
                {
                    return new GCDevJsonReadResult(
                        parsedFile,
                        GCDevJsonValidationResult.FromIssue(issue),
                        null
                    );
                }

                seats[index] = new GCDevJsonSeat(name, enabled, isBot);
            }

            var data = new GCDevJsonFile(GCDevJsonFile.SupportedDevVersion, entryKey, seed, seats);
            var validation = ValidateData(data, path);
            return new GCDevJsonReadResult(parsedFile, validation, validation.IsValid ? data : null);
        }

        private static GCDevJsonReadResult InvalidReadResult(GCDevJsonParsedFile parsedFile, GCDevJsonIssueCode code, string message)
        {
            return new GCDevJsonReadResult(
                parsedFile,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(code, message, parsedFile.path)),
                null
            );
        }

        private static bool TryReadSupportedDevVersion(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer)
            {
                return false;
            }

            var devVersion = token.Value<long>();
            return devVersion == GCDevJsonFile.SupportedDevVersion;
        }

        private static bool TryReadString(JToken token, out string value)
        {
            value = null;
            if (token == null || token.Type != JTokenType.String)
            {
                return false;
            }

            value = token.Value<string>();
            return true;
        }

        private static bool TryReadBool(JToken token, out bool value)
        {
            value = false;
            if (token == null || token.Type != JTokenType.Boolean)
            {
                return false;
            }

            value = token.Value<bool>();
            return true;
        }

        private static bool TryReadSeat(
            JToken token,
            int seatIndex,
            string path,
            out string name,
            out bool enabled,
            out bool isBot,
            out GCDevJsonIssue issue
        )
        {
            name = null;
            enabled = false;
            isBot = false;
            issue = null;

            var seatObject = token as JObject;
            if (seatObject == null)
            {
                issue = InvalidSeatIssue(path, seatIndex, null);
                return false;
            }

            if (!TryReadString(seatObject["name"], out name) || !IsValidPlayerName(name))
            {
                issue = InvalidSeatIssue(path, seatIndex, "name");
                return false;
            }

            if (!TryReadBool(seatObject["enabled"], out enabled))
            {
                issue = InvalidSeatIssue(path, seatIndex, "enabled");
                return false;
            }

            if (!TryReadBool(seatObject["isBot"], out isBot))
            {
                issue = InvalidSeatIssue(path, seatIndex, "isBot");
                return false;
            }

            return true;
        }

        private static void AddSeatIssues(GCDevJsonSeat[] seats, string path, List<GCDevJsonIssue> issues)
        {
            if (seats == null || seats.Length != GCDevJsonFile.SeatCount)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidSeatCount, "gc.dev.json must contain exactly " + GCDevJsonFile.SeatCount + " seats.", path));
                return;
            }

            var hasEnabledSeat = false;
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = seats[index];
                if (seat == null)
                {
                    issues.Add(InvalidSeatIssue(path, index + 1, null));
                    continue;
                }

                if (!IsValidPlayerName(seat.name))
                {
                    issues.Add(InvalidSeatIssue(path, index + 1, "name"));
                }

                if (seat.enabled)
                {
                    hasEnabledSeat = true;
                }
            }

            if (!hasEnabledSeat)
            {
                issues.Add(GCDevJsonIssue.Error(GCDevJsonIssueCode.NoEnabledSeats, "gc.dev.json must enable at least one seat.", path));
            }
        }

        private static GCDevJsonIssue InvalidSeatIssue(string path, int seatIndex, string fieldName)
        {
            return GCDevJsonIssue.Error(
                GCDevJsonIssueCode.InvalidSeatFields,
                "Each gc.dev.json seat must include a " + GCDevJsonFile.PlayerNameMinLength + "-" + GCDevJsonFile.PlayerNameMaxLength + " character name, enabled, and isBot.",
                path,
                seatIndex,
                fieldName
            );
        }

        private static bool IsValidPlayerName(string name)
        {
            return name != null &&
                   name.Trim().Length >= GCDevJsonFile.PlayerNameMinLength &&
                   name.Length <= GCDevJsonFile.PlayerNameMaxLength;
        }

        private static bool IsValidEntryKey(string entryKey)
        {
            return !string.IsNullOrWhiteSpace(entryKey);
        }

        private static bool IsValidSeed(string seed)
        {
            if (seed == GCDevJsonFile.RandomSeed)
            {
                return true;
            }

            if (string.IsNullOrEmpty(seed))
            {
                return false;
            }

            var parsedSeed = 0;
            for (var index = 0; index < seed.Length; index++)
            {
                var character = seed[index];
                if (character < '0' || character > '9')
                {
                    return false;
                }

                parsedSeed = parsedSeed * 10 + character - '0';
                if (parsedSeed > GCDevJsonFile.MaxSeed)
                {
                    return false;
                }
            }

            return parsedSeed >= GCDevJsonFile.MinSeed;
        }

        private static string GetInvalidSeedMessage()
        {
            return "gc.dev.json seed must be \"random\" or an integer string from " + GCDevJsonFile.MinSeed + " to " + GCDevJsonFile.MaxSeed + ".";
        }

        private static string GetInvalidEntryKeyMessage()
        {
            return "gc.dev.json entryKey must be a non-empty string.";
        }
    }
}
#endif
