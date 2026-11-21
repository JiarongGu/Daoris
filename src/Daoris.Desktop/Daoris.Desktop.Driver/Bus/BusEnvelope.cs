namespace Daoris.Driver;

/// <summary>
/// One entry of the bus's log: one typed, versioned envelope. Its payload carries ids, codes, counts and times only;
/// words, diffs and output stay in their records, referenced by id.
/// </summary>
/// <param name="Id">The entry's identity, minted at its append, never reused: what a later entry's <see cref="Causes"/> names.</param>
/// <param name="Seq">The log's order: given at the append by the lease holder, and seated after the store's newest by the write that keeps it.</param>
/// <param name="Kind">Its type and version, <c>name/version</c> (<c>check.started/1</c>): a fact, a request or a response.</param>
/// <param name="Subject">The key its order is kept by: an opinion, a run.</param>
/// <param name="Emitter">Who appended it (<c>driver/checks</c>).</param>
/// <param name="Answerer">Who must answer it, for a request; null for a fact.</param>
/// <param name="Deadline">By when it must be handled: its append and its type's deadline (the registry's alone); null where none is obliged.</param>
/// <param name="Effect">The key of the outside act it records, which runs once by it; null where it records none.</param>
/// <param name="Causes">The ids of the entries it follows from, where they are known; empty is not known, never none.</param>
/// <param name="Payload">Its small values: ids and codes (short strings), counts and times (numbers), flags, short lists of ids.</param>
/// <param name="At">The moment of its append.</param>
public sealed record BusEnvelope(
    string Id, long Seq, string Kind, string Subject, string Emitter, string? Answerer, DateTimeOffset? Deadline, string? Effect,
    IReadOnlyList<string> Causes, IReadOnlyList<(string Key, object? Value)> Payload, DateTimeOffset At);

/// <summary>What a writer hands the log to append (<see cref="BusLog.Append"/>): an envelope without the id, order and time the log gives it.</summary>
/// <param name="Type">Its type, as the registry declares it (<see cref="BusTypes"/>), whose kind and emitter it carries: one made elsewhere is refused.</param>
/// <param name="Subject">The key its order is kept by.</param>
public sealed record BusDraft(BusType Type, string Subject)
{
    public string Kind => Type.Kind;

    public string Emitter => Type.Emitter;

    public string? Answerer { get; init; }

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
    /// <summary>The log's store would not take a flush, for a reason no later try passes or busy past the tries; its entries are dropped.</summary>
    public const string Store = "store";

    /// <summary>An envelope whose subject, answerer, effect or causes are no ids; a payload's value is dropped instead (<see cref="BusDrops"/>).</summary>
    public const string Shape = "shape";

    /// <summary>A type the registry does not declare (<see cref="BusTypes"/>).</summary>
    public const string Unknown = "unknown-type";

    /// <summary>This process does not hold the home's lease, at the append or afresh before the write, so it is not the log's writer.</summary>
    public const string NotHolder = "not-holder";

    /// <summary>Appended after the log closed.</summary>
    public const string Closed = "closed";

    /// <summary>The append itself failed; the writer beside it is told nothing.</summary>
    public const string Append = "append";

    /// <summary>More waited unflushed than the log keeps in memory (<see cref="BusLog.MostPending"/>).</summary>
    public const string Full = "full";

    /// <summary>The writer's loop failed; the close wrote what waited.</summary>
    public const string Loop = "loop";
}
