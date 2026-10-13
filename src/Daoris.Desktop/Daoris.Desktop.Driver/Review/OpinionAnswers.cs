using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The working session's answers, read when its turn ends (XAGENT1e, D155 point 7; design §6.4): each finding's latest answer,
/// a fix's commit checked against the tree's history, and a finding with none read as <c>unresolved</c>, not answered. What a
/// fix fixes is the session's claim; that its commit is one the turn made is a fact git answers.
/// </summary>
/// <remarks>
/// A top file, not the Domain's (FLAT1b-ii): reading the tree asks git and the disk, so the type holds I/O; the records and
/// codes it reads stay in <c>Domain/OpinionDelivery.cs</c>. The lines are pure. The git it runs is the one it is handed
/// (<see cref="WorkingTree.GitRead"/>), so a test stands in.
/// </remarks>
public static class OpinionAnswers
{
    /// <summary>
    /// The driver's line in the working session's conversation once its answers are read: whose findings they were, and how
    /// each counts. The person's view of the answers beside the claims is the page's (XAGENT1g); this is the record's.
    /// </summary>
    public static string Line(OpinionView opinion, OpinionReading reading) => Told(opinion, reading).Note;

    /// <summary>
    /// <see cref="Line"/> by code (CONVNOTE2h), the English byte for byte: a lead for whose findings and how many, then a part for
    /// each way they count that is above nought, in the English's order, and the disputed <c>must</c> findings last.
    /// </summary>
    public static Noted Told(OpinionView opinion, OpinionReading reading)
    {
        int Count(Func<OpinionAnswerRead, bool> which) => reading.Findings.Count(which);
        var fixedCount = Count(row => row.Counts == OpinionViews.Fixed);
        var rejected = Count(row => row.Counts == OpinionViews.Rejected);
        var unresolved = Count(row => row.Counts == OpinionViews.Unresolved && row.Why is null);
        var unanswered = Count(row => row.Why == OpinionAnswerWhy.NotAnswered);
        var notMade = Count(row => row.Said == OpinionViews.Fixed && row.Fix is null);
        var disputed = Count(row => row.Disputed);
        var counts = new List<Noted>();
        void Add(NoteCode code, int count, string text)
        {
            if (count > 0) counts.Add(Noted.Of(code, text, ("count", count)));
        }

        Add(NoteCodes.OpinionFixed, fixedCount, $"{fixedCount} fixed, each commit read from git as one this turn made");
        Add(NoteCodes.OpinionUnproven, notMade, $"{notMade} said fixed whose commit git did not read as one this turn made, read as unresolved");
        Add(NoteCodes.OpinionRejected, rejected, $"{rejected} rejected with its evidence");
        Add(NoteCodes.OpinionUnresolved, unresolved, $"{unresolved} unresolved");
        Add(NoteCodes.OpinionUnanswered, unanswered, $"{unanswered} not answered, read as unresolved");

        var noted = new Noted("— ", []).Then(
            "",
            Noted.Of(
                NoteCodes.OpinionAnswersLead, $"the answers to {opinion.Who}'s {Plural(reading.Findings.Count, "finding")}, as its turn ended:",
                ("who", opinion.Who), ("count", reading.Findings.Count)));
        for (var each = 0; each < counts.Count; each++) noted = noted.Then(each == 0 ? " " : "; ", counts[each]);
        // The English has always closed an empty list as ": ." (a space with nothing after it).
        noted = noted.Then(counts.Count == 0 ? " ." : ".");
        return disputed > 0
            ? noted.Then(
                " ",
                Noted.Of(
                    NoteCodes.OpinionDisputed, $"{Plural(disputed, "`must` finding")} not fixed {(disputed == 1 ? "waits" : "wait")} for the person",
                    ("count", disputed))).Then(".")
            : noted;
    }

    /// <summary>
    /// How the reviewer's own run ended, by code (XAGENT1g4a, D142): the English as <c>Driver.Read</c> has always worded it, a
    /// failed turn's own message beneath as the program's words (it may hold a path).
    /// </summary>
    public static Noted Concluded(string opinion, int exitCode, string? turnFailed) =>
        turnFailed is { Length: > 0 } failed
            ? Noted.Of(
                    NoteCodes.OpinionReviewerFailed, $"the reviewer's turn failed as it read the work for second opinion `{opinion}`:", ("opinion", opinion))
                .Then(" ", Noted.Said(failed, NoteBy.Program))
            : exitCode == 0
                ? Noted.Of(NoteCodes.OpinionReviewerRead, $"it read the work and ended; what it said is second opinion `{opinion}`'s.", ("opinion", opinion))
                : Noted.Of(
                    NoteCodes.OpinionReviewerExited,
                    $"it ended with exit {exitCode} as it read the work; what it said, if anything, is second opinion `{opinion}`'s.",
                    ("opinion", opinion), ("exit", exitCode));

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    /// <summary>
    /// The answers to <paramref name="opinion"/>'s findings as they stand, read in <paramref name="tree"/>: a fix counts where
    /// git reads its commit after the candidate's tip and at or before the tree's tip. A tree that is gone checks no fix.
    /// </summary>
    internal static async Task<OpinionReading> ReadAsync(
        OpinionView opinion, string session, string? tree, WorkingTree.GitRead git, DateTimeOffset at, CancellationToken ct)
    {
        var readable = tree is { Length: > 0 } && Directory.Exists(tree);
        var head = readable ? await CommitAsync(tree!, "HEAD", git, ct).ConfigureAwait(false) : null;
        var tip = readable && head is not null ? await CommitAsync(tree!, opinion.Tip, git, ct).ConfigureAwait(false) : null;

        var rows = new List<OpinionAnswerRead>();
        foreach (var finding in opinion.Findings ?? [])
        {
            var answer = opinion.AnswerTo(finding.Number);
            if (answer is null)
            {
                rows.Add(new OpinionAnswerRead(finding.Number, finding.Weight, OpinionViews.Unresolved) { Why = OpinionAnswerWhy.NotAnswered });
                continue;
            }

            if (answer.Said != OpinionViews.Fixed)
            {
                var counts = answer.Said == OpinionViews.Rejected ? OpinionViews.Rejected : OpinionViews.Unresolved;
                rows.Add(new OpinionAnswerRead(finding.Number, finding.Weight, counts) { Said = answer.Said });
                continue;
            }

            var (fix, why) = head is null || tip is null
                ? (null, OpinionAnswerWhy.FixUnread)
                : await CheckFixAsync(tree!, tip, head, answer.Commit, git, ct).ConfigureAwait(false);
            rows.Add(new OpinionAnswerRead(finding.Number, finding.Weight, fix is null ? OpinionViews.Unresolved : OpinionViews.Fixed)
            {
                Said = answer.Said, Commit = answer.Commit, Fix = fix, Why = why,
            });
        }

        return new OpinionReading(session, head, at, rows);
    }

    /// <summary>
    /// Whether <paramref name="commit"/> is one the turn made (design §6.4): a commit the tree holds, after
    /// <paramref name="tip"/>, at or before <paramref name="head"/>. Its full id where it is; else why not, by code.
    /// </summary>
    internal static async Task<(string? Fix, string? Why)> CheckFixAsync(
        string tree, string tip, string head, string? commit, WorkingTree.GitRead git, CancellationToken ct)
    {
        // A name git would read as an option names no commit, and neither does anything that is no commit's id.
        if (commit is not { Length: > 0 } named || !WorkingTree.IsCommitId(named)) return (null, OpinionAnswerWhy.FixUnknown);
        var full = await CommitAsync(tree, named, git, ct).ConfigureAwait(false);
        if (full is null) return (null, OpinionAnswerWhy.FixUnknown);
        if (string.Equals(full, tip, StringComparison.OrdinalIgnoreCase)) return (null, OpinionAnswerWhy.FixNotAfter);

        // `--is-ancestor` answers 0 for yes and 1 for no; anything else is git failing to answer.
        var after = await RunAsync(tree, ["merge-base", "--is-ancestor", tip, full], git, ct).ConfigureAwait(false);
        if (after.Code == 1) return (null, OpinionAnswerWhy.FixNotAfter);
        if (after.Code != 0) return (null, OpinionAnswerWhy.FixUnread);

        var on = await RunAsync(tree, ["merge-base", "--is-ancestor", full, head], git, ct).ConfigureAwait(false);
        return on.Code switch
        {
            0 => (full, null),
            1 => (null, OpinionAnswerWhy.FixOffBranch),
            _ => (null, OpinionAnswerWhy.FixUnread),
        };
    }

    /// <summary>A commit's full id as <paramref name="tree"/>'s git names it, or null.</summary>
    private static async Task<string?> CommitAsync(string tree, string name, WorkingTree.GitRead git, CancellationToken ct)
    {
        if (name.StartsWith('-')) return null;
        var (code, output) = await RunAsync(tree, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], git, ct).ConfigureAwait(false);
        var id = output.Trim().ToLowerInvariant();
        return code == 0 && EvidenceCodes.IsObjectId(id) ? id : null;
    }

    private static async Task<(int Code, string Output)> RunAsync(
        string tree, IReadOnlyList<string> arguments, WorkingTree.GitRead git, CancellationToken ct)
    {
        var output = new StringBuilder();
        var code = await git(tree, arguments, piece =>
        {
            output.Append(piece.Span);
            return true;
        }, ct).ConfigureAwait(false);
        return (code, output.ToString());
    }
}
