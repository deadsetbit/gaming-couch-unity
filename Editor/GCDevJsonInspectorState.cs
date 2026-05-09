using System;
using System.Globalization;
using DSB.GC.Dev;

internal sealed class GCDevJsonInspectorState
{
    private readonly GCDevJsonStore devStore;
    private readonly GCMetadataJsonStore metadataStore;

    private GCDevJsonFile cleanData;
    private GCDevJsonReadResult devReadResult;
    private GCMetadataJsonReadResult metadataReadResult;
    private GCDevJsonValidationResult draftValidation;
    private GCDevJsonWriteResult lastWriteResult;

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

    internal bool CanApply
    {
        get { return Draft != null && IsDirty && draftValidation != null && draftValidation.IsValid; }
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
        lastWriteResult = null;
        metadataReadResult = metadataStore.Read();
        devReadResult = devStore.Read(metadataReadResult);
        cleanData = devReadResult != null && devReadResult.data != null ? devReadResult.data.Clone() : null;
        Draft = cleanData != null ? GCDevJsonDraft.FromFile(cleanData) : null;
        ValidateDraft();
    }

    internal void NotifyDraftChanged()
    {
        lastWriteResult = null;
        ValidateDraft();
    }

    internal bool Apply()
    {
        lastWriteResult = null;
        ValidateDraft();
        if (!CanApply)
        {
            return false;
        }

        lastWriteResult = devStore.Write(Draft.ToFile(), metadataReadResult);
        if (lastWriteResult != null && lastWriteResult.success)
        {
            Reload();
            return true;
        }

        return false;
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
