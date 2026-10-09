namespace Daoris.Driver;

/// <summary>
/// What one session's own tree holds that nobody has accepted yet (SESSUX1a, D126 §2.1): the fact Sessions' *To review*
/// is read from, by the clean-up's own proof (D88).
/// </summary>
public sealed partial class SessionTrees
{
    /// <summary>
    /// The tree's uncommitted paths and the commits on it no branch of the person's holds (<see cref="UnlandedAsync"/>);
    /// null where there is no tree of this home's to ask. Where there are such commits, whether the line or a person's branch
    /// holds them by content (SQUASHTIDY1b), and what an unforced discard would do then.
    /// </summary>
    /// <remarks>
    /// <para><b>Only a tree this home opened, still here, and a repository of its own.</b> 🔴 git walks UP: a folder under
    /// the trees that is no repository of its own (a leftover, D109 §5) would answer for whatever repository encloses the
    /// home, so it is never asked, and nothing is said of it. The clean-up's leftovers are where such a folder goes.</para>
    ///
    /// <para><b>Reads, and never writes.</b> <c>git status</c> and <c>git log</c> in the tree; a count git cannot give is
    /// null, which the reader keeps as work, as the clean-up keeps what it cannot clear. The content proof writes nothing
    /// either: the recovery ref an unforced discard would keep is named, never made.</para>
    ///
    /// <para><b>One commit judged</b> (SQUASHTIDY1c): the tree's commit is read once, and the count and the content proof are
    /// both asked of that id, so a commit made meanwhile is never half of what was judged.</para>
    /// </remarks>
    public async Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default)
    {
        bool held;
        try
        {
            held = Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!held || TreeGone(tree) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;

        var (code, porcelain, _) = await WorkingTree.GitAsync(tree, ["status", "--porcelain"], ct).ConfigureAwait(false);
        int? uncommitted = code != 0
            ? null
            : porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        var (tipCode, tipOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
            .ConfigureAwait(false);
        var judged = tipCode == 0 ? tipOut.Trim() : null;
        var work = new TreeWork(await UnlandedAsync(tree, judged ?? "HEAD", ct).ConfigureAwait(false), uncommitted);
        // SQUASHTIDY1b: a squash merge or a cherry-pick holds commits ancestry cannot see, and landing them again would make a
        // branch of work already there.
        return work.Commits is > 0 && judged is not null && await HeldWorkAsync(tree, judged, ct).ConfigureAwait(false) is { } found
            ? work with { Held = found }
            : work;
    }

    /// <summary>
    /// What a tree offers where the line, or a branch of the person's, holds its commits by content (SQUASHTIDY1b): the session's
    /// head's own judgement (<see cref="WorkAsync"/>, then <see cref="DiscardOffer.Of"/>), which every landing asks before anything
    /// is made (SQUASHTIDY1f) and the terminal's plan says. Null where nothing holds them, or there is no tree of this home's to ask.
    /// </summary>
    public async Task<DiscardOffer?> DiscardOfferAsync(string tree, CancellationToken ct = default) =>
        await WorkAsync(tree, ct).ConfigureAwait(false) is { } work ? DiscardOffer.Of(tree, work) : null;

    /// <summary>
    /// Where a tree's commits are held by content, by the review's Discard's own proof (SQUASHTIDY1's
    /// <see cref="HeldByContentAsync"/>, with SQUASHTIDY1c's guarantees), and what an unforced discard of it would do now; null
    /// where nothing holds them, or git could not say.
    /// </summary>
    /// <remarks>
    /// <para>Asked as <see cref="RemoveAsync"/> asks it: in the tree, of <paramref name="judged"/>, against the line of the
    /// workspace the layout names, and never vouched for by a branch a landing made and recorded here, which the clean-up
    /// removes on its own proof.</para>
    ///
    /// <para>🔴 <b>What the tree holds that no commit does keeps it</b>, as the unforced removal asks it: an uncommitted or
    /// untracked path, whatever git's settings hide, and an ignored path the registered checkout lacks. Then no discard is
    /// offered to go, and the clause says why. Otherwise the ref named is the one the discard would keep, numbered as it numbers
    /// one whose name another commit holds.</para>
    /// </remarks>
    private async Task<HeldWork?> HeldWorkAsync(string tree, string judged, CancellationToken ct)
    {
        try
        {
            var full = Path.GetFullPath(tree);
            var (rootCode, commonDir, _) = await WorkingTree.GitAsync(
                full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
            if (rootCode != 0) return null;
            var root = Path.GetDirectoryName(commonDir.Trim())!;
            var (workspace, repository) = OwnerOf(full);
            var line = (await LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
            if (await HeldByContentAsync(full, judged, line, LandedHere(repository), ct).ConfigureAwait(false) is not { } held) return null;

            var holds = await HoldsAsync(full, root, ct).ConfigureAwait(false);
            var stays = holds is null ? "Daoris cannot tell what it holds beyond its commits"
                : holds.Uncommitted.Count > 0 ? $"it has {holds.Uncommitted.Count} uncommitted path(s), which a discard would destroy"
                : holds.Ignored.Count > 0 ? $"it holds {holds.IgnoredSaid}, which a discard would destroy"
                : null;
            if (stays is not null) return new HeldWork(held, null, stays);

            // The name the discard keeps its commits under: the tree's branch, or for a detached tree the one its layout names.
            var (_, branchOut, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
            var named = branchOut.Trim() is { Length: > 0 } branch and not "HEAD" ? branch : BranchOfTree(full)?.Branch ?? "HEAD";
            return await DiscardedNameAsync(root, named, judged, ct).ConfigureAwait(false) is { } keeps
                ? new HeldWork(held, keeps, null)
                : new HeldWork(held, null, "every name for a ref to keep its commits at is taken");
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // A comparison that could not be made proves nothing: its commits are offered to land as ancestry counted them.
            return null;
        }
    }

    /// <summary>
    /// The recovery ref an unforced discard of <paramref name="branch"/> at <paramref name="judged"/> would keep its commits at
    /// (SQUASHTIDY1c's <see cref="KeepDiscardedAsync"/>), named without making it: the first of its names no ref holds, or one
    /// that holds that commit already. Null where every name is taken.
    /// </summary>
    private static async Task<string?> DiscardedNameAsync(string root, string branch, string judged, CancellationToken ct)
    {
        for (var n = 1; n <= DiscardedNames; n++)
        {
            var reference = DiscardedPrefix + branch + (n == 1 ? "" : $"-{n}");
            var (exists, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (exists != 0 || at.Trim() == judged) return reference;
        }

        return null;
    }
}
