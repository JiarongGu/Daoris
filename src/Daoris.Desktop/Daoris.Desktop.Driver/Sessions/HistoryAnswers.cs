using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The codes a reason is said in (HIST1c, HIST1d; the history-clearing design §1.2): the page's catalogue keys for each word, which
/// the shell's <c>Refusals</c> declares as these, and the driver's verbatim refusal for a word this build does not know.
/// </summary>
/// <remarks>In the driver library, so the terminal's <c>history --json</c> prints the code the page reads (HIST1d, D50).</remarks>
public static class HistoryCodes
{
    public const string Unknown = "HISTORY_UNKNOWN";
    public const string Open = "HISTORY_OPEN";
    public const string Asked = "HISTORY_ASKED";
    public const string Live = "HISTORY_LIVE";
    public const string NeedsYou = "HISTORY_NEEDS_YOU";
    public const string Awaited = "HISTORY_AWAITED";
    public const string TreeHere = "HISTORY_TREE_HERE";
    public const string LandingStands = "HISTORY_LANDING_STANDS";
    public const string Unpushed = "HISTORY_UNPUSHED";
    public const string NotOurs = "HISTORY_NOT_OURS";

    /// <summary>The shell's <c>DRIVER_REFUSED</c>: a word from a newer service, said in its own sentence.</summary>
    public const string Refused = "DRIVER_REFUSED";

    /// <summary>The code a word is said in: the service's words and this machine's, and the verbatim refusal for a newer word.</summary>
    public static string Of(string word) => word switch
    {
        HistoryWords.Unknown => Unknown,
        HistoryWords.Open => Open,
        HistoryWords.Asked => Asked,
        HistoryWords.Live => Live,
        HistoryWords.NeedsYou => NeedsYou,
        HistoryWords.Awaited => Awaited,
        HistoryWords.TreeHere => TreeHere,
        HistoryWords.LandingStands => LandingStands,
        HistoryWords.Unpushed => Unpushed,
        HistoryWords.NotOurs => NotOurs,
        _ => Refused,
    };
}

/// <summary><c>HISTORY_PLAN</c>'s answer: the scope, its units, and a workspace's reading.</summary>
public sealed record HistoryPlanAnswer(string Scope, string Id, IReadOnlyList<HistoryUnitAnswer> Units, HistoryReadingAnswer? Reading);

/// <summary>One unit as the page reads it: what it takes, by id, why it stays, and what it holds on the disk.</summary>
public sealed record HistoryUnitAnswer(
    string Kind,
    string Id,
    string? Workspace,
    bool Clearable,
    IReadOnlyList<string> Quests,
    IReadOnlyList<string> Forgotten,
    IReadOnlyList<string> Asks,
    IReadOnlyList<string> Sessions,
    IReadOnlyList<string> Teammates,
    HistoryReasonAnswer? Keep,
    IReadOnlyList<HistoryReasonAnswer> Kept,
    HistorySizesAnswer Bytes);

/// <summary>A reason by its code, which of its sentences is meant, and what it names; the sentence only for a word this build does not know.</summary>
public sealed record HistoryReasonAnswer(
    string Code,
    string? Context,
    string? Quest,
    string? Ask,
    string? Session,
    string? Machine,
    string? Workspace,
    string? Repository,
    string? Branch,
    string? Message);

/// <summary>What the home holds, by kind, in bytes, and their total.</summary>
public sealed record HistorySizesAnswer(long Conversations, long Transcripts, long Files, long Kept, long Other, long Total);

/// <summary>A workspace's reading (§2.4): counts and bytes, never a path or a title.</summary>
public sealed record HistoryReadingAnswer(
    string Workspace,
    int Quests,
    int Asks,
    int Sessions,
    int Teammates,
    HistorySizesAnswer Bytes,
    long Intake,
    HistoryTakesAnswer Takes,
    IReadOnlyDictionary<string, int> KeptBy,
    HistoryCountAnswer Conversations,
    HistoryCountAnswer LeftOver,
    long Log);

/// <summary>What a clear would take now.</summary>
public sealed record HistoryTakesAnswer(int Quests, int Asks, int Sessions, int Teammates, long Bytes);

/// <summary>How many, and how large.</summary>
public sealed record HistoryCountAnswer(int Count, long Bytes);

/// <summary><c>HISTORY_CLEAR</c>'s answer: what went, in counts and bytes, and what changed since the list and stayed.</summary>
public sealed record HistoryClearAnswer(
    string Scope,
    string Id,
    int Listed,
    IReadOnlyList<HistoryUnitName> Cleared,
    int Quests,
    int Asks,
    int Sessions,
    int Teammates,
    int Forgotten,
    long Bytes,
    int LeftOver,
    bool Intake,
    int Failed,
    IReadOnlyList<HistoryChangedAnswer> Changed);

/// <summary>A unit the list said may go that stayed, with what keeps it now.</summary>
public sealed record HistoryChangedAnswer(string Kind, string Id, HistoryReasonAnswer Keep);

/// <summary><c>daoris-driver history --json</c> with no workspace: each workspace's <c>HISTORY_PLAN</c> answer.</summary>
public sealed record HistoryWorkspacesAnswer(IReadOnlyList<HistoryPlanAnswer> Workspaces);

/// <summary>
/// What a clear's two presses answer, for both doors (HIST1c, HIST1d; the history-clearing design §6.2–§6.3): the shell's
/// <c>HISTORY_PLAN</c> and <c>HISTORY_CLEAR</c> return these, and <c>daoris-driver history --json</c> prints the first, so the
/// terminal's answer is the route's field for field by construction. Ids, counts and bytes: never a path or a title.
/// </summary>
/// <remarks>
/// <para>Moved here from the shell's route (HIST1d) so one projection serves both doors; each field list below is held by a test
/// on each side, as <see cref="SessionsCommand.JsonFields"/> is for <c>SESSION_GROUPS</c>.</para>
/// </remarks>
public static class HistoryAnswers
{
    /// <summary>The fields of a plan, in order, as the page reads them.</summary>
    public static IReadOnlyList<string> PlanFields { get; } = ["scope", "id", "units", "reading"];

    /// <summary>The fields of a unit, in order.</summary>
    public static IReadOnlyList<string> UnitFields { get; } =
        ["kind", "id", "workspace", "clearable", "quests", "forgotten", "asks", "sessions", "teammates", "keep", "kept", "bytes"];

    /// <summary>The fields of a reason, in order.</summary>
    public static IReadOnlyList<string> ReasonFields { get; } =
        ["code", "context", "quest", "ask", "session", "machine", "workspace", "repository", "branch", "message"];

    /// <summary>The fields of a workspace's reading, in order.</summary>
    public static IReadOnlyList<string> ReadingFields { get; } =
        ["workspace", "quests", "asks", "sessions", "teammates", "bytes", "intake", "takes", "keptBy", "conversations", "leftOver", "log"];

    /// <summary>The first press's answer.</summary>
    public static HistoryPlanAnswer Plan(HistoryPlan plan) => new(
        HistoryClearing.Word(plan.Scope),
        plan.Id,
        [.. plan.Units.Select(Unit)],
        plan.Reading is { } reading ? Reading(reading) : null);

    /// <summary>The second press's answer.</summary>
    public static HistoryClearAnswer Cleared(HistoryOutcome outcome) => new(
        HistoryClearing.Word(outcome.Scope),
        outcome.Id,
        outcome.Listed,
        [.. outcome.Cleared.Select(unit => unit.Name)],
        outcome.Quests,
        outcome.Asks,
        outcome.Sessions,
        outcome.Teammates,
        outcome.Forgotten,
        outcome.Bytes,
        outcome.LeftOver,
        outcome.Intake,
        outcome.Failed,
        [.. outcome.Changed.Select(unit => new HistoryChangedAnswer(unit.Kind, unit.Id, Reason(unit.Keep!)))]);

    /// <summary>A reason as the page reads it: its code, which of its sentences, what it names; the sentence only for a word this build does not know.</summary>
    public static HistoryReasonAnswer Reason(HistoryKeep keep)
    {
        var code = HistoryCodes.Of(keep.Word);
        return new HistoryReasonAnswer(
            code, keep.Context, keep.Quest, keep.Ask, keep.Session, keep.Machine, keep.Workspace, keep.Repository, keep.Branch,
            code == HistoryCodes.Refused ? keep.Message : null);
    }

    /// <summary>
    /// An answer as the terminal prints it: camel-cased as the page reads it, indented, one line feed per line on every
    /// platform, as <c>sessions --json</c> prints its own.
    /// </summary>
    public static string Json(object answer) => JsonSerializer.Serialize(answer, answer.GetType(), Written);

    private static readonly JsonSerializerOptions Written = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
    };

    private static HistoryUnitAnswer Unit(HistoryUnitPlan unit) => new(
        unit.Kind, unit.Id, unit.Workspace, unit.Clearable, unit.Quests, unit.Forgotten, unit.Asks, unit.Sessions, unit.Teammates,
        unit.Keep is { } keep ? Reason(keep) : null,
        [.. unit.Kept.Select(Reason)],
        Sizes(unit.Bytes));

    private static HistorySizesAnswer Sizes(HistoryBytes bytes) =>
        new(bytes.Conversations, bytes.Transcripts, bytes.Files, bytes.Kept, bytes.Other, bytes.Total);

    private static HistoryReadingAnswer Reading(HistoryReading reading) => new(
        reading.Workspace,
        reading.Quests,
        reading.Asks,
        reading.Sessions,
        reading.Teammates,
        Sizes(reading.Bytes),
        reading.Intake,
        new HistoryTakesAnswer(reading.Takes.Quests, reading.Takes.Asks, reading.Takes.Sessions, reading.Takes.Teammates, reading.Takes.Bytes),
        reading.KeptBy
            .GroupBy(each => HistoryCodes.Of(each.Key), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(each => each.Value), StringComparer.Ordinal),
        new HistoryCountAnswer(reading.Conversations, reading.ConversationBytes),
        new HistoryCountAnswer(reading.LeftOver, reading.LeftOverBytes),
        reading.Log);
}
