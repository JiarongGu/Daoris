namespace Daoris.Driver;

/// <summary>What a delete did with the session asked of it (SESSUX1f, D126 §5.4).</summary>
public enum DeleteVerdict
{
    /// <summary>Its record and what this machine kept of it are gone.</summary>
    Deleted,

    /// <summary>No record here is this session.</summary>
    Unknown,

    /// <summary>A teammate's record (SYNC4): it ran on their machine, and the record is theirs.</summary>
    NotOurs,

    /// <summary>It still runs or waits: stopped first, it ends.</summary>
    Live,

    /// <summary>It served a quest, and is that work's record: archive is how it is cleared.</summary>
    ServedQuest,

    /// <summary>An ask names it as its intake, a quest was published by it, or a landing names it (<see cref="DeleteOutcome.NamedBy"/>).</summary>
    Named,

    /// <summary>Its own tree is still on this machine: discarded or cleaned up first.</summary>
    TreeHere,

    /// <summary>A workspace's remote holds it, and a session record does not travel as a deletion.</summary>
    OnRemote,
}

/// <summary>One delete's outcome, with the facts its refusal names.</summary>
/// <param name="Message">The sentence a terminal prints: the ledger's verbatim for the record's half, the driver's for this machine's.</param>
public sealed record DeleteOutcome(string Session, DeleteVerdict Verdict, string Message)
{
    /// <summary>The quest it served, or the one it published.</summary>
    public string? Quest { get; init; }

    /// <summary>The ask that names it as its intake.</summary>
    public string? Ask { get; init; }

    /// <summary>The machine a teammate's record ran on.</summary>
    public string? Machine { get; init; }

    /// <summary>The workspace whose remote holds it.</summary>
    public string? Workspace { get; init; }

    /// <summary>For <see cref="DeleteVerdict.Named"/>: <see cref="SessionDeletion.ByAsk"/>, <see cref="SessionDeletion.ByQuest"/> or <see cref="SessionDeletion.ByLanding"/>.</summary>
    public string? NamedBy { get; init; }

    /// <summary>
    /// What went, by name: <c>record</c>, then what <see cref="SessionHomeFiles"/> took (<c>conversation</c>, <c>transcript</c>,
    /// <c>files</c>, <c>harness</c>, <c>marker</c>, <c>mark</c>, <c>choice</c>, <c>spawn</c>, <c>help</c>, <c>held</c>, <c>landing</c>,
    /// <c>archived</c>), in that order.
    /// </summary>
    public IReadOnlyList<string> Removed { get; init; } = [];

    /// <summary>
    /// What the disk would not let go of, by the same names, in the same order (SESSDEL1): left over, and said in
    /// <see cref="Message"/>. Empty when everything went. The record's removal never waits on it: the ledger has already said yes.
    /// </summary>
    public IReadOnlyList<string> Stayed { get; init; } = [];
}
