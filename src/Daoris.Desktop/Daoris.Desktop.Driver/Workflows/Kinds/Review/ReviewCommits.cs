namespace Daoris.Driver;

/// <summary>
/// One file of a branch's or a checkout's review record, drawn from the commits the record names, never from the checkout as it stands: after,
/// the branch's tip or the newest turn's end; before, the line's merge base with the tip or the first turn's start.
/// </summary>
public static class ReviewCommits
{
    /// <summary>The text of <paramref name="path"/> on <paramref name="side"/> of <paramref name="record"/>, read in the checkout at <paramref name="root"/>.</summary>
    public static async Task<(string? Text, string? Refused)> ReadAsync(string root, ReviewRecord record, string path, string side, CancellationToken ct = default)
    {
        if (path.Length == 0 || Path.IsPathRooted(path) || path.Split('/', '\\').Contains("..")) return (null, ReviewContent.NotRow);
        var commit = await CommitAsync(root, record, side, ct).ConfigureAwait(false);
        if (commit is null) return (null, record.Kind is ReviewKinds.Branch or ReviewKinds.Checkout ? ReviewContent.NotRow : ReviewContent.NotKept);
        var (code, text, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "show", "--no-textconv", $"{commit}:{path.Replace('\\', '/')}"], ct).ConfigureAwait(false);
        return code == 0 ? (text, null) : (null, ReviewContent.NotRow);
    }

    /// <summary>The commit <paramref name="side"/> of <paramref name="record"/> names; null where the record names none.</summary>
    public static async Task<string?> CommitAsync(string root, ReviewRecord record, string side, CancellationToken ct)
    {
        var reference = record.Reference;
        switch (record.Kind, side)
        {
            case (ReviewKinds.Branch, ReviewContent.After):
                return reference.Tip;
            case (ReviewKinds.Branch, ReviewContent.Before) when reference.Base is { } frozen:
                return frozen;
            case (ReviewKinds.Branch, ReviewContent.Before) when reference is { Tip: { } tip, Line: { } line }:
                return await LineBaseAsync(root, line, tip, ct).ConfigureAwait(false);
            case (ReviewKinds.Checkout, ReviewContent.After):
                return reference.Turns.LastOrDefault()?.To;
            case (ReviewKinds.Checkout, ReviewContent.Before):
                return reference.Turns.FirstOrDefault()?.From;
            default:
                return null;
        }
    }

    /// <summary>With no frozen base, the newest commit the tip shares with the line here or on origin: a local line behind its remote would count the remote's merged work.</summary>
    private static async Task<string?> LineBaseAsync(string root, string line, string tip, CancellationToken ct)
    {
        async Task<string?> MergeBase(string of)
        {
            var (code, based, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "merge-base", of, tip], ct).ConfigureAwait(false);
            return code == 0 ? based.Trim() : null;
        }

        var candidates = ReviewBase.Candidates(await MergeBase(line).ConfigureAwait(false), await MergeBase($"refs/remotes/origin/{line}").ConfigureAwait(false), null);
        if (candidates.Count < 2) return candidates.FirstOrDefault();
        var (named, maximal, _) = await WorkingTree.GitAsync(root, ["--no-optional-locks", "merge-base", "--independent", .. candidates], ct).ConfigureAwait(false);
        return named == 0 ? ReviewBase.Newest(candidates, maximal.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) : candidates[0];
    }
}
