namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a run is observed by (the agent-lyntai design §6.3 point 3), listed once by what the native capture hands each line to and what a
/// run does beside its lines, so no case chooses its own. A channel neither path wrote is equal by being empty on both.
/// </summary>
internal enum ParityChannel
{
    /// <summary>The lineage hook each line is handed.</summary>
    Lineage,

    /// <summary>The harness's own words the ending reads.</summary>
    HarnessWords,

    /// <summary>The streams beside the session: its subagents and background work.</summary>
    Streams,

    /// <summary>The conversation the harness named.</summary>
    Conversation,

    /// <summary>The transcript's lines, in order.</summary>
    Transcript,

    /// <summary>The session's events, in order.</summary>
    Events,

    /// <summary>The account's window readings.</summary>
    Windows,

    /// <summary>The live console.</summary>
    Console,

    /// <summary>The spawn, flag by flag, as its program received it.</summary>
    Spawn,

    /// <summary>What is written on stdin, and when.</summary>
    Stdin,

    /// <summary>The downloads handed.</summary>
    Downloads,

    /// <summary>The watch's hearing stamps.</summary>
    Hearing,

    /// <summary>The machine log's lines.</summary>
    MachineLog,

    /// <summary>The service's account lines.</summary>
    AccountLines,

    /// <summary>The launcher's own arguments beside the spawn: its kind, class, home and log, which containment reads.</summary>
    Launch,

    /// <summary>The records written: the session's record, the binding, cooling, the weekly windows, offers and the usage history.</summary>
    Records,

    /// <summary>The failure as <c>AgentError</c> reads it: kind, phase, access disposition, limit with its end, window, provenance, delivery.</summary>
    Failure,

    /// <summary>The end: an exit, a stop or a conclusion.</summary>
    End,
}

/// <summary>One thing a path observed: its channel, what it is, and its fields in the order they were read.</summary>
internal sealed record Observed(ParityChannel Channel, string What, IReadOnlyList<(string Name, string? Value)> Fields)
{
    /// <summary>The value of <paramref name="name"/>; null where the entry has none.</summary>
    public string? this[string name] => Fields.FirstOrDefault(field => field.Name == name).Value;

    public override string ToString() => $"{Channel} {What}({string.Join(", ", Fields.Select(field => $"{field.Name}={field.Value ?? "null"}"))})";
}

/// <summary>Everything one path observed of one run, in the order it is written, and the run's start, which a time is measured from.</summary>
internal sealed class ParityObservation(string path, DateTimeOffset start)
{
    private readonly List<Observed> _seen = [];

    /// <summary>The path's name, as a difference names it.</summary>
    public string Path => path;

    public DateTimeOffset Start => start;

    public IReadOnlyList<Observed> Seen
    {
        get
        {
            lock (_seen) return [.. _seen];
        }
    }

    public void Add(ParityChannel channel, string what, params (string Name, string? Value)[] fields)
    {
        lock (_seen) _seen.Add(new Observed(channel, what, fields));
    }

    /// <summary>The same observation with <paramref name="change"/> applied to what it saw, for a planted regression.</summary>
    public ParityObservation With(Func<IReadOnlyList<Observed>, IEnumerable<Observed>> change)
    {
        var changed = new ParityObservation(path, start);
        foreach (var each in change(Seen)) changed._seen.Add(each);
        return changed;
    }
}
