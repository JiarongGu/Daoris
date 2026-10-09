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

    /// <summary>
    /// Where the commits a content-held removal deleted are kept (SQUASHTIDY1c), and the two things a person may do with them:
    /// bring the branch back, or let them go. A content proof compares files, so a commit's message, an empty commit and
    /// history the final tree does not show are held by this ref alone.
    /// </summary>
    public static string KeptAt(string branch, string reference) =>
        $"Its commits stay at `{reference}` until you delete that ref; `git branch {branch} {reference}` brings the branch back.";
}

public sealed partial class SessionTrees
{
    /// <summary>
    /// Where a content-held removal keeps what it deleted (SQUASHTIDY1c): one ref per deletion, under the branch's own name, out
    /// of every proof's sight (<c>--branches</c> and <c>--remotes</c> never read it), and never deleted by Daoris.
    /// </summary>
    internal const string DiscardedPrefix = "refs/daoris/discarded/";

    /// <summary>How many names a branch's recovery refs take before a discard gives up (SQUASHTIDY1c), the offer naming them alike (SQUASHTIDY1b).</summary>
    private const int DiscardedNames = 20;

    /// <summary>What a delete that found its branch moved since the judgement says (SQUASHTIDY1c).</summary>
    internal const string MovedSince = "it moved since it was judged";

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
    ///
    /// <para>🔴 <b>Every comparison is between object ids read once</b> (SQUASHTIDY1c): the tip is the caller's, and its tree is
    /// read from that id, so a commit made meanwhile is never half of what was judged; each holder is read once too. A branch
    /// a landing made and recorded never holds another's work here, since the clean-up removes it on its own proof: one that
    /// vouched for a session branch would leave that work on no ref once it went.</para>
    /// </remarks>
    /// <param name="cwd">A working tree of the repository: the session's own, or the registered root.</param>
    /// <param name="tip">The branch's tip as an object id, read once by the caller, which deletes only while the branch is still it.</param>
    /// <param name="line">The repository's line, or null where none is set and git names none.</param>
    /// <param name="notHolders">The branches that may not hold its work: those a landing made and recorded here.</param>
    private static async Task<ContentHold?> HeldByContentAsync(
        string cwd, string tip, string? line, IReadOnlySet<string> notHolders, CancellationToken ct)
    {
        var forms = new List<(string Name, string Id)>();
        foreach (var name in await LineFormsAsync(cwd, line, ct).ConfigureAwait(false))
        {
            var (formCode, formId, _) = await WorkingTree.GitAsync(cwd, ["rev-parse", "--verify", "--quiet", $"{name}^{{commit}}"], ct)
                .ConfigureAwait(false);
            if (formCode == 0) forms.Add((name, formId.Trim()));
        }

        if (forms.Count == 0) return null;
        var (treeCode, tree, _) = await WorkingTree.GitAsync(cwd, ["rev-parse", "--verify", "--quiet", $"{tip}^{{tree}}"], ct)
            .ConfigureAwait(false);
        if (treeCode != 0) return null;

        IReadOnlyList<string>? changed = null;
        foreach (var (form, formId) in forms)
        {
            var (baseCode, mergeBase, _) = await WorkingTree.GitAsync(cwd, ["merge-base", tip, formId], ct).ConfigureAwait(false);
            if (baseCode != 0) continue;
            var paths = await NamesAsync(cwd, mergeBase.Trim(), tip, ct).ConfigureAwait(false);
            if (paths is null || paths.Count == 0) continue;
            changed ??= paths;

            var (logCode, trees, _) = await WorkingTree.GitAsync(cwd, ["log", "--format=%T", $"{mergeBase.Trim()}..{formId}"], ct)
                .ConfigureAwait(false);
            if (logCode == 0 && trees.Split('\n', StringSplitOptions.TrimEntries).Contains(tree.Trim(), StringComparer.Ordinal))
            {
                return new ContentHold(form, Squash: true);
            }

            if (await SameFilesAsync(cwd, tip, formId, paths, ct).ConfigureAwait(false)) return new ContentHold(form, Squash: false);
        }

        if (changed is null) return null;
        // Newest first: a cherry-pick is a recent commit, so the holder is usually found before the rest are compared.
        var (refsCode, refs, _) = await WorkingTree.GitAsync(
            cwd, ["for-each-ref", "--sort=-committerdate", "--format=%(objectname) %(refname)", "refs/heads/"], ct).ConfigureAwait(false);
        if (refsCode != 0) return null;
        foreach (var row in refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = row.IndexOf(' ');
            if (space <= 0) continue;
            var (id, branch) = (row[..space], row[(space + 1)..]["refs/heads/".Length..]);
            if (branch.StartsWith(SessionPrefix, StringComparison.Ordinal) || forms.Any(form => form.Name == branch)
                || notHolders.Contains(branch))
            {
                continue;
            }

            if (await SameFilesAsync(cwd, tip, id, changed, ct).ConfigureAwait(false)) return new ContentHold(branch, Squash: false);
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

    /// <summary>The branches a landing made and recorded in <paramref name="repository"/>: never a holder by content (SQUASHTIDY1c).</summary>
    private IReadOnlySet<string> LandedHere(string repository) =>
        Recorded.All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Branch)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Keep the commit a content-held removal is about to delete on a ref of its own (SQUASHTIDY1c), before anything goes: the
    /// proof compared files, and a commit's message, an empty commit and history the final tree does not show are held nowhere
    /// else. One ref per deletion, under the branch's name, a number after it where that name holds another commit already.
    /// </summary>
    /// <returns>The ref, or null with git's reason; then nothing may be removed.</returns>
    private static async Task<(string? Reference, string? Why)> KeepDiscardedAsync(
        string root, string branch, string judged, CancellationToken ct)
    {
        string? why = null;
        for (var n = 1; n <= DiscardedNames; n++)
        {
            var reference = DiscardedPrefix + branch + (n == 1 ? "" : $"-{n}");
            // An empty old value: made only where no ref of that name exists, so none is ever moved.
            var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", reference, judged, ""], ct).ConfigureAwait(false);
            if (code == 0) return (reference, null);
            var (exists, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (exists != 0) return (null, FirstLine(err));
            if (at.Trim() == judged) return (reference, null);
            why = FirstLine(err);
        }

        return (null, why ?? "every name for it is taken");
    }

    /// <summary>
    /// A recovery ref made for a delete that did not happen (SQUASHTIDY1c) goes again, while the branch still holds its commit:
    /// the ref is one per deletion. Where the branch moved off it, the ref stays, since then it alone holds that commit.
    /// </summary>
    private static async Task DropKeptAsync(string root, string branch, string? reference, string judged, CancellationToken ct)
    {
        if (reference is null) return;
        var (held, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", judged, $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (held == 0) await WorkingTree.GitAsync(root, ["update-ref", "-d", reference, judged], ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a branch a judgement cleared, only while it is still the commit judged (SQUASHTIDY1c): <c>git update-ref -d</c>
    /// with that id, which git refuses once the ref moved, so a commit made after the judgement never goes with it. Where the
    /// branch is gone already, nothing is left to delete.
    /// </summary>
    /// <returns>Null where it is deleted; otherwise why it is kept, in a clause.</returns>
    private async Task<string?> DeleteJudgedAsync(string root, string branch, string judged, CancellationToken ct)
    {
        if (BeforeDeleting is { } seam) await seam(branch).ConfigureAwait(false);
        // git branch -D refuses a branch a working tree has checked out; update-ref does not ask, so this does.
        if ((await WorktreesAsync(root, ct).ConfigureAwait(false)).GetValueOrDefault(branch) is { } checkedOut)
        {
            return $"it is checked out at {checkedOut}";
        }

        var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", "-d", $"refs/heads/{branch}", judged], ct).ConfigureAwait(false);
        if (code != 0)
        {
            var (exists, now, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
                .ConfigureAwait(false);
            if (exists != 0) return null;
            return now.Trim() != judged ? MovedSince : $"git would not delete it: {FirstLine(err)}";
        }

        // What git branch -D also removes: the branch's own section of the config, its upstream where it had one.
        await WorkingTree.GitAsync(root, ["config", "--remove-section", $"branch.{branch}"], ct).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// What an unforced removal of a tree would take that no commit holds (SQUASHTIDY1c), asked of git so no setting hides it:
    /// every tracked change and untracked file (<c>--untracked-files=all</c>, whatever <c>status.showUntrackedFiles</c> says),
    /// and every ignored path (<c>--ignored=matching</c>, a folder ignored whole named once).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>An ignored path the registered checkout holds too goes with the tree</b>: it is what the repository leaves in any
    /// checkout it is built in (installed dependencies, a build's output), and the checkout keeps its own. One only the session's
    /// tree holds (a local database, an export) was made there alone, and nothing says it can be made again, so it keeps the
    /// tree, named. Null is git unable to say, which keeps the tree too.
    /// </remarks>
    private static async Task<TreeHolds?> HoldsAsync(string tree, string root, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(
            tree, ["status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching", "--ignore-submodules=none"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var uncommitted = new List<string>();
        var ignored = new List<string>();
        var entries = porcelain.Split('\0');
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            var path = entry[3..];
            if (entry.StartsWith("!! ", StringComparison.Ordinal))
            {
                var there = Path.Combine(root, path.TrimEnd('/'));
                if (!File.Exists(there) && !Directory.Exists(there)) ignored.Add(path);
                continue;
            }

            uncommitted.Add(path);
            // A rename's or a copy's source follows its entry, and is no path of its own.
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
        }

        return new(uncommitted, ignored);
    }

    /// <summary>What a tree holds that no commit does (SQUASHTIDY1c): uncommitted paths, and ignored paths only it holds.</summary>
    private sealed record TreeHolds(IReadOnlyList<string> Uncommitted, IReadOnlyList<string> Ignored)
    {
        /// <summary>The ignored paths, the first three named, in a clause.</summary>
        public string IgnoredSaid => $"{Ignored.Count} ignored path(s) your checkout does not have: "
            + string.Join(", ", Ignored.Take(3)) + (Ignored.Count > 3 ? $" and {Ignored.Count - 3} more" : "");
    }
}
