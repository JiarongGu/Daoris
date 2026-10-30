using System.Collections.Frozen;

namespace Daoris.Driver;

/// <summary>
/// One row of the service's event journal as the driver reads it (FOUNDATION1 l-i, the foundation design §4.1): the raw transition and the
/// snapshot its coding needs, the lists the triggers comma-joined already split. Never a text.
/// </summary>
public sealed record JournalEntry(
    long Position, long At, string Record, string? Family, string Change, string? StateOld, string? StateNew, bool? LimitedOld, bool? LimitedNew,
    IReadOnlyList<string>? NoteCodes, IReadOnlyList<string> Marks, string? Workspace, string? Quest);

/// <summary>
/// A part that ended (§4.2's <c>part.ended</c>), judged from its journal row alone: the record, the state it took, the note's <c>ended.*</c>
/// code as its reason and its class, one closed code. Keyed by the store and the position, the occurrence's one key (§4.5).
/// </summary>
/// <param name="Reason">The note's first <c>ended.*</c> code, or null where it carried none.</param>
/// <param name="Conversation">Whether the record is a conversation, a chat: the one filter of <c>SESSION_ENDED</c>.</param>
public sealed record PartEnded(
    long Position, string Store, string Session, string State, string? Reason, string Class, bool Conversation, string? Workspace, DateTimeOffset At, bool Limited);

/// <summary>
/// The one judge of an end (FOUNDATION1 l-i, the foundation design §4.2): <c>part.ended</c>, its reason and class, from a journal row's state, its
/// limit flag and its note's codes, and the table that says who reads which end. Four meanings of <i>ended</i> become one event and four
/// filters (<see cref="EndReaders"/>).
/// </summary>
public static class PartEnds
{
    /// <summary>The event's code.</summary>
    public const string Code = "part.ended";

    /// <summary>The ledger's closed states, compared without case or hyphens (the journal spells them as the record does).</summary>
    private static readonly FrozenSet<string> Closed = new[] { "completed", "declined", "stooddown", "failed", "stopped" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The closed class codes of §4.2.</summary>
    public static readonly IReadOnlyList<string> Classes =
    [
        "completed", "failed", "limited", "transient-bound", "access", "stopped-person", "stopped-role", "stopped-daoris", "refused-choice",
        "awaits-helper", "stood-down", "unknown",
    ];

    /// <summary>The <c>ended.*</c> codes that decide a class whatever the state is.</summary>
    public static readonly IReadOnlyDictionary<string, string> Decided = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ended.choice-not-offered"] = "refused-choice",
        ["ended.transient"] = "transient-bound",
        ["ended.awaits-helper"] = "awaits-helper",
        ["ended.stood-down"] = "stood-down",
        ["ended.stopped"] = "stopped-person",
        ["ended.quiet-pressed"] = "stopped-person",
        ["ended.stopped-steward"] = "stopped-role",
        ["ended.driver-closed"] = "stopped-daoris",
        ["ended.setup-changed"] = "stopped-daoris",
        ["ended.words-setup"] = "stopped-daoris",
        ["ended.handed-over"] = "stopped-daoris",
        ["ended.quiet"] = "stopped-daoris",
        ["ended.timeout"] = "stopped-daoris",
        ["ended.gone"] = "stopped-daoris",
        ["ended.source-reopened"] = "stopped-daoris",
        ["ended.background-cut"] = "stopped-daoris",
        ["ended.went-on"] = "stopped-daoris",
        ["ended.went-on-exit"] = "stopped-daoris",
        ["ended.limit-words-wait"] = "limited",
        ["ended.done"] = "completed",
        ["ended.submitted"] = "completed",
        ["ended.declined"] = "completed",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The <c>ended.*</c> codes that say how it ended and not who or what ended it: their class is the state's (completed or failed).</summary>
    public static readonly IReadOnlyList<string> ByState =
    [
        "ended.awaits", "ended.exit", "ended.turn-failed-taken", "ended.turn-failed-open", "ended.provider-refused", "ended.parked-asked",
        "ended.parked-transcript", "ended.parked-short", "ended.answered-unfinished", "ended.carried-unfinished", "ended.taken-exit",
        "ended.untouched", "ended.untouched-exit", "ended.went-on-failed", "ended.words-wait", "ended.words-wait-you", "ended.lost-claim",
        "ended.lost-claim-reopened",
    ];

    /// <summary>The account lines a failed end that the account refused carries: its class is <c>access</c>.</summary>
    private static readonly FrozenSet<string> AccessCodes =
        new[] { "account.refused", "account.refused-own", "account.refused-model" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>What a notification reads: the cause of each class, one table (§4.2).</summary>
    public static readonly IReadOnlyDictionary<string, string> Causes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["completed"] = "agent",
        ["failed"] = "agent",
        ["limited"] = "limit",
        ["transient-bound"] = "transient",
        ["access"] = "agent",
        ["stopped-person"] = "person",
        ["stopped-role"] = "role",
        ["stopped-daoris"] = "daoris",
        ["refused-choice"] = "choice",
        ["awaits-helper"] = "agent",
        ["stood-down"] = "daoris",
        ["unknown"] = "unknown",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The end a journal row is, or null where it is none: a move of a session record into a closed state out of one that is not. A reopen,
    /// a same-state write, a flag moving alone and another family's row are none. The class is judged from this row alone.
    /// </summary>
    public static PartEnded? Judge(JournalEntry row, string store)
    {
        if (row.Family != "sessions" || row.Change != "state" || row.StateNew is not { } state) return null;
        if (!Closed.Contains(Norm(state)) || (row.StateOld is { } old && Closed.Contains(Norm(old)))) return null;

        var codes = row.NoteCodes ?? [];
        var limited = row.LimitedNew == true;
        var reason = codes.FirstOrDefault(code => code.StartsWith("ended.", StringComparison.Ordinal));
        return new PartEnded(
            row.Position, store, row.Record, Spelled(state), reason, ClassOf(state, limited, codes), row.Marks.Contains("chat", StringComparer.Ordinal),
            row.Workspace, DateTimeOffset.FromUnixTimeMilliseconds(row.At), limited);
    }

    /// <summary>
    /// The class of an end from its state, its limit flag and its note's codes: a code that names who ended it first, then the limit, then an
    /// account's refusal, then the state's own. What nothing said is <c>unknown</c>, never a guess (D173).
    /// </summary>
    public static string ClassOf(string state, bool limited, IEnumerable<string> codes)
    {
        var held = codes.ToList();
        // An account's limit outranks an end Daoris made or a good one; a person's or a role's stop, and the rest, name themselves.
        foreach (var code in held)
        {
            if (!Decided.TryGetValue(code, out var decided)) continue;
            return limited && decided is "completed" or "stopped-daoris" ? "limited" : decided;
        }

        if (limited) return "limited";
        var norm = Norm(state);
        if (norm == "failed" && held.Any(AccessCodes.Contains)) return "access";
        return norm switch
        {
            "completed" or "declined" => "completed",
            "failed" => "failed",
            "stooddown" => "stood-down",
            _ => "unknown",
        };
    }

    private static string Norm(string state) => state.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>The state in its public spelling, lower-case and hyphenated.</summary>
    private static string Spelled(string state) => Norm(state) == "stooddown" ? "stood-down" : Norm(state);
}

/// <summary>Who reads an end: <c>part.ended</c>'s four meanings, each a filter (§4.2, the survey's §3.4 row 1).</summary>
public enum EndReader
{
    /// <summary>The page's <c>SESSION_ENDED</c>: a conversation's end.</summary>
    SessionEnded,

    /// <summary>The log's <c>session.ended</c>: every end.</summary>
    Log,

    /// <summary>The notifications' <i>Ended</i>: the classes the attention watch says (a conversation's end never).</summary>
    Notified,
}

/// <summary>The one table of what each reader of an end takes (FOUNDATION1 l-ii to l-v).</summary>
public static class EndReaders
{
    /// <summary>The classes the person is not told an end of: their own stop, an account's limit and an agent's refusal of the choice.</summary>
    private static readonly FrozenSet<string> Silent = new[] { "stopped-person", "limited", "refused-choice" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="reader"/> takes <paramref name="end"/>.</summary>
    public static bool Takes(EndReader reader, PartEnded end) => reader switch
    {
        EndReader.SessionEnded => end.Conversation,
        EndReader.Log => true,
        EndReader.Notified => !end.Conversation && Notifies(end.Class),
        _ => false,
    };

    /// <summary>
    /// Whether an end of this class is news to tell the person (<i>Ended</i>): everything but their own stop, an account's limit, which goes on
    /// by itself and says so as its own news (LIMITRESUME1a), and an agent's refusal of the choice, which a fallback answers (D205 §3.5(c)).
    /// </summary>
    public static bool Notifies(string endClass) => !Silent.Contains(endClass);
}
