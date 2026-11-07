namespace Daoris.Knowledge;

/// <summary>
/// Why a unit stays, or a piece of it is kept: the refusal, its sentence, and what it names. The driver reads
/// the refusal's word and the facts beside it, never the sentence, as it reads a session delete's.
/// </summary>
/// <param name="Message">The sentence a person reads, phrased once here: the piece, and the act that would free it.</param>
public sealed record HistoryKept(HistoryRefusal Refusal, string Message)
{
    public string? Quest { get; init; }

    public string? Ask { get; init; }

    public string? Session { get; init; }

    /// <summary>The machine a teammate's record ran on, where that is what keeps it.</summary>
    public string? Origin { get; init; }

    /// <summary>The workspace whose remote has yet to take a move or a record of it.</summary>
    public string? Workspace { get; init; }

    /// <summary>What waits on the person, for <see cref="HistoryRefusal.NeedsYou"/> only; null for every other word.</summary>
    public HistoryWaiting? Waits { get; init; }

    /// <summary>How the work in progress stands, for <see cref="HistoryRefusal.Open"/> only; null for every other word.</summary>
    public HistoryStands? Stands { get; init; }

    /// <summary>The open work that names the unit, for <see cref="HistoryRefusal.Awaited"/> only; null for every other word.</summary>
    public HistoryAwaitedBy? By { get; init; }

    // Every word below is held, with the driver's constants, to one table: the service tests' fixtures/history-words.json.

    /// <summary>The refusal's word on the wire, kebab-case as every word there: <c>needs-you</c>, <c>not-ours</c>.</summary>
    public static string Spell(HistoryRefusal refusal) => refusal switch
    {
        HistoryRefusal.NeedsYou => "needs-you",
        HistoryRefusal.NotOurs => "not-ours",
        _ => refusal.ToString().ToLowerInvariant(),
    };

    /// <summary>What waits, as the wire's <c>waits</c> says it: <c>parked</c>, <c>held</c>, <c>conflict</c>, <c>ask</c>, <c>proposal</c>.</summary>
    public static string Spell(HistoryWaiting waiting) => waiting.ToString().ToLowerInvariant();

    /// <summary>How the work stands, as the wire's <c>stands</c> says it: <c>open</c>, <c>taken</c>.</summary>
    public static string Spell(HistoryStands stands) => stands.ToString().ToLowerInvariant();

    /// <summary>The open work naming it, as the wire's <c>by</c> says it: <c>question</c>, <c>asker</c>, <c>step</c>.</summary>
    public static string Spell(HistoryAwaitedBy by) => by.ToString().ToLowerInvariant();
}
