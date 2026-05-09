using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DSB.GC.Dev;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

internal sealed class GCDevJsonInspectorState
{
    private const double PollIntervalSeconds = 0.25;

    private readonly GCDevJsonStore devStore;
    private readonly GCMetadataJsonStore metadataStore;

    private GCDevJsonFile cleanData;
    private GCDevJsonReadResult devReadResult;
    private GCMetadataJsonReadResult metadataReadResult;
    private GCDevJsonValidationResult draftValidation;
    private GCDevJsonWriteResult lastWriteResult;
    private GCRootJsonFileStamp devFileStamp;
    private GCRootJsonFileStamp metadataFileStamp;
    private bool hasConflict;
    private bool hasPendingPlayChange;
    private bool hasUnloadedPlayDevJsonChange;
    private double nextPollTime;

    internal GCDevJsonInspectorState()
    {
        var projectRootResolver = new GCUnityLocalProjectRootResolver();
        devStore = new GCDevJsonStore(projectRootResolver);
        metadataStore = new GCMetadataJsonStore(projectRootResolver);
        Reload();
    }

    internal GCDevJsonDraft Draft { get; private set; }

    internal GCDevJsonReadResult DevReadResult
    {
        get { return devReadResult; }
    }

    internal GCMetadataJsonReadResult MetadataReadResult
    {
        get { return metadataReadResult; }
    }

    internal GCDevJsonValidationResult DraftValidation
    {
        get { return draftValidation; }
    }

    internal GCDevJsonWriteResult LastWriteResult
    {
        get { return lastWriteResult; }
    }

    internal bool HasDraft
    {
        get { return Draft != null; }
    }

    internal bool HasValidMetadata
    {
        get { return metadataReadResult != null && metadataReadResult.IsValid; }
    }

    internal bool IsDirty
    {
        get { return Draft != null && !AreEqual(cleanData, Draft.ToFile()); }
    }

    internal bool HasConflict
    {
        get { return hasConflict; }
    }

    internal bool HasPendingPlayChange
    {
        get { return hasPendingPlayChange; }
    }

    internal bool CanApply
    {
        get { return Draft != null && IsDirty && !hasConflict && draftValidation != null && draftValidation.IsValid; }
    }

    internal bool CanWriteDraft
    {
        get { return Draft != null && hasConflict && draftValidation != null && draftValidation.IsValid; }
    }

    internal string DevJsonPath
    {
        get
        {
            if (devReadResult != null && devReadResult.parsedFile != null)
            {
                return devReadResult.parsedFile.path;
            }

            return devStore.ResolveFilePath();
        }
    }

    internal void Reload()
    {
        ReloadFromDisk(EditorApplication.isPlaying && hasPendingPlayChange);
    }

    internal bool PollForExternalChanges()
    {
        return PollForExternalChanges(false);
    }

    internal bool PollForExternalChanges(bool force)
    {
        var now = EditorApplication.timeSinceStartup;
        if (!force && now < nextPollTime)
        {
            return false;
        }

        nextPollTime = now + PollIntervalSeconds;

        var nextMetadataFileStamp = metadataStore.ReadFileStamp();
        var nextDevFileStamp = devStore.ReadFileStamp();
        var metadataChanged = !nextMetadataFileStamp.IsSameAs(metadataFileStamp);
        var devChanged = !nextDevFileStamp.IsSameAs(devFileStamp);
        if (!metadataChanged && !devChanged)
        {
            return false;
        }

        var changedState = false;
        var isPlaying = EditorApplication.isPlaying;

        if (metadataChanged)
        {
            metadataFileStamp = nextMetadataFileStamp;
            RefreshMetadata();
            changedState = true;
            if (isPlaying)
            {
                hasPendingPlayChange = true;
            }
        }

        if (devChanged)
        {
            devFileStamp = nextDevFileStamp;
            changedState = true;
            lastWriteResult = null;

            if (isPlaying)
            {
                hasPendingPlayChange = true;
                if (IsDirty || hasConflict)
                {
                    EnterConflict();
                }
                else
                {
                    hasUnloadedPlayDevJsonChange = true;
                }
            }
            else if (IsDirty || hasConflict)
            {
                EnterConflict();
            }
            else
            {
                ReloadFromDisk(false);
            }
        }

        return changedState;
    }

    internal GCEditorPlayPreflightResult PrepareForPlayBoundary(GCEditorPlayPreflightContext context)
    {
        var validationResult = ValidateForPlayBoundary(context);
        if (!validationResult.success)
        {
            return validationResult;
        }

        var boundaryName = GCEditorPlayPreflight.GetBoundaryDisplayName(context);
        if (!IsDirty)
        {
            return GCEditorPlayPreflightResult.Succeeded();
        }

        if (WriteDraftToDisk())
        {
            return GCEditorPlayPreflightResult.Succeeded();
        }

        return GCEditorPlayPreflightResult.Failed(
            boundaryName + " blocked because the current gc.dev.json inspector draft could not be auto-applied.",
            lastWriteResult != null ? lastWriteResult.path : DevJsonPath,
            lastWriteResult != null ? lastWriteResult.validation : draftValidation
        );
    }

    internal GCEditorPlayPreflightResult ValidateForPlayBoundary(GCEditorPlayPreflightContext context)
    {
        PollForExternalChanges(true);
        ValidateDraft();

        var boundaryName = GCEditorPlayPreflight.GetBoundaryDisplayName(context);
        if (hasConflict)
        {
            return GCEditorPlayPreflightResult.Failed(
                boundaryName + " blocked because the current gc.dev.json inspector draft is conflicted. Reload from disk or write the draft before continuing.",
                DevJsonPath,
                draftValidation
            );
        }

        if (!IsDirty)
        {
            return GCEditorPlayPreflightResult.Succeeded();
        }

        if (draftValidation == null || !draftValidation.IsValid)
        {
            return GCEditorPlayPreflightResult.Failed(
                boundaryName + " blocked because the current gc.dev.json inspector draft is invalid.",
                DevJsonPath,
                draftValidation
            );
        }

        return GCEditorPlayPreflightResult.Succeeded();
    }

    internal bool MarkPlayChangesCaptured()
    {
        var changed = hasPendingPlayChange || hasUnloadedPlayDevJsonChange;
        hasPendingPlayChange = false;
        hasUnloadedPlayDevJsonChange = false;

        if (!hasConflict && !IsDirty)
        {
            ReloadFromDisk(false);
            return true;
        }

        UpdateFileStamps();
        return changed;
    }

    internal bool HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            var changed = hasPendingPlayChange || hasUnloadedPlayDevJsonChange;
            hasPendingPlayChange = false;
            hasUnloadedPlayDevJsonChange = false;
            return changed;
        }

        if (change == PlayModeStateChange.EnteredEditMode)
        {
            var shouldReloadPendingDiskState = hasUnloadedPlayDevJsonChange && !hasConflict && !IsDirty;
            var changed = hasPendingPlayChange || hasUnloadedPlayDevJsonChange;
            hasPendingPlayChange = false;
            hasUnloadedPlayDevJsonChange = false;
            if (shouldReloadPendingDiskState)
            {
                ReloadFromDisk(false);
                return true;
            }

            return changed;
        }

        return false;
    }

    internal bool Apply()
    {
        lastWriteResult = null;
        ValidateDraft();
        if (!CanApply)
        {
            return false;
        }

        return WriteDraftToDisk();
    }

    internal bool WriteDraft()
    {
        lastWriteResult = null;
        ValidateDraft();
        if (!CanWriteDraft)
        {
            return false;
        }

        return WriteDraftToDisk();
    }

    internal GCDevJsonIssue[] GetDisplayIssues()
    {
        if (lastWriteResult != null && !lastWriteResult.success)
        {
            return GetIssues(lastWriteResult.validation);
        }

        if (Draft != null)
        {
            return GetIssues(draftValidation);
        }

        return CombineIssues(
            devReadResult != null ? devReadResult.validation : null,
            metadataReadResult != null ? metadataReadResult.validation : null
        );
    }

    private void ReloadFromDisk(bool preservePendingPlayChange)
    {
        var pendingPlayChange = preservePendingPlayChange && hasPendingPlayChange;
        lastWriteResult = null;
        hasConflict = false;
        hasPendingPlayChange = pendingPlayChange;
        hasUnloadedPlayDevJsonChange = false;
        metadataReadResult = metadataStore.Read();
        devReadResult = devStore.Read(metadataReadResult);
        cleanData = devReadResult != null && devReadResult.data != null ? devReadResult.data.Clone() : null;
        Draft = cleanData != null ? GCDevJsonDraft.FromFile(cleanData) : null;
        ValidateDraft();
        UpdateFileStamps();
    }

    internal void NotifyDraftChanged()
    {
        lastWriteResult = null;
        if (hasUnloadedPlayDevJsonChange)
        {
            hasConflict = true;
        }

        ValidateDraft();
    }

    private bool WriteDraftToDisk()
    {
        lastWriteResult = devStore.Write(Draft.ToFile(), metadataReadResult);
        if (lastWriteResult != null && lastWriteResult.success)
        {
            if (EditorApplication.isPlaying)
            {
                hasPendingPlayChange = true;
            }

            ReloadFromDisk(hasPendingPlayChange);
            return true;
        }

        return false;
    }

    private void RefreshMetadata()
    {
        lastWriteResult = null;
        metadataReadResult = metadataStore.Read();
        if (Draft == null)
        {
            devReadResult = devStore.Read(metadataReadResult);
            cleanData = devReadResult != null && devReadResult.data != null ? devReadResult.data.Clone() : null;
            Draft = cleanData != null ? GCDevJsonDraft.FromFile(cleanData) : null;
        }

        ValidateDraft();
    }

    private void EnterConflict()
    {
        hasConflict = true;
        lastWriteResult = null;
        ValidateDraft();
    }

    private void UpdateFileStamps()
    {
        metadataFileStamp = metadataStore.ReadFileStamp();
        devFileStamp = devStore.ReadFileStamp();
    }

    private void ValidateDraft()
    {
        if (Draft == null)
        {
            draftValidation = devReadResult != null ? devReadResult.validation : null;
            return;
        }

        draftValidation = GCDevJsonValidation.ValidateData(Draft.ToFile(), DevJsonPath, metadataReadResult);
    }

    private static GCDevJsonIssue[] GetIssues(GCDevJsonValidationResult validation)
    {
        return validation != null && validation.issues != null ? validation.issues : new GCDevJsonIssue[0];
    }

    private static GCDevJsonIssue[] CombineIssues(GCDevJsonValidationResult first, GCDevJsonValidationResult second)
    {
        var firstIssues = GetIssues(first);
        var secondIssues = GetIssues(second);
        if (firstIssues.Length == 0)
        {
            return secondIssues;
        }

        if (secondIssues.Length == 0)
        {
            return firstIssues;
        }

        var combined = new GCDevJsonIssue[firstIssues.Length + secondIssues.Length];
        Array.Copy(firstIssues, combined, firstIssues.Length);
        Array.Copy(secondIssues, 0, combined, firstIssues.Length, secondIssues.Length);
        return combined;
    }

    private static bool AreEqual(GCDevJsonFile left, GCDevJsonFile right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        if (left.devVersion != right.devVersion ||
            !string.Equals(left.entryKey, right.entryKey, StringComparison.Ordinal) ||
            !string.Equals(left.seed, right.seed, StringComparison.Ordinal))
        {
            return false;
        }

        if (left.seats == null || right.seats == null || left.seats.Length != right.seats.Length)
        {
            return left.seats == right.seats;
        }

        for (var index = 0; index < left.seats.Length; index++)
        {
            if (!AreEqual(left.seats[index], right.seats[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEqual(GCDevJsonSeat left, GCDevJsonSeat right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        return string.Equals(left.name, right.name, StringComparison.Ordinal) &&
               left.enabled == right.enabled &&
               left.isBot == right.isBot;
    }
}

internal sealed class GCDevJsonDraft
{
    internal string entryKey;
    internal bool usesRandomSeed;
    internal int fixedSeed;
    internal GCDevJsonSeatDraft[] seats;

    private GCDevJsonDraft()
    {
    }

    internal static GCDevJsonDraft FromFile(GCDevJsonFile file)
    {
        var draft = new GCDevJsonDraft
        {
            entryKey = file.entryKey,
            usesRandomSeed = file.seed == GCDevJsonFile.RandomSeed,
            fixedSeed = GCDevJsonFile.MinSeed,
            seats = new GCDevJsonSeatDraft[GCDevJsonFile.SeatCount],
        };

        if (!draft.usesRandomSeed)
        {
            int parsedSeed;
            if (int.TryParse(file.seed, NumberStyles.None, CultureInfo.InvariantCulture, out parsedSeed))
            {
                draft.fixedSeed = parsedSeed;
            }
        }

        for (var index = 0; index < draft.seats.Length; index++)
        {
            var sourceSeat = file.seats[index];
            draft.seats[index] = new GCDevJsonSeatDraft
            {
                name = sourceSeat.name,
                enabled = sourceSeat.enabled,
                isBot = sourceSeat.isBot,
            };
        }

        return draft;
    }

    internal GCDevJsonFile ToFile()
    {
        var fileSeats = new GCDevJsonSeat[seats.Length];
        for (var index = 0; index < seats.Length; index++)
        {
            var seat = seats[index];
            fileSeats[index] = new GCDevJsonSeat(seat.name, seat.enabled, seat.isBot);
        }

        var seed = usesRandomSeed
            ? GCDevJsonFile.RandomSeed
            : fixedSeed.ToString(CultureInfo.InvariantCulture);

        return new GCDevJsonFile(entryKey, seed, fileSeats);
    }
}

internal sealed class GCDevJsonSeatDraft
{
    internal string name;
    internal bool enabled;
    internal bool isBot;
}

internal sealed class GCDevJsonStore
{
    private readonly IGCLocalProjectRootResolver projectRootResolver;

    internal GCDevJsonStore()
        : this(new GCUnityLocalProjectRootResolver())
    {
    }

    internal GCDevJsonStore(IGCLocalProjectRootResolver projectRootResolver)
    {
        if (projectRootResolver == null)
        {
            throw new ArgumentNullException(nameof(projectRootResolver));
        }

        this.projectRootResolver = projectRootResolver;
    }

    internal string ResolveFilePath()
    {
        return Path.Combine(projectRootResolver.ResolveProjectRootPath(), GCDevJsonFile.FileName);
    }

    internal GCRootJsonFileStamp ReadFileStamp()
    {
        return GCRootJsonFileStamp.Read(ResolveFilePath());
    }

    internal GCDevJsonReadResult Read()
    {
        return Read(ReadMetadata());
    }

    internal GCDevJsonReadResult Read(GCMetadataJsonReadResult metadataReadResult)
    {
        var path = ResolveFilePath();
        return GCDevJsonValidation.BuildReadResult(ReadParsedFile(path), metadataReadResult);
    }

    internal GCDevJsonWriteResult Write(GCDevJsonFile data)
    {
        return Write(data, ReadMetadata());
    }

    internal GCDevJsonWriteResult Write(GCDevJsonFile data, GCMetadataJsonReadResult metadataReadResult)
    {
        var path = ResolveFilePath();
        if (data == null)
        {
            return GCDevJsonWriteResult.Failed(
                path,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json write data is missing.", path))
            );
        }

        var dataValidation = GCDevJsonValidation.ValidateData(data, path, metadataReadResult);
        if (!dataValidation.IsValid)
        {
            return GCDevJsonWriteResult.Failed(path, dataValidation);
        }

        if (!File.Exists(path))
        {
            return GCDevJsonWriteResult.Failed(
                path,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.MissingFile, "gc.dev.json was not found at " + path + ".", path))
            );
        }

        JObject jsonObject;
        try
        {
            var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
            jsonObject = token as JObject;
            if (jsonObject == null)
            {
                return GCDevJsonWriteResult.Failed(
                    path,
                    GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidRoot, "gc.dev.json must be a JSON object.", path))
                );
            }
        }
        catch (JsonException exception)
        {
            return GCDevJsonWriteResult.Failed(
                path,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.InvalidJson, "gc.dev.json is not valid JSON: " + exception.Message, path))
            );
        }
        catch (Exception exception)
        {
            return GCDevJsonWriteResult.Failed(
                path,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json could not be read before write: " + exception.Message, path))
            );
        }

        jsonObject["devVersion"] = data.devVersion;
        jsonObject["entryKey"] = data.entryKey;
        jsonObject["seed"] = data.seed;
        jsonObject["seats"] = BuildSeatsArray(data.seats);

        try
        {
            File.WriteAllText(path, jsonObject.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            return GCDevJsonWriteResult.Failed(
                path,
                GCDevJsonValidationResult.FromIssue(GCDevJsonIssue.Error(GCDevJsonIssueCode.WriteError, "gc.dev.json could not be written: " + exception.Message, path))
            );
        }

        return GCDevJsonWriteResult.Succeeded(path, dataValidation);
    }

    private GCMetadataJsonReadResult ReadMetadata()
    {
        return new GCMetadataJsonStore(projectRootResolver).Read();
    }

    private static GCDevJsonParsedFile ReadParsedFile(string path)
    {
        if (!File.Exists(path))
        {
            return GCDevJsonParsedFile.Missing(path);
        }

        try
        {
            var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
            var jsonObject = token as JObject;
            if (jsonObject == null)
            {
                return GCDevJsonParsedFile.InvalidRoot(path);
            }

            return GCDevJsonParsedFile.Parsed(path, jsonObject);
        }
        catch (JsonException exception)
        {
            return GCDevJsonParsedFile.InvalidJson(path, "gc.dev.json is not valid JSON: " + exception.Message);
        }
        catch (Exception exception)
        {
            return GCDevJsonParsedFile.ReadError(path, "gc.dev.json could not be read: " + exception.Message);
        }
    }

    private static JArray BuildSeatsArray(GCDevJsonSeat[] seats)
    {
        var seatsArray = new JArray();
        for (var index = 0; index < seats.Length; index++)
        {
            seatsArray.Add(new JObject
            {
                ["name"] = seats[index].name,
                ["enabled"] = seats[index].enabled,
                ["isBot"] = seats[index].isBot,
            });
        }

        return seatsArray;
    }
}

internal sealed class GCDevJsonWriteResult
{
    internal readonly string path;
    internal readonly bool success;
    internal readonly GCDevJsonValidationResult validation;

    private GCDevJsonWriteResult(string path, bool success, GCDevJsonValidationResult validation)
    {
        this.path = path;
        this.success = success;
        this.validation = validation;
    }

    internal static GCDevJsonWriteResult Succeeded(string path)
    {
        return new GCDevJsonWriteResult(path, true, GCDevJsonValidationResult.Valid());
    }

    internal static GCDevJsonWriteResult Succeeded(string path, GCDevJsonValidationResult validation)
    {
        return new GCDevJsonWriteResult(path, true, validation ?? GCDevJsonValidationResult.Valid());
    }

    internal static GCDevJsonWriteResult Failed(string path, GCDevJsonValidationResult validation)
    {
        return new GCDevJsonWriteResult(path, false, validation);
    }
}

internal sealed class GCMetadataJsonStore
{
    private readonly IGCLocalProjectRootResolver projectRootResolver;

    internal GCMetadataJsonStore()
        : this(new GCUnityLocalProjectRootResolver())
    {
    }

    internal GCMetadataJsonStore(IGCLocalProjectRootResolver projectRootResolver)
    {
        if (projectRootResolver == null)
        {
            throw new ArgumentNullException(nameof(projectRootResolver));
        }

        this.projectRootResolver = projectRootResolver;
    }

    internal string ResolveFilePath()
    {
        return Path.Combine(projectRootResolver.ResolveProjectRootPath(), GCMetadataJsonFile.FileName);
    }

    internal GCRootJsonFileStamp ReadFileStamp()
    {
        return GCRootJsonFileStamp.Read(ResolveFilePath());
    }

    internal GCMetadataJsonReadResult Read()
    {
        return GCMetadataJsonValidation.BuildReadResult(ReadParsedFile(ResolveFilePath()));
    }

    private static GCMetadataJsonParsedFile ReadParsedFile(string path)
    {
        if (!File.Exists(path))
        {
            return GCMetadataJsonParsedFile.Missing(path);
        }

        try
        {
            var token = JToken.Parse(File.ReadAllText(path, Encoding.UTF8));
            var jsonObject = token as JObject;
            if (jsonObject == null)
            {
                return GCMetadataJsonParsedFile.InvalidRoot(path);
            }

            return GCMetadataJsonParsedFile.Parsed(path, jsonObject);
        }
        catch (JsonException exception)
        {
            return GCMetadataJsonParsedFile.InvalidJson(path, "gc.metadata.json is not valid JSON: " + exception.Message);
        }
        catch (Exception exception)
        {
            return GCMetadataJsonParsedFile.ReadError(path, "gc.metadata.json could not be read: " + exception.Message);
        }
    }
}

internal struct GCRootJsonFileStamp
{
    internal readonly string path;
    internal readonly bool exists;
    internal readonly long lastWriteTimeUtcTicks;
    internal readonly long length;
    internal readonly string contentHash;
    internal readonly string readError;

    private GCRootJsonFileStamp(
        string path,
        bool exists,
        long lastWriteTimeUtcTicks,
        long length,
        string contentHash,
        string readError
    )
    {
        this.path = path;
        this.exists = exists;
        this.lastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
        this.length = length;
        this.contentHash = contentHash;
        this.readError = readError;
    }

    internal static GCRootJsonFileStamp Read(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return new GCRootJsonFileStamp(path, false, 0, 0, null, "Path is missing.");
        }

        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return new GCRootJsonFileStamp(path, false, 0, 0, null, null);
            }

            var bytes = File.ReadAllBytes(path);
            return new GCRootJsonFileStamp(
                path,
                true,
                fileInfo.LastWriteTimeUtc.Ticks,
                bytes.LongLength,
                ComputeHash(bytes),
                null
            );
        }
        catch (Exception exception)
        {
            return new GCRootJsonFileStamp(
                path,
                true,
                0,
                -1,
                null,
                exception.GetType().Name + ": " + exception.Message
            );
        }
    }

    internal bool IsSameAs(GCRootJsonFileStamp other)
    {
        return string.Equals(path, other.path, StringComparison.Ordinal) &&
               exists == other.exists &&
               lastWriteTimeUtcTicks == other.lastWriteTimeUtcTicks &&
               length == other.length &&
               string.Equals(contentHash, other.contentHash, StringComparison.Ordinal) &&
               string.Equals(readError, other.readError, StringComparison.Ordinal);
    }

    private static string ComputeHash(byte[] bytes)
    {
        using (var sha256 = SHA256.Create())
        {
            return Convert.ToBase64String(sha256.ComputeHash(bytes));
        }
    }
}
