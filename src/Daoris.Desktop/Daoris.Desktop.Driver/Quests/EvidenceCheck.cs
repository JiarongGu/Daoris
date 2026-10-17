using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

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
                // FABRIC-CLOSE1b (CLOSE1e's first fence): a submitted quest's verdict names the submission it was read for; a done's names none.
                var named = quest is { Status: "Taken", Submission: { } submission } ? new QuestSubmissionRef(submission.Machine, submission.Sequence) : null;
                posted = await service.EvidenceAsync(quest.Id, verdict with { Submission = named }, ct).ConfigureAwait(false);
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
        foreach (var item in verdict.Items) text.Append('\n').Append(EvidenceWords.Line(item));
        return text.ToString();
    }

    /// <summary>
    /// The done's commit a driven end read (D144 §3), from a session record's evidence bundle: the full id a session's end or
    /// the sweep wrote, never the terminal's own read. Null where the record says none.
    /// </summary>
    public static string? DonesCommit(string? bundle) =>
        bundle is not null && DrivenRead().Match(bundle) is { Success: true } read ? read.Groups[1].Value : null;

    private static string OneLine(string text) => TextBounds.OneLine(text);

    [GeneratedRegex(@"^evidence read at ([0-9a-f]{64}|[0-9a-f]{40}) \((?:session-end|sweep)\)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DrivenRead();
}
