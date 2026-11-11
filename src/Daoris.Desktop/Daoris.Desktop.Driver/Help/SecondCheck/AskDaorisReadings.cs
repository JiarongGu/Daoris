namespace Daoris.Driver;

/// <summary>
/// The states a reading is shown in, each derived from its report alone: a read report with any limit of its evidence is partial, never
/// clean; a left one could not judge.
/// </summary>
public static class ReadingShown
{
    public const string Queued = AskDaorisStates.Queued;
    public const string Reading = AskDaorisStates.Running;
    public const string Read = AskDaorisStates.Read;
    public const string Partial = "partial";
    public const string Left = AskDaorisStates.Left;
    public const string Unread = AskDaorisStates.Unread;
    public const string Disabled = AskDaorisStates.Disabled;
}

/// <summary>One finding as the window shows it, by its place in its report, with the person's <i>Seen</i> and its delivery.</summary>
public sealed record ReadingFinding(
    int Index, string Severity, string Certainty, string Says, string Call, string Instruction, DateTimeOffset? Seen, FlagTold? Told);

/// <summary>
/// One range as the window draws its line under the last turn it covers (<paramref name="Last"/>): its shown state
/// (<see cref="ReadingShown"/>), the turns it holds, every limit of its evidence, why it is not read or still waits, its reader and how
/// that reader stands to the writer, and its findings counted by severity.
/// </summary>
public sealed record AskDaorisReading(
    string Range, string State, DateTimeOffset Opened, DateTimeOffset? Closed, string? Last, IReadOnlyList<string> Turns,
    IReadOnlyList<ReadLimitAt> Limits, string? Why, string? Waiting, SupervisorAgentHolder? Reader, string? Independence,
    IReadOnlyList<ReadingFinding> Findings, int More, string? Leave, IReadOnlyList<SupervisorReason> Reasons)
{
    public int Flags => Findings.Count(finding => finding.Severity == AskDaorisFinding.Flag);

    public int Notes => Findings.Count(finding => finding.Severity == AskDaorisFinding.Note);
}

/// <summary>A page of a conversation's readings, newest first, and the range the next page begins before; null at the last.</summary>
public sealed record ReadingsPage(IReadOnlyList<AskDaorisReading> Readings, string? Next);

/// <summary>A flag the person has not marked seen, with the conversation and range it is of, as the bell counts it.</summary>
public sealed record UnseenFlag(
    string Session, string Range, int Index, string Says, string Certainty, string Call, DateTimeOffset At, string? Reader, string? Independence);

/// <summary>
/// What the window reads of the second check, from the reports alone: a conversation's readings paged newest first, how many of its ranges
/// are queued or being read, and every conversation's flags not yet seen. Nothing here reads, selects or starts anything.
/// </summary>
public static class AskDaorisReadings
{
    public const int PageSize = 20;

    public const int MostPerPage = 50;

    public static ReadingsPage Page(AskDaorisReports reports, string session, string? before, int? limit)
    {
        var size = Math.Clamp(limit ?? PageSize, 1, MostPerPage);
        var newest = reports.Of(session).Reverse().ToList();
        var from = before is null ? 0 : newest.FindIndex(report => report.Range == before) + 1;
        if (before is not null && from == 0) return new([], null);
        var taken = newest.Skip(from).Take(size).ToList();
        return new([.. taken.Select(Shown)], from + taken.Count < newest.Count ? taken[^1].Range : null);
    }

    /// <summary>How many of <paramref name="session"/>'s ranges are queued or being read: the chip's <i>N pending</i>.</summary>
    public static int Pending(AskDaorisReports reports, string session) =>
        reports.Of(session).Count(report => report.State is AskDaorisStates.Queued or AskDaorisStates.Running);

    /// <summary>Every flag of every read report not yet seen, oldest first: what <i>Notifications</i> lists and the bell counts.</summary>
    public static IReadOnlyList<UnseenFlag> Unseen(AskDaorisReports reports) =>
        [.. reports.InState(AskDaorisStates.Read)
            .OrderBy(report => report.Closed ?? report.Opened).ThenBy(report => report.Session, StringComparer.Ordinal).ThenBy(report => report.Range, StringComparer.Ordinal)
            .SelectMany(report => report.Findings.Select((finding, index) => (finding, index))
                .Where(each => each.finding is { Severity: AskDaorisFinding.Flag, Seen: null })
                .Select(each => new UnseenFlag(
                    report.Session, report.Range, each.index, each.finding.Says, each.finding.Certainty, each.finding.Call,
                    report.Closed ?? report.Opened, report.Reader?.Agent, report.Independence)))];

    public static AskDaorisReading Shown(AskDaorisReport report)
    {
        var limits = report.Limits.Concat(report.Turns.SelectMany(turn => turn.Limits)).Distinct().ToList();
        var state = report.State == AskDaorisStates.Read && limits.Count > 0 ? ReadingShown.Partial : report.State;
        return new(
            report.Range, state, report.Opened, report.Closed, report.Turns.Count > 0 ? report.Turns[^1].Id : null, [.. report.Turns.Select(turn => turn.Id)],
            limits, report.Why, report.Waiting, report.Reader, report.Independence,
            [.. report.Findings.Select((finding, index) => new ReadingFinding(
                index, finding.Severity, finding.Certainty, finding.Says, finding.Call, finding.Instruction, finding.Seen, finding.Told))],
            report.More, report.Leave, report.Reasons);
    }
}
