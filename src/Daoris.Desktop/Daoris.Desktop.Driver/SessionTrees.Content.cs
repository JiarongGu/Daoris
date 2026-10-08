namespace Daoris.Driver;

/// <summary>
/// Where a session branch's work is held by content though no branch of the person's holds its commits (SQUASHTIDY1): a squash
/// merge makes one new commit on the line from the branch's content, and a cherry-pick copies a change onto another branch
/// under a commit of its own, so ancestry (D88) sees neither.
/// </summary>
/// <param name="Where">What holds it, as git names it: a form of the line (<c>main</c>, <c>origin/main</c>), or a branch of the person's.</param>
/// <param name="Squash">Its whole tree is the tree of a commit on the line since it left it: what a squash merge of it leaves.</param>
public sealed record ContentHold(string Where, bool Squash)
{
    /// <summary>How its work was found held: the clause a removal's sentence and the clean-up's row in the terminal say.</summary>
    public string Said => Squash
        ? $"its work is on `{Where}` by content (a squash merge)"
        : $"its work is on `{Where}` by content (each file it changed reads the same there)";
}

public sealed partial class SessionTrees
{
    /// <summary>
    /// Where a session branch's work is held by content (SQUASHTIDY1), asked only once D88's proof found commits no branch of the
    /// person's holds: the owner squash-merged a session's work as a pull request, the platform deleted the pull request's branch,
    /// and the session branches then held commits no ref contains, so neither Discard nor git would let them go.
    /// </summary>
    /// <remarks>
    /// <para>For each form of the line (D86: its own branch, then <c>origin/</c>'s copy), from where the branch left it:</para>
    /// <list type="number">
    /// <item><b>A squash</b>: the branch's tree is the tree of a commit on the line since then, its tip included. A squash merge
    /// of the branch onto a line that had not moved makes exactly that commit, and the line may have moved on after it.</item>
    /// <item><b>Its files</b>: every file it changed since then reads on the line's tip as the branch left it — D102's proof by
    /// content, as a landed branch is judged.</item>
    /// </list>
    /// <para>Failing both, its files read the same on one branch of the person's: a local branch outside <c>daoris/</c>, where a
    /// cherry-pick put them. One holder holds all of them, so the sentence can name it; work split over two is not proven.</para>
    ///
    /// <para>🔴 <b>Null keeps the refusal</b>: commits that change no file leave nothing to find (D102), and a comparison git could
    /// not make proves nothing. The paths are <c>--no-renames</c>, so a rename is both its paths and a deletion is a path the
    /// holder must not have; git compares them as blobs, so the same means byte for byte as the repository stores it.</para>
    /// </remarks>
    /// <param name="cwd">A working tree of the repository: the session's own, or the registered root.</param>
    /// <param name="revision">The branch's tip as git can name it there (<c>HEAD</c> in its tree, <c>refs/heads/…</c> at the root).</param>
    /// <param name="line">The repository's line, or null where none is set and git names none.</param>
    private static async Task<ContentHold?> HeldByContentAsync(string cwd, string revision, string? line, CancellationToken ct)
    {
        var forms = await LineFormsAsync(cwd, line, ct).ConfigureAwait(false);
        if (forms.Count == 0) return null;
        var (tipCode, tip, _) = await WorkingTree.GitAsync(cwd, ["rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"], ct)
            .ConfigureAwait(false);
        var (treeCode, tree, _) = await WorkingTree.GitAsync(cwd, ["rev-parse", "--verify", "--quiet", $"{revision}^{{tree}}"], ct)
            .ConfigureAwait(false);
        if (tipCode != 0 || treeCode != 0) return null;

        IReadOnlyList<string>? changed = null;
        foreach (var form in forms)
        {
            var (baseCode, mergeBase, _) = await WorkingTree.GitAsync(cwd, ["merge-base", tip.Trim(), form], ct).ConfigureAwait(false);
            if (baseCode != 0) continue;
            var paths = await NamesAsync(cwd, mergeBase.Trim(), tip.Trim(), ct).ConfigureAwait(false);
            if (paths is null || paths.Count == 0) continue;
            changed ??= paths;

            var (logCode, trees, _) = await WorkingTree.GitAsync(cwd, ["log", "--format=%T", $"{mergeBase.Trim()}..{form}"], ct)
                .ConfigureAwait(false);
            if (logCode == 0 && trees.Split('\n', StringSplitOptions.TrimEntries).Contains(tree.Trim(), StringComparer.Ordinal))
            {
                return new ContentHold(form, Squash: true);
            }

            if (await SameFilesAsync(cwd, tip.Trim(), form, paths, ct).ConfigureAwait(false)) return new ContentHold(form, Squash: false);
        }

        if (changed is null) return null;
        // Newest first: a cherry-pick is a recent commit, so the holder is usually found before the rest are compared.
        var (refsCode, refs, _) = await WorkingTree.GitAsync(
            cwd, ["for-each-ref", "--sort=-committerdate", "--format=%(refname)", "refs/heads/"], ct).ConfigureAwait(false);
        if (refsCode != 0) return null;
        foreach (var reference in refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var branch = reference["refs/heads/".Length..];
            if (branch.StartsWith(SessionPrefix, StringComparison.Ordinal) || forms.Contains(branch, StringComparer.Ordinal)) continue;
            if (await SameFilesAsync(cwd, tip.Trim(), reference, changed, ct).ConfigureAwait(false)) return new ContentHold(branch, Squash: false);
        }

        return null;
    }

    /// <summary>Whether every one of <paramref name="paths"/> reads on <paramref name="holder"/> as on <paramref name="tip"/>; false where git could not say.</summary>
    private static async Task<bool> SameFilesAsync(string cwd, string tip, string holder, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var differing = await NamesAsync(cwd, tip, holder, ct).ConfigureAwait(false);
        if (differing is null) return false;
        var differs = new HashSet<string>(differing, StringComparer.Ordinal);
        return !paths.Any(differs.Contains);
    }
}
