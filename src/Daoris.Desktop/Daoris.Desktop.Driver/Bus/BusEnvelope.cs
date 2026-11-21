namespace Daoris.Driver;

/// <summary>
/// One entry of the bus's log: one typed, versioned envelope. Its payload carries ids, codes, counts and times only;
/// words, diffs and output stay in their records, referenced by id.
/// </summary>
/// <param name="Id">The entry's identity, minted at its append, never reused: what a later entry's <see cref="Causes"/> names.</param>
/// <param name="Seq">The log's order: one more than the entry before it, given at the append by the lease holder, the log's one writer.</param>
/// <param name="Kind">Its type and version, <c>name/version</c> (<c>check.started/1</c>): a fact, a request or a response.</param>
/// <param name="Subject">The key its order is kept by: an opinion, a run.</param>
/// <param name="Emitter">Who appended it (<c>driver/checks</c>).</param>
/// <param name="Answerer">Who must answer it, for a request; null for a fact.</param>
/// <param name="Deadline">By when it must be answered or handled; null where its type names none yet.</param>
/// <param name="Effect">The key of the outside act it records, which runs once by it; null where it records none.</param>
/// <param name="Causes">The ids of the entries it follows from, where they are known; empty is not known, never none.</param>
/// <param name="Payload">Its small values: ids and codes (short strings), counts and times (numbers), flags, short lists of ids.</param>
/// <param name="At">The moment of its append.</param>
public sealed record BusEnvelope(
    string Id, long Seq, string Kind, string Subject, string Emitter, string? Answerer, DateTimeOffset? Deadline, string? Effect,
    IReadOnlyList<string> Causes, IReadOnlyList<(string Key, object? Value)> Payload, DateTimeOffset At);

/// <summary>What a writer hands the log to append (<see cref="BusLog.Append"/>): an envelope without the id, order and time the log gives it.</summary>
/// <param name="Kind">Its type and version, <c>name/version</c>.</param>
/// <param name="Subject">The key its order is kept by.</param>
/// <param name="Emitter">Who appends it.</param>
public sealed record BusDraft(string Kind, string Subject, string Emitter)
{
    public string? Answerer { get; init; }

    public DateTimeOffset? Deadline { get; init; }

    public string? Effect { get; init; }

    public IReadOnlyList<string> Causes { get; init; } = [];

    public IReadOnlyList<(string Key, object? Value)> Payload { get; init; } = [];

    /// <summary>
    /// Of the commit class (a settle, a give-up, an acknowledgement the system must not lose): flushed at once, grouped with whatever
    /// else is due, rather than written behind.
    /// </summary>
    public bool Commit { get; init; }

    /// <summary>How long the handling it records took (a check's run, a pass from its ask), measured by its kind; null where none is known.</summary>
    public TimeSpan? Took { get; init; }

    /// <summary>The key a later entry names this one by (<see cref="After"/>), held in memory by the log for its process's life.</summary>
    public string? Key { get; init; }

    /// <summary>The keys of the entries it follows from, the first this log knows becoming a cause; none known adds no cause.</summary>
    public IReadOnlyList<string> After { get; init; } = [];
}

/// <summary>The reasons a shadow append is refused, each counted by the day and said once in the machine log.</summary>
public static class BusMisses
{
    /// <summary>The log's store would not take a flush; its entries are dropped, never retried into a later order.</summary>
    public const string Store = "store";

    /// <summary>A payload that is no id, code, count or time, or an envelope missing its kind, subject or emitter.</summary>
    public const string Shape = "shape";

    /// <summary>This process does not hold the home's lease, so it is not the log's writer.</summary>
    public const string NotHolder = "not-holder";

    /// <summary>Appended after the log closed.</summary>
    public const string Closed = "closed";

    /// <summary>The append itself failed; the writer beside it is told nothing.</summary>
    public const string Append = "append";

    /// <summary>More waited unflushed than the log keeps in memory (<see cref="BusLog.MostPending"/>).</summary>
    public const string Full = "full";
}
