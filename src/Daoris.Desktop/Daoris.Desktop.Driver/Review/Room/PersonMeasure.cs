namespace Daoris.Driver;

/// <summary>
/// One reading of the machine as the room keeps it: what the machine holds in use, what Daoris's own processes hold of it, and
/// how busy the processors were since the sample before (0 to 1, null on the first).
/// </summary>
public sealed record RoomSample(DateTimeOffset At, long Used, long Daoris, double? Busy)
{
    /// <summary>The person's share: the machine's use outside Daoris's processes.</summary>
    public long Person => Math.Max(0, Used - Daoris);
}

/// <summary>What the room reads of the person's own work from the samples: their share now, how far it has stood above it, and what that keeps free.</summary>
/// <param name="Person">The person's share in the newest sample, bytes.</param>
/// <param name="Swing">How far their share stood above its present at the highest in the window, bytes.</param>
/// <param name="Margin">The safety margin kept above the swing, bytes.</param>
public sealed record PersonRoom(long Person, long Swing, long Margin)
{
    /// <summary>What the person's own work may take back: its swing and the margin.</summary>
    public long Headroom => Swing + Margin;
}

/// <summary>The measured share of the machine that is the person's, with its headroom from their recent swings.</summary>
public static class PersonMeasure
{
    /// <summary>How far back a swing is read.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>The least apart two samples are taken, so many waiting runs share one series.</summary>
    public static readonly TimeSpan SampleEvery = TimeSpan.FromSeconds(5);

    /// <summary>The span the CPU's busy is averaged over.</summary>
    public static readonly TimeSpan CpuSpan = TimeSpan.FromSeconds(10);

    /// <summary>Busy at or above this is saturated: nine in ten.</summary>
    public const double Saturated = 0.9;

    /// <summary>The margin above the swing: 2 % of the machine's memory, 1 GB at least.</summary>
    public static long MarginOf(long? total) => Math.Max(1L << 30, (total ?? 0) / 50);

    /// <summary>The person's room from <paramref name="samples"/> at <paramref name="now"/>: only the samples inside the window count, none is a margin alone.</summary>
    public static PersonRoom Person(IReadOnlyList<RoomSample> samples, DateTimeOffset now, long? total)
    {
        var recent = samples.Where(sample => now - sample.At <= Window).OrderBy(sample => sample.At).ToList();
        var margin = MarginOf(total);
        if (recent.Count == 0) return new PersonRoom(0, 0, margin);
        var current = recent[^1].Person;
        return new PersonRoom(current, Math.Max(0, recent.Max(sample => sample.Person) - current), margin);
    }

    /// <summary>The processors' busy over the last <see cref="CpuSpan"/>, or null where no sample in it says.</summary>
    public static double? Busy(IReadOnlyList<RoomSample> samples, DateTimeOffset now)
    {
        var busy = samples.Where(sample => now - sample.At <= CpuSpan && sample.Busy is not null).Select(sample => sample.Busy!.Value).ToList();
        return busy.Count == 0 ? null : busy.Average();
    }

    /// <summary>The samples kept: the window's, with <paramref name="added"/> after them where it is far enough from the newest.</summary>
    public static IReadOnlyList<RoomSample> Add(IReadOnlyList<RoomSample> samples, RoomSample? added, DateTimeOffset now)
    {
        var kept = samples.Where(sample => now - sample.At <= Window).OrderBy(sample => sample.At).ToList();
        if (added is not null && (kept.Count == 0 || added.At - kept[^1].At >= SampleEvery)) kept.Add(added);
        return kept;
    }
}

/// <summary>What the concurrency limit stands at: its count, when it last changed, when pressure last showed and which signal set it.</summary>
public sealed record RoomLimit(int Limit, DateTimeOffset ChangedAt, DateTimeOffset PressureAt, string Signal)
{
    public const string Start = RoomSignals.Start;
    public const string Memory = RoomSignals.Memory;
    public const string Cpu = RoomSignals.Cpu;
    public const string Timeouts = RoomSignals.Timeouts;
    public const string Clear = RoomSignals.Clear;

    /// <summary>A limit that has not been told anything: the ceiling.</summary>
    public static RoomLimit Initial(int ceiling) => new(ceiling, DateTimeOffset.MinValue, DateTimeOffset.MinValue, Start);
}

/// <summary>
/// The concurrency's backing off and growing back: additive increase, multiplicative decrease. A pressure signal (free
/// memory under what is kept, the processors saturated, a run whose tests timed out) halves the limit, one at least and at most once in
/// <see cref="Cool"/>; <see cref="Calm"/> with none adds one, up to the ceiling.
/// </summary>
public static class RoomPressure
{
    public static readonly TimeSpan Cool = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan Calm = TimeSpan.FromSeconds(60);

    /// <summary>The limit after one look at <paramref name="now"/>, <paramref name="signal"/> the name of the pressure seen or null.</summary>
    public static RoomLimit Next(RoomLimit state, string? signal, int ceiling, DateTimeOffset now)
    {
        var limit = Math.Clamp(state.Limit, 1, Math.Max(1, ceiling));
        state = state with { Limit = limit };
        if (signal is not null)
        {
            state = state with { PressureAt = now };
            return now - state.ChangedAt >= Cool && limit > 1 ? state with { Limit = Math.Max(1, limit / 2), ChangedAt = now, Signal = signal } : state with { Signal = signal };
        }

        return now - state.PressureAt >= Calm && now - state.ChangedAt >= Calm && limit < ceiling
            ? state with { Limit = limit + 1, ChangedAt = now, Signal = RoomLimit.Clear }
            : state;
    }
}
