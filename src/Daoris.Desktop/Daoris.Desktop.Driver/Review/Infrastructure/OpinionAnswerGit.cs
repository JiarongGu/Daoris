using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The working session's answers read against its tree's history (XAGENT1e, D155 point 7; design §6.4): whether each fix's commit
/// is one the turn made, a fact git answers.
/// </summary>
/// <remarks>
/// <para><b>Named where its callers always named it</b> (STRUCT3c, D166's note): <c>OpinionAnswers.ReadAsync</c> and
/// <c>CheckFixAsync</c> are static members of a C# 14 extension block on the Domain's <see cref="OpinionAnswers"/>, whose lines
/// are pure; reading the tree is this layer's. The git it runs is the one it is handed (<see cref="WorkingTree.GitRead"/>), so a
/// test stands in. Only the loop and the tests ask it.</para>
/// </remarks>
internal static class OpinionAnswerGit
{
    extension(OpinionAnswers)
    {
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
                    : await OpinionAnswers.CheckFixAsync(tree!, tip, head, answer.Commit, git, ct).ConfigureAwait(false);
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
