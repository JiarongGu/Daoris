namespace Daoris.Driver;

/// <summary>
/// What holds the exact tip of a branch the clean-up removes: the line's own branch where it holds the tip by ancestry, else a recovery
/// ref, named here and made in the delete's own act (<see cref="ContentHolds.DeleteJudgedAsync"/>). Read before the look's hold, since the
/// delete verifies each ref it rests on in its own transaction.
/// </summary>
internal static class TipKeeps
{
    /// <summary>
    /// The holder of <paramref name="judged"/>, added to <paramref name="rests"/> where it is the line's own branch (one verify for each ref: git
    /// refuses two for one ref in a transaction, and one the judgement rested on is verified already); the recovery ref's name where
    /// nothing else holds it, with the ref it is kept at; or why the commits could not be kept.
    /// </summary>
    internal static async Task<(RecoveryName? Keep, string? Kept, string? Why)> HoldAsync(
        string root, SweepItem now, string? line, string judged, List<(string Ref, string Commit)> rests)
    {
        var lineAt = now.HeldBy is null ? await LineOwnHoldsAsync(root, line, judged).ConfigureAwait(false) : null;
        if (lineAt is null)
        {
            var (name, why) = await RecoveryKeeps.NameAsync(root, now.Branch, judged, CancellationToken.None).ConfigureAwait(false);
            return name is null ? (null, null, why ?? "no reason was given") : (name, name.Ref, null);
        }

        if (!rests.Any(rest => rest.Ref == $"refs/heads/{line}")) rests.Add(($"refs/heads/{line}", lineAt));
        return (null, null, null);
    }

    /// <summary>
    /// Whether the line's own branch holds <paramref name="tip"/> by ancestry, the one hold that needs no recovery ref; any other answer,
    /// origin's copy alone included, writes one. The commit the branch is read at; null where it does not hold it, or git could not say.
    /// </summary>
    private static async Task<string?> LineOwnHoldsAsync(string root, string? line, string tip)
    {
        if (line is null) return null;
        var none = CancellationToken.None;
        var (readCode, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], none).ConfigureAwait(false);
        if (readCode != 0 || at.Trim() is not { Length: > 0 } commit) return null;
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, commit], none).ConfigureAwait(false);
        return code == 0 ? commit : null;
    }
}
