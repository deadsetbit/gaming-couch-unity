using System.Globalization;
using DSB.GC.Dev;

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
