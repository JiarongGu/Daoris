using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// What the evidence door said of a verdict (EVID1a's <c>POST /api/quests/{id}/evidence</c>), or why nothing was posted: an
/// outcome's code and the service's words, or this side's where it said none.
/// </summary>
/// <param name="Outcome">One of the codes below.</param>
/// <param name="Message">The service's sentence, verbatim, or why nothing reached it.</param>
public sealed record EvidencePosted(string Outcome, string Message)
{
    /// <summary>The exchange kept the verdict: all found lifts the hold, anything else keeps it.</summary>
    public const string Kept = "kept";

    /// <summary>The done no longer waits on evidence (409): found already, or accepted as it stands.</summary>
    public const string NotWaiting = "not-waiting";

    /// <summary>The door refused the verdict (400), or holds no such quest (404).</summary>
    public const string Refused = "refused";

    /// <summary>A host older than the door.</summary>
    public const string NoDoor = "no-door";

    /// <summary>The host did not answer.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>Nothing was read, so nothing was posted (<see cref="EvidenceReading.Unread"/> says why).</summary>
    public const string Unread = "unread";
}

/// <summary>A read's whole end (EVID1b): the verdict where one was read, what the door said, and the record's lines.</summary>
/// <param name="Verdict">What was read, or null where nothing was.</param>
/// <param name="Posted">What the door said, or why nothing was posted.</param>
/// <param name="Bundle">
/// The lines the session record's evidence bundle gains (D46 §4, D144 §5) beneath its commits: the commit read by its full id
/// and how, the count found and whether it was kept, then each item with its code. No machine path.
/// </param>
public sealed record EvidenceOutcome(EvidenceVerdict? Verdict, EvidencePosted Posted, string Bundle)
{
    /// <summary>Whether the done's evidence was found and kept: what lets what it held go on.</summary>
    public bool Released => Posted.Outcome == EvidencePosted.Kept && Verdict is { Found: true };
}

/// <summary>
/// The machine log's evidence line (EVID1b, D144 §5, D94): <c>evidence.checked</c>, one per read, with counts and codes and no
/// words: the quest, the session whose end was read (null for the terminal's), how the commit was chosen, how many items were
/// read and how many each code took, and what the door said.
/// </summary>
public sealed record EvidenceLine(string Event, IReadOnlyList<(string Key, object? Value)> Data)
{
    public static EvidenceLine Checked(string quest, EvidenceAt at, int items, EvidenceOutcome outcome)
    {
        int Count(string result) => outcome.Verdict?.Count(result) ?? 0;
        return new("evidence.checked",
        [
            ("quest", quest), ("session", at.Session), ("how", at.How), ("items", items),
            ("found", Count(EvidenceCodes.Found)), ("missing", Count(EvidenceCodes.Missing)),
            ("uncommitted", Count(EvidenceCodes.Uncommitted)), ("case", Count(EvidenceCodes.Case)),
            ("noQueue", Count(EvidenceCodes.NoQueue)), ("outcome", outcome.Posted.Outcome),
        ]);
    }
}

/// <summary>
/// A done's evidence, read and posted (EVID1b, D144 §3): what a session's end, the orphan sweep and the terminal's check each
/// do with the same code. The driver reads, the exchange judges and keeps; the driver moves no quest (D46 as D144 amends it).
/// </summary>
public static partial class EvidenceCheck
{
    /// <summary>
    /// Read <paramref name="quest"/>'s evidence where <paramref name="at"/> says, post the verdict to the evidence door, and
    /// say the read's line to the client's watchers. Never throws for a host that did not answer: a session's end still writes
    /// its record, and a later read posts again.
    /// </summary>
    public static Task<EvidenceOutcome> RunAsync(ServiceClient service, QuestView quest, EvidenceAt at, CancellationToken ct = default) =>
        RunAsync(service, quest, at, WorkingTree.ReadGitAsync, ct);

    /// <inheritdoc cref="RunAsync(ServiceClient, QuestView, EvidenceAt, CancellationToken)"/>
    /// <param name="git">How git is read: the review's seam (REVIEW3), which a test stands git in at.</param>
    internal static async Task<EvidenceOutcome> RunAsync(
        ServiceClient service, QuestView quest, EvidenceAt at, WorkingTree.GitRead git, CancellationToken ct = default)
    {
        var wanted = EvidenceReader.Wanted(quest);
        var reading = await EvidenceReader.ReadAsync(at, wanted, git, ct).ConfigureAwait(false);

        EvidencePosted posted;
        if (reading.Verdict is not { } verdict)
        {
            posted = new(EvidencePosted.Unread, reading.Unread ?? "nothing was read.");
        }
        else
        {
            try
            {
                posted = await service.EvidenceAsync(quest.Id, verdict, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                posted = new(EvidencePosted.Unanswered, $"the service did not answer ({error.Message})");
            }
        }

        var outcome = new EvidenceOutcome(reading.Verdict, posted, Bundle(reading, posted));
        service.EvidenceSaid(EvidenceLine.Checked(quest.Id, at, wanted.Count, outcome));
        return outcome;
    }

    /// <summary>
    /// The record's evidence at a session's end (D46 §4, D144 §3, §5): <paramref name="commits"/>, and beneath them what was read
    /// of its done's evidence, where <paramref name="quest"/> waits on some. A quest that waits on none, or that the service no
    /// longer answers, ends as it did: git is asked nothing more and nothing is posted. Never throws but for the caller's own
    /// cancellation, since the record is written whatever the read came to.
    /// </summary>
    /// <param name="at">Where and how: the tree's HEAD at a session's end or the sweep, with its base and its session.</param>
    public static Task<string?> AtEndAsync(
        ServiceClient service, QuestView? quest, EvidenceAt at, string? commits, CancellationToken ct = default) =>
        AtEndAsync(service, quest, at, commits, WorkingTree.ReadGitAsync, ct);

    /// <inheritdoc cref="AtEndAsync(ServiceClient, QuestView?, EvidenceAt, string?, CancellationToken)"/>
    internal static async Task<string?> AtEndAsync(
        ServiceClient service, QuestView? quest, EvidenceAt at, string? commits, WorkingTree.GitRead git, CancellationToken ct)
    {
        if (quest is not { AwaitsEvidence: true }) return commits;

        string bundle;
        try
        {
            bundle = (await RunAsync(service, quest, at, git, ct).ConfigureAwait(false)).Bundle;
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            bundle = $"evidence not read: {OneLine(error.Message)}";
        }

        return commits is { Length: > 0 } ? $"{commits}\n{bundle}" : bundle;
    }

    /// <summary>The record's lines for one read (D144 §5): the commit and how, then each item; or why nothing was read.</summary>
    private static string Bundle(EvidenceReading reading, EvidencePosted posted)
    {
        if (reading.Verdict is not { } verdict) return $"evidence not read: {reading.Unread}";

        var text = new StringBuilder($"evidence read at {verdict.Commit} ({verdict.How}): {verdict.Count(EvidenceCodes.Found)} of "
            + $"{verdict.Items.Count} found, ");
        text.Append(posted.Outcome == EvidencePosted.Kept ? "kept" : $"not kept: {OneLine(posted.Message)}");
        foreach (var item in verdict.Items) text.Append('\n').Append(Line(item));
        return text.ToString();
    }

    /// <summary>One item as the record says it: its requirement, what it names, its code and what the code means here.</summary>
    internal static string Line(EvidenceRead item) =>
        item.Gate is { } gate
            ? $"- requirement {item.Requirement} gate `{gate}`: {Said(item)}"
            : $"- requirement {item.Requirement} `{item.Path}`: {Said(item)}";

    /// <summary>
    /// What one read came to, in words beside its code: <c>found</c> and whether this work changed it, the spelling a <c>case</c>
    /// read found, where an <c>uncommitted</c> path is, and a gate's <c>no-queue</c>. The record and the trace say it alike.
    /// </summary>
    internal static string Said(EvidenceRead item)
    {
        if (item.Gate is not null)
        {
            return item.Result == EvidenceCodes.NoQueue ? "no-queue, read from the landing queue, which does not run it here" : item.Result;
        }

        var said = item.Result switch
        {
            EvidenceCodes.Case => $"case, the commit spells it `{item.Spelled}`",
            EvidenceCodes.Uncommitted => "uncommitted, in the tree and not in the commit",
            var result => result,
        };
        var changed = item.Changed switch
        {
            true => ", changed by this work",
            false when item.Result == EvidenceCodes.Found => ", unchanged by this work",
            _ => "",
        };
        return said + changed;
    }

    /// <summary>
    /// The done's commit a driven end read (D144 §3), from a session record's evidence bundle: the full id a session's end or
    /// the sweep wrote, never the terminal's own read. Null where the record says none.
    /// </summary>
    public static string? DonesCommit(string? bundle) =>
        bundle is not null && DrivenRead().Match(bundle) is { Success: true } read ? read.Groups[1].Value : null;

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    [GeneratedRegex(@"^evidence read at ([0-9a-f]{64}|[0-9a-f]{40}) \((?:session-end|sweep)\)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DrivenRead();
}
