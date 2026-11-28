using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>How a stop's check is placed: the least it is held to, and how it waits for its place in the machine's admission (a test hands its fast one).</summary>
/// <param name="Floor">The least a stop is held to, and all it is held to while it waits for its place.</param>
/// <param name="Waiting">How the admission looks again, or null for the machine's own cadence.</param>
/// <param name="Capacity">How many heavy runs the machine admits at once, or null for this machine's own.</param>
public sealed record CheckPlacing(TimeSpan Floor, AdmissionWaiting? Waiting = null, int? Capacity = null)
{
    /// <summary>The machine's own: the floor of <see cref="StopBounds"/> and the admission's own cadence and capacity.</summary>
    public static CheckPlacing Machine { get; } = new(StopBounds.Floor);
}

/// <summary>
/// One stop's place in the machine's admission, and the bound it is held to from there. The check copies the home and starts the staged build,
/// which are heavy runs: it takes one place as every heavy run does, so it does not compete with them. Waiting for the place is the machine's
/// and not the check's, so it is held only to the floor; the learned bound (<see cref="StopBounds"/>) counts from the moment the place is taken.
/// </summary>
internal sealed class StopPlace(string home, DateTimeOffset began, CheckPlacing placing)
{
    private readonly object _gate = new();
    private DateTimeOffset? _placed;

    /// <summary>The bound the machine's history says, read as the stop began.</summary>
    public StopBound Bound { get; } = StopBounds.Read(home, placing.Floor);

    /// <summary>What the stop is held to now, and since when.</summary>
    public (DateTimeOffset Since, TimeSpan Limit) Held()
    {
        lock (_gate) return _placed is { } placed ? (placed, Bound.Bound) : (began, placing.Floor);
    }

    /// <summary>
    /// Run <paramref name="check"/> in a place of the admission, held until it has ended (a stop ended meanwhile unwinding included): <paramref name="waits"/>
    /// is said while the stop waits for the place, <paramref name="placed"/> once it has it.
    /// </summary>
    public async Task<T> RunAsync<T>(Func<DateTimeOffset> now, Action waits, Action placed, Func<T> check, CancellationToken ct)
    {
        using var hold = await MachineAdmission.TakeAsync(
            home, new AdmissionRequest("the check of the staged build"), new Said(waits), ct, placing.Waiting, placing.Capacity).ConfigureAwait(false);
        lock (_gate) _placed = now();
        placed();
        return await Task.Run(check, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>What the stop waits on at <paramref name="now"/>, for how long, and the bound it is held to.</summary>
    public UpdateWaiting Waiting(string on, DateTimeOffset now)
    {
        var (since, limit) = Held();
        return new UpdateWaiting(on, (int)Math.Max(0, (now - since).TotalSeconds), (int)limit.TotalSeconds);
    }

    /// <summary>The stop ended on its own: its time from the place is kept for the bound the next one is held to.</summary>
    public void Measure(DateTimeOffset now)
    {
        DateTimeOffset? placed;
        lock (_gate) placed = _placed;
        if (placed is { } since) StopBounds.Keep(home, now, now - since);
    }

    /// <summary>The refusal's sentence for a stop that outlasted its bound while it waits on <paramref name="on"/>.</summary>
    public string Words(string on, bool forPlace)
    {
        var basis = forPlace ? "" : $" ({Bound.Basis})";
        return $"the application waited {(int)Held().Limit.TotalSeconds} seconds on {on} and gave up{basis}, so it stays open and nothing was replaced.";
    }

    /// <summary>The admission speaks only while it makes the run wait: any line of it means the stop waits for its place.</summary>
    private sealed class Said(Action waits) : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void Write(char value)
        {
        }

        public override void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value)) waits();
        }

        public override void WriteLine(string? value) => Write(value);
    }
}
