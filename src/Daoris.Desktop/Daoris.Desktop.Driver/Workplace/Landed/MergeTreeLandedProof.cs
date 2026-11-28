using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The local landing's proof: the work is landed when merging the line into it, in git's own scratch (<c>merge-tree --write-tree</c>),
/// leaves the line's tree as it is. No patch id decides: a merge commit's own resolution and a change of whitespace alone are content a
/// patch id passes over. A merge that conflicts or a merged tree that differs is unlanded; anything git cannot answer is unknown.
/// </summary>
internal sealed class MergeTreeLandedProof : ILandedProof
{
    public IReadOnlyCollection<LandingKind> Kinds { get; } = [LandingKind.Local];

    public async Task<LandedAnswer> ProveAsync(LandedQuestion question, CancellationToken ct)
    {
        // Both named first: git's merge says a name it cannot read in the code it says a conflict in.
        var line = await GitRuns.GitAsync(question.Root, ["rev-parse", "--verify", "--quiet", $"{question.Line}^{{tree}}"], ct).ConfigureAwait(false);
        if (line.Code != 0) return LandedAnswer.Unknown($"git names no tree for `{question.Line}`");
        var work = await GitRuns.GitAsync(question.Root, ["rev-parse", "--verify", "--quiet", $"{question.Work}^{{commit}}"], ct).ConfigureAwait(false);
        if (work.Code != 0) return LandedAnswer.Unknown($"git names no commit for `{question.Work}`");
        var merged = await GitRuns.GitAsync(question.Root, ["merge-tree", "--write-tree", "--no-messages", question.Line, question.Work], ct).ConfigureAwait(false);
        var tree = merged.Stdout.Split('\n', StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        if (merged.Code is not (0 or 1) || tree.Length == 0) return LandedAnswer.Unknown($"git could not merge `{question.Line}` into it: {FirstLine(merged.Stderr)}");
        if (merged.Code == 1) return new(LandedVerdict.Unlanded, $"merging `{question.Line}` into it conflicts");
        return tree == line.Stdout.Trim()
            ? new(LandedVerdict.Landed, $"merging `{question.Line}` into it leaves the line's tree {tree[..Math.Min(12, tree.Length)]} as it is")
            : new(LandedVerdict.Unlanded, $"`{question.Line}` lacks some of its content");
    }
}
