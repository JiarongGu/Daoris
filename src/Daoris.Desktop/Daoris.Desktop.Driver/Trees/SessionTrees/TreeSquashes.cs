using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The squash a landing rule asks for (PROCESS1b-ii, D164's amendment point 2 and its PROCESS1b-ii note; PART10l, out of
/// <c>SessionTrees.Squash.cs</c>): the pull request's branch made as one commit from the session's work, the first commit Daoris
/// itself makes, and the landing record's word of what it squashed, which is what proves the session landed once its commits are
/// on no branch of the person's. Over the trees' context for that record; the commit's making is static, for the landing's branch
/// form.
/// </summary>
internal sealed class TreeSquashes(TreeContext context)
{
    /// <summary>
    /// The one commit (point 1): the tree of <paramref name="tip"/>, its parent <paramref name="parent"/>, its message
    /// <paramref name="message"/>, made by <c>git commit-tree</c>, which moves no ref and touches no checkout. git makes it as its
    /// own identity in the repository, which <see cref="IdentityProblemAsync"/> asked first.
    /// </summary>
    /// <returns>The commit made, or null with git's reason.</returns>
    internal static async Task<(string? Commit, string? Why)> SquashAsync(
        string root, string tip, string parent, string message, CancellationToken ct)
    {
        var (code, made, err) = await WorkingTree.GitAsync(root, ["commit-tree", $"{tip}^{{tree}}", "-p", parent, "-m", message], ct)
            .ConfigureAwait(false);
        return code == 0 && made.Trim().Length > 0 ? (made.Trim(), null) : (null, FirstLine(err));
    }

    /// <summary>
    /// Why git has no identity to make the squash in <paramref name="repository"/> (point 1), as Accept's refusal, or null where
    /// its <c>user.name</c> and <c>user.email</c> are both set there. Daoris names nobody of its own: a commit it makes is made as
    /// the person's git makes theirs, and one with no name would carry whatever git guessed from the machine.
    /// </summary>
    internal static async Task<string?> IdentityProblemAsync(string root, string repository, CancellationToken ct)
    {
        var (_, name, _) = await WorkingTree.GitAsync(root, ["config", "--get", "user.name"], ct).ConfigureAwait(false);
        var (_, email, _) = await WorkingTree.GitAsync(root, ["config", "--get", "user.email"], ct).ConfigureAwait(false);
        var missing = (name.Trim().Length == 0, email.Trim().Length == 0) switch
        {
            (true, true) => "`user.name` or `user.email`",
            (true, false) => "`user.name`",
            (false, true) => "`user.email`",
            _ => null,
        };
        return missing is null
            ? null
            : $"the landing rule for `{repository}` squashes the work into one commit, which git makes as its own identity there, and git "
              + $"has no {missing} in `{repository}`, so nothing was landed. Set them in its checkout (`git config user.name \"Your Name\"` "
              + "and `git config user.email you@example.com`), or set the rule without `--squash` (`daoris driver landing`), then accept again.";
    }

    /// <summary>
    /// The standing landing whose squash holds <paramref name="tip"/> (point 3): its recorded session tip is that commit or one
    /// that grew from it, and its branch still stands holding the commit it was recorded at. Null where none does, or git could
    /// not say. The record is Daoris's own word of what it squashed, so it vouches as a branch the landing made would by ancestry,
    /// and only while that branch is the landing's (D102's rule); the content proof (SQUASHTIDY1) is left as it is for the rest.
    /// </summary>
    /// <param name="cwd">A working tree of the repository: the session's own, or the registered root.</param>
    /// <param name="tip">The session branch's commit, read once by the caller.</param>
    /// <param name="refs">The judgement's refs (PROC1d-ii); null asks git.</param>
    internal async Task<LandedBranch?> SquashedHolderAsync(string cwd, string repository, string tip, CancellationToken ct, RefSnapshot? refs = null) =>
        (await SquashedHoldAsync(cwd, repository, tip, ct, refs).ConfigureAwait(false))?.Entry;

    /// <summary>
    /// <see cref="SquashedHolderAsync"/> with the commit its branch was read standing at (FABRIC-END4a, third review): the record vouches only
    /// while that branch is the landing's, so a removal on its word rests on the branch at that commit, and verifies it as it goes.
    /// </summary>
    internal async Task<(LandedBranch Entry, string At)?> SquashedHoldAsync(string cwd, string repository, string tip, CancellationToken ct, RefSnapshot? refs = null)
    {
        var squashed = context.Recorded.All()
            .Where(entry => entry.Squashed is not null && string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .ToList();
        if (squashed.Count == 0) return null;

        // One ask for them all first, since the head asks it of every tree at each look: where nothing of the tip's history is
        // outside every squashed tip's, the tip is inside one of them. A squashed tip git no longer has is passed over.
        var (code, outside, _) = await WorkingTree.GitAsync(
            cwd, ["rev-list", "--ignore-missing", "-n", "1", tip, "--not", .. squashed.Select(entry => entry.Squashed!)], ct).ConfigureAwait(false);
        if (code != 0 || outside.Trim().Length > 0) return null;

        foreach (var entry in squashed)
        {
            var (under, _, _) = await WorkingTree.GitAsync(cwd, ["merge-base", "--is-ancestor", tip, entry.Squashed!], ct).ConfigureAwait(false);
            if (under == 0 && await LandedProofs.StandingTipAsync(cwd, entry, ct, refs).ConfigureAwait(false) is { } at) return (entry, at);
        }

        return null;
    }

    /// <summary>
    /// A chain's later landing under a squash (point 4), once the landing's advance (<see cref="TreeLands"/>) judged the branch may move on: one more
    /// commit on the pull request's branch, its tree the session's tip's, its parent the branch's tip, its message only what the
    /// session added since the recorded tip; then the branch moved to it by compare-and-swap from the tip judged, as a
    /// fast-forward is. Like the rest of the branch form, it writes one ref and touches no checkout.
    /// </summary>
    /// <param name="standsAt">The commit the branch stands at, which the new commit grows from.</param>
    /// <param name="head">The session's tip, judged to grow from <paramref name="grewFrom"/> with something new.</param>
    /// <param name="grewFrom">The session tip the record says was squashed last, or the branch's own tip where it says none.</param>
    /// <param name="against">The line's form, from which a plugin is told every commit the branch holds now (D100).</param>
    internal static async Task<Branched> AdvanceSquashedAsync(
        string root, string name, string standsAt, string branch, string? line, string head, string grewFrom, bool handedOn,
        string? against, LandingSubject squash, CancellationToken ct)
    {
        var (logCode, added, logErr) = await WorkingTree.GitAsync(
            root, ["log", "--reverse", "--format=%H%x09%s", $"{grewFrom}..{head}"], ct).ConfigureAwait(false);
        if (logCode != 0)
        {
            return new TreeLanding(false, $"git cannot list what `{branch}` added since `{name}` was last landed: {FirstLine(logErr)} "
                + "Nothing was changed.");
        }

        var adds = Commits(added);
        var (made, why) = await SquashAsync(root, head, standsAt, SquashMessage.Of(squash.SquashTitle, adds, squash.Session), ct).ConfigureAwait(false);
        if (made is null) return new TreeLanding(false, $"git would not make the squashed commit for `{name}`: {why} Nothing was changed.");

        // Compare-and-swap from the tip judged: git refuses where the branch moved since, and nothing is forced.
        var (moved, _, moveErr) = await WorkingTree.GitAsync(
            root, ["update-ref", "-m", $"daoris: {branch} squashed onto {name}", $"refs/heads/{name}", made, standsAt], ct).ConfigureAwait(false);
        if (moved != 0)
        {
            return new TreeLanding(false, $"git would not move `{name}` on from {TextBounds.ShortCommit(standsAt, 8)}: "
                + $"{FirstLine(moveErr)} It may have moved meanwhile; nothing was changed.")
            { Refusal = AutoLandingCode.Exists };
        }

        // What the branch holds beyond the line now, as a plugin is told at an advance; the one commit made where git cannot say.
        var (heldCode, held, _) = against is null
            ? (1, "", "")
            : await WorkingTree.GitAsync(root, ["log", "--reverse", "--format=%H%x09%s", $"{against}..{made}"], ct).ConfigureAwait(false);
        IReadOnlyList<LandingCommit> carried = heldCode == 0 && Commits(held) is { Count: > 0 } all
            ? all
            : [new LandingCommit(made, SquashMessage.Subject(squash.SquashTitle, adds, squash.Session))];

        var pushIt = handedOn ? "" : $" Push it to grow its pull request: `git push origin {name}`.";
        return new Branched(
            new TreeLanding(true,
                $"moved `{name}` on from {TextBounds.ShortCommit(standsAt, 8)} to this session's work — {adds.Count} more commit(s), "
                + $"squashed into one.{pushIt} Nothing was merged and the checkout was not touched.{TreeLands.TreeStays}",
                name)
            { AdvancedFrom = standsAt },
            root, line, carried, branch)
        { Squashed = head };
    }

    /// <summary>The commits <c>git log --format=%H%x09%s</c> listed, each its id and its subject, in git's order.</summary>
    private static List<LandingCommit> Commits(string log) =>
        [.. log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('\t', 2))
            .Select(parts => new LandingCommit(parts[0], parts.Length > 1 ? parts[1] : ""))];

    /// <summary>
    /// <see cref="TreeFollows.UnlandedInTreeAsync"/>, with the record's squash among the proofs (point 3): a tree whose commits a
    /// squashing landing holds counts none. What the automatic landing asks before it lands a done.
    /// </summary>
    internal async Task<int?> UnlandedHereAsync(string tree, CancellationToken ct = default)
    {
        var unlanded = await TreeFollows.UnlandedInTreeAsync(tree, "HEAD", ct).ConfigureAwait(false);
        if (unlanded is not > 0 || !context.Holds(tree)) return unlanded;
        var (code, tip, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct).ConfigureAwait(false);
        return code == 0
               && await SquashedHolderAsync(tree, context.OwnerOf(Path.GetFullPath(tree)).Repository, tip.Trim(), ct).ConfigureAwait(false) is not null
            ? 0
            : unlanded;
    }

    /// <summary>How a removal says the record's squash held the work (point 3), in the clause <see cref="ContentHold.Said"/> takes elsewhere.</summary>
    internal static string SquashedSaid(LandedBranch entry) => $"its work is on `{entry.Branch}`, squashed into one commit by its landing";
}
