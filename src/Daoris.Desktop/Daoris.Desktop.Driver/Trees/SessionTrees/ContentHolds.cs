using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeWorktrees;

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

/// <summary>
/// What the content proof found (PROOF-UNREAD1): the work held (<see cref="Hold"/>), not held, or unread (<see cref="Unread"/>, git's
/// failure in a clause) where a read it needs failed. Unread is never held and never "not held": a caller keeps what it would keep
/// for either, and says it could not read.
/// </summary>
/// <param name="Hold">Where the work is held; null where it is not, or where <paramref name="Unread"/> says git could not tell.</param>
/// <param name="Unread">Why git could not tell, or null where the proof was read through.</param>
internal sealed record ContentProof(ContentHold? Hold, string? Unread)
{
    /// <summary>The proof read through and found nothing holding the work.</summary>
    public static readonly ContentProof NotHeld = new(null, null);

    /// <summary>
    /// The ref the work was found held on, at the commit the proof compared (FABRIC-END4a, third review): what a removal by this proof rests
    /// on, and what its delete verifies. Null where the work is not held.
    /// </summary>
    internal (string Ref, string Commit)? At { get; init; }

    /// <summary>What a removal by it rests on: <see cref="At"/>, or nothing.</summary>
    internal IReadOnlyList<(string Ref, string Commit)> Rests => At is { } at ? [at] : [];

    /// <summary>The work is held, on <paramref name="reference"/> at <paramref name="commit"/>.</summary>
    public static ContentProof Of(ContentHold hold, string reference, string commit) => new(hold, null) { At = (reference, commit) };

    /// <summary>A read failed, and nothing found the work held.</summary>
    public static ContentProof UnreadBy(string why) => new(null, why);
}

/// <summary>
/// The content proof and what a removal keeps and deletes by it (SQUASHTIDY1, SQUASHTIDY1c; PART10l, out of
/// <c>SessionTrees.Content.cs</c>): where a branch's work is held by content, what a tree holds that no commit does, the
/// recovery ref a content-held removal keeps, and the delete at the commit judged. Over the trees' context for the record of
/// what landings made, the delete's test seam and the working trees git lists; the rest is static.
/// </summary>
internal sealed class ContentHolds(TreeContext context)
{
    /// <summary>
    /// Where a content-held removal keeps what it deleted (SQUASHTIDY1c): one ref per deletion, under the branch's own name, out
    /// of every proof's sight (<c>--branches</c> and <c>--remotes</c> never read it), and never deleted by Daoris.
    /// </summary>
    internal const string DiscardedPrefix = "refs/daoris/discarded/";

    /// <summary>How many names a branch's recovery refs take before a discard gives up (SQUASHTIDY1c), the offer naming them alike (SQUASHTIDY1b).</summary>
    internal const int DiscardedNames = 20;

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
    /// <para>🔴 <b>Nothing held keeps the refusal</b>: commits that change no file leave nothing to find (D102), and a comparison git
    /// could not make proves nothing, and says so (<see cref="ContentProof.Unread"/>, PROOF-UNREAD1) rather than reading as not held. The paths are <c>--no-renames</c>, so a rename is both its paths and a deletion is a path the
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
    /// <param name="refs">The judgement's refs (PROC1d-ii), the repository's whichever tree <paramref name="cwd"/> is; null asks git.</param>
    internal static async Task<ContentProof> HeldByContentAsync(
        string cwd, string tip, string? line, IReadOnlySet<string> notHolders, CancellationToken ct, RefSnapshot? refs = null)
    {
        // PROOF-UNREAD1: a read git could not make is remembered, so no failure ends as "not held"; a hold found by other
        // reads still stands, since it is found, not assumed.
        var (lineForms, unread) = await LandedProofs.ReadLineFormsAsync(cwd, line, ct, refs).ConfigureAwait(false);
        var forms = new List<(string Name, string Ref, string Id)>();
        foreach (var form in lineForms)
        {
            if ((form.At ?? await TreeRefs.CommitOfAsync(cwd, form.Ref, ct, refs).ConfigureAwait(false)) is { } formId) forms.Add((form.Name, form.Ref, formId));
            else unread ??= $"git could not read `{form.Name}`";
        }

        if (forms.Count == 0) return unread is null ? ContentProof.NotHeld : ContentProof.UnreadBy(unread);
        var (treeCode, tree, treeErr) = await WorkingTree.GitAsync(cwd, ["rev-parse", "--verify", "--quiet", $"{tip}^{{tree}}"], ct)
            .ConfigureAwait(false);
        if (treeCode != 0) return ContentProof.UnreadBy($"git could not read the branch's tree: {Said(treeErr)}");

        IReadOnlyList<string>? changed = null;
        foreach (var (form, formRef, formId) in forms)
        {
            var (baseCode, mergeBase, baseErr) = await WorkingTree.GitAsync(cwd, ["merge-base", tip, formId], ct).ConfigureAwait(false);
            if (baseCode != 0)
            {
                // Exit 1 is git's answer that they share no commit; anything else is a read that failed.
                if (baseCode != 1) unread ??= $"git could not find where the branch left `{form}`: {Said(baseErr)}";
                continue;
            }

            var paths = await LandedProofs.NamesAsync(cwd, mergeBase.Trim(), tip, ct).ConfigureAwait(false);
            if (paths is null)
            {
                unread ??= $"git could not list the files the branch changed since it left `{form}`";
                continue;
            }

            if (paths.Count == 0) continue;
            changed ??= paths;

            var (logCode, trees, logErr) = await WorkingTree.GitAsync(cwd, ["log", "--format=%T", $"{mergeBase.Trim()}..{formId}"], ct)
                .ConfigureAwait(false);
            if (logCode != 0) unread ??= $"git could not read `{form}`'s commits: {Said(logErr)}";
            else if (trees.Split('\n', StringSplitOptions.TrimEntries).Contains(tree.Trim(), StringComparer.Ordinal))
            {
                return ContentProof.Of(new ContentHold(form, Squash: true), formRef, formId);
            }

            switch (await SameFilesAsync(cwd, tip, formId, paths, ct).ConfigureAwait(false))
            {
                case true: return ContentProof.Of(new ContentHold(form, Squash: false), formRef, formId);
                case null: unread ??= $"git could not compare the branch's files with `{form}`"; break;
            }
        }

        if (changed is null) return unread is null ? ContentProof.NotHeld : ContentProof.UnreadBy(unread);
        // Newest first: a cherry-pick is a recent commit, so the holder is usually found before the rest are compared.
        var (refsCode, byDate, refsErr) = await WorkingTree.GitAsync(
            cwd, ["for-each-ref", "--sort=-committerdate", "--format=%(objectname) %(refname)", "refs/heads/"], ct).ConfigureAwait(false);
        if (refsCode != 0) return ContentProof.UnreadBy(unread ?? $"git could not list the branches: {Said(refsErr)}");
        foreach (var row in byDate.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = row.IndexOf(' ');
            if (space <= 0) continue;
            var (id, branch) = (row[..space], row[(space + 1)..]["refs/heads/".Length..]);
            if (branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal) || forms.Any(form => form.Name == branch)
                || notHolders.Contains(branch))
            {
                continue;
            }

            switch (await SameFilesAsync(cwd, tip, id, changed, ct).ConfigureAwait(false))
            {
                case true: return ContentProof.Of(new ContentHold(branch, Squash: false), $"refs/heads/{branch}", id);
                case null: unread ??= $"git could not compare the branch's files with `{branch}`"; break;
            }
        }

        return unread is null ? ContentProof.NotHeld : ContentProof.UnreadBy(unread);
    }

    /// <summary>Git's own words for a failure, or that it said none.</summary>
    /// <summary>git's words as a clause, its first line, or that it said nothing.</summary>
    internal static string Said(string error) => string.IsNullOrWhiteSpace(error) ? "git said nothing" : FirstLine(error);

    /// <summary>Whether every one of <paramref name="paths"/> reads on <paramref name="holder"/> as on <paramref name="tip"/>; null where git could not say.</summary>
    private static async Task<bool?> SameFilesAsync(string cwd, string tip, string holder, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var differing = await LandedProofs.NamesAsync(cwd, tip, holder, ct).ConfigureAwait(false);
        if (differing is null) return null;
        var differs = new HashSet<string>(differing, StringComparer.Ordinal);
        return !paths.Any(differs.Contains);
    }

    /// <summary>The branches a landing made and recorded in <paramref name="repository"/>: never a holder by content (SQUASHTIDY1c).</summary>
    internal IReadOnlySet<string> LandedHere(string repository) =>
        context.Recorded.All()
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Branch)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Keep the commit a content-held removal is about to delete on a ref of its own (SQUASHTIDY1c), before anything goes: the
    /// proof compared files, and a commit's message, an empty commit and history the final tree does not show are held nowhere
    /// else. One ref per deletion, under the branch's name, a number after it where that name holds another commit already.
    /// </summary>
    /// <returns>The ref, or null with git's reason; then nothing may be removed.</returns>
    internal static async Task<(string? Reference, string? Why)> KeepDiscardedAsync(
        string root, string branch, string judged, CancellationToken ct, string? absent = null)
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
    /// <remarks>
    /// 🔴 <b>Kept until the commit is shown protected</b> (FABRIC-END4a, second review): the branch's commit is read, shown to hold the
    /// commit judged, and verified at that commit in the same ref transaction that deletes the recovery ref, so a branch gone, never put
    /// back, or made again elsewhere as the ref goes keeps the ref.
    /// </remarks>
    internal static async Task DropKeptAsync(string root, string branch, string? reference, string judged, CancellationToken ct)
    {
        if (reference is null) return;
        var (stands, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false);
        if (stands != 0) return;
        var tip = at.Trim();
        var (held, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", judged, tip], ct).ConfigureAwait(false);
        if (held != 0) return;
        await WorkingTree.GitAsync(root, ["update-ref", "--stdin"], $"verify refs/heads/{branch} {tip}\ndelete {reference} {judged}\n", ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A detached tree's recovery ref goes (DETACHTX1) only in one ref transaction that verifies what its proof rested on, each ref at the
    /// commit the proof read it at, as a judged delete verifies them: a holder rewound or replaced as the tree went refuses the whole, and
    /// the ref stays, the one ref left holding the commit the tree had out.
    /// </summary>
    /// <returns>True where the ref went; false where it stays, git having refused or nothing having been named to verify.</returns>
    internal static async Task<bool> DropKeptVerifiedAsync(
        string root, string reference, string judged, IReadOnlyList<(string Ref, string Commit)> held, CancellationToken ct)
    {
        if (held.Count == 0) return false;
        var (code, _, _) = await WorkingTree.GitAsync(root, ["update-ref", "--stdin"], Verified(held, $"delete {reference} {judged}\n"), ct)
            .ConfigureAwait(false);
        return code == 0;
    }

    /// <summary>
    /// A ref transaction's input (FABRIC-END4a, second review): a <c>verify</c> for each ref and commit the proof rested on, named once
    /// where two parts of a proof rest on the same ref at the same commit, then <paramref name="change"/>.
    /// </summary>
    private static string Verified(IReadOnlyList<(string Ref, string Commit)> held, string change) =>
        string.Concat(held.Distinct().Select(each => $"verify {each.Ref} {each.Commit}\n")) + change;

    /// <summary>
    /// Whether git says <paramref name="reference"/> is missing (RECORDUNREAD1, FABRIC-END4a's fourth review): asked where a read of it
    /// failed, or a list left it out. <c>rev-parse --verify --quiet</c> exits 1 on any failure to resolve, a broken ref's too, and
    /// <c>for-each-ref</c> leaves a broken ref out of its list with a warning and exits 0; <c>show-ref --exists</c> exits 2 for a missing
    /// ref alone (git 2.43 and later; the knowledge note <c>git-ref-absence</c>). Only that forgets or retires a record.
    /// </summary>
    /// <returns>Missing only on git's own word; otherwise false, with git's words as a clause for the unread sentence. A git older than 2.43
    /// has no <c>--exists</c>, so there a missing ref reads as unread: kept, never the other way.</returns>
    internal static async Task<(bool Missing, string Said)> MissingAsync(string root, string reference, CancellationToken ct)
    {
        var (code, _, err) = await WorkingTree.GitAsync(root, ["show-ref", "--exists", reference], ct).ConfigureAwait(false);
        return (code == 2, Said(err));
    }

    /// <summary>
    /// The refs that hold <paramref name="tip"/> by ancestry, each at the commit this read found it at (FABRIC-END4a, third review): the read
    /// a proof by ancestry makes, which picks its holder from them (<see cref="HolderOf"/>) and returns it for the delete to verify. Never
    /// <paramref name="branch"/> itself, a symbolic <c>HEAD</c>, or Daoris's own recovery refs.
    /// </summary>
    /// <returns>Null where git could not read them, and then nothing is proven.</returns>
    internal static async Task<IReadOnlyList<(string Ref, string Commit)>?> HoldersAsync(string root, string tip, string branch, CancellationToken ct)
    {
        var (code, containing, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--contains", tip, "--format=%(objectname) %(refname)"], ct).ConfigureAwait(false);
        return code != 0 ? null
            : [.. Pairs(containing).Where(pair => pair.Ref != $"refs/heads/{branch}" && !pair.Ref.EndsWith("/HEAD", StringComparison.Ordinal) && AnyRef(pair.Ref))];
    }

    /// <summary>
    /// The holder a proof by ancestry rests on (FABRIC-END4a, third review), out of what <see cref="HoldersAsync"/> read: a form of
    /// <paramref name="line"/> first, its own branch then origin's copy; else the first ref <paramref name="admit"/> takes, a branch of the
    /// person's before any other, since another session branch may go in the same pass. Null where none holds it: the holder the proof found
    /// is gone, and that is a refusal, never a fallback to a line that never held the tip.
    /// </summary>
    internal static (string Ref, string Commit)? HolderOf(IReadOnlyList<(string Ref, string Commit)> holders, string? line, Func<string, bool> admit)
    {
        foreach (var form in line is null ? [] : new[] { $"refs/heads/{line}", $"refs/remotes/origin/{line}" })
        {
            if (holders.FirstOrDefault(pair => pair.Ref == form) is { Ref: not null } on) return on;
        }

        var admitted = holders.Where(pair => admit(pair.Ref)).ToList();
        return admitted.Where(pair => PersonsRef(pair.Ref)).Concat(admitted).FirstOrDefault() is { Ref: not null } first ? first : null;
    }

    /// <summary>A ref as a person names it: a branch's or a remote-tracking branch's short name.</summary>
    internal static string Named(string reference) =>
        reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..]
        : reference.StartsWith("refs/remotes/", StringComparison.Ordinal) ? reference["refs/remotes/".Length..]
        : reference;

    private static IEnumerable<(string Ref, string Commit)> Pairs(string listed) =>
        listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(row => row.Split(' ', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => (parts[1], parts[0]));

    /// <summary>D88's holder: a branch of the person's, local outside <c>daoris/</c> or remote-tracking outside <c>*/daoris/</c>.</summary>
    internal static bool PersonsRef(string reference) =>
        (reference.StartsWith("refs/heads/", StringComparison.Ordinal) && !reference.StartsWith($"refs/heads/{TreeFollows.SessionPrefix}", StringComparison.Ordinal))
        || (reference.StartsWith("refs/remotes/", StringComparison.Ordinal) && !reference.Contains($"/{TreeFollows.SessionPrefix}", StringComparison.Ordinal));

    /// <summary>Any ref but Daoris's own recovery refs, which a delete makes for itself.</summary>
    internal static bool AnyRef(string reference) => !reference.StartsWith(DiscardedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Delete a branch a judgement cleared, only while it is still the commit judged (SQUASHTIDY1c): <c>git update-ref -d</c>
    /// with that id, which git refuses once the ref moved, so a commit made after the judgement never goes with it. Where the
    /// branch is gone already, nothing is left to delete.
    /// </summary>
    /// <returns>Null where it is deleted; otherwise why it is kept, in a clause.</returns>
    /// <remarks>
    /// <para>🔴 <b>One ref transaction</b> (FABRIC-END4a, second review): <c>git update-ref --stdin</c> holds a <c>verify</c> for every ref
    /// in <paramref name="held"/> at the commit the proof read and the <c>delete</c> at the commit judged, and git checks them all under its
    /// locks, so a line rewound or replaced as the branch goes refuses the whole: the branch never leaves, and no look, page or session can
    /// observe it absent. The transaction and what follows it are never cancelled, since a removal stopped halfway is one nobody judged.</para>
    ///
    /// <para><b>The one window no ref says: a checkout made after the trees were read.</b> So the trees are read again once it went. A tree
    /// on the branch, or a list git could not give, puts it back at the commit judged, only where nothing stands at its name; a put-back
    /// git refuses (a lock, a branch made meanwhile) is said as such, and the caller keeps any recovery ref, since
    /// <see cref="DropKeptAsync"/> drops one only while the branch is shown to hold the commit.</para>
    /// </remarks>
    /// <param name="held">What the proof rested on, each ref at the commit the proof read it at, as the proof returned it (FABRIC-END4a, third
    /// review): the holder by ancestry, the ref holding the work by content, a squashing landing's branch, the landing a landed branch is
    /// inside and what that one rests on, the form of the line a merge or a pull request's merge was found on. Verified exactly, and never
    /// looked for again here. Empty is no proof, and keeps the branch.</param>
    internal async Task<string?> DeleteJudgedAsync(
        string root, string branch, string judged, IReadOnlyList<(string Ref, string Commit)> held, CancellationToken ct)
    {
        if (held.Count == 0) return "nothing its proof rested on was named, so its proof cannot be checked as it goes";
        if (context.BeforeDeleting is { } seam) await seam(branch).ConfigureAwait(false);
        // git branch -D refuses a branch a working tree has checked out; update-ref does not ask, so this does. A list git could
        // not give is no answer to that (AUTOTIDY1).
        if (await context.Worktrees.ReadAsync(root, ct).ConfigureAwait(false) is not { } worktrees)
        {
            return $"{ListUnread}, so whether a tree has it checked out is not known";
        }

        if (worktrees.GetValueOrDefault(branch) is { } checkedOut)
        {
            return $"it is checked out at {checkedOut}";
        }

        if (context.BeforeRefDeleted is { } late) await late(branch).ConfigureAwait(false);
        var none = CancellationToken.None;
        var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", "--stdin"], Verified(held, $"delete refs/heads/{branch} {judged}\n"), none)
            .ConfigureAwait(false);
        if (code != 0) return await RefusedAsync(root, branch, judged, held, err).ConfigureAwait(false);

        // The late checkout, which no ref says: a tree on the branch, or a list git could not give, is not clear.
        var trees = await context.Worktrees.ReadAsync(root, none).ConfigureAwait(false);
        var lateTree = trees?.GetValueOrDefault(branch);
        if (trees is null || lateTree is not null)
        {
            return await PutBackAsync(root, branch, judged, trees is null
                ? $"{ListUnread} once it went, so whether a tree checked it out as it went is not known"
                : $"it was checked out at {lateTree} as it went").ConfigureAwait(false);
        }

        // What git branch -D also removes: the branch's own section of the config, its upstream where it had one.
        await WorkingTree.GitAsync(root, ["config", "--remove-section", $"branch.{branch}"], none).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Why git refused the transaction (FABRIC-END4a, second review): the branch gone already (nothing left to delete, null), moved since
    /// it was judged, or a ref the proof rested on no longer at its commit; else git's own words. Nothing was deleted.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Gone only on git's own word</b> (third review): any failure to read is never "gone": the branch is said kept, unread, and the
    /// caller keeps any recovery ref, since <see cref="DropKeptAsync"/> drops one only where the branch is shown to hold its commit. Exit 1
    /// from <c>rev-parse --verify --quiet</c> is any failure to resolve, a broken ref's too (fourth review), so absence is asked of
    /// <c>show-ref --exists</c>, whose exit 2 alone says the ref is missing. A git older than 2.43 has no <c>--exists</c>, and its refusal
    /// reads as unread: the branch said kept where it went, never the other way.
    /// </remarks>
    private static async Task<string?> RefusedAsync(string root, string branch, string judged, IReadOnlyList<(string Ref, string Commit)> held, string err)
    {
        var none = CancellationToken.None;
        var (exists, now, readErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], none).ConfigureAwait(false);
        var unread = Said(readErr);
        if (exists == 1)
        {
            var (missing, said) = await MissingAsync(root, $"refs/heads/{branch}", none).ConfigureAwait(false);
            if (missing) return null;
            unread = said;
        }

        if (exists != 0)
        {
            return $"git would not delete it ({FirstLine(err)}), and could not read whether it still stands ({unread}), so it is kept as unread";
        }

        if (now.Trim() != judged) return MovedSince;
        foreach (var (reference, commit) in held)
        {
            var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], none).ConfigureAwait(false);
            if (code != 0 || at.Trim() != commit) return $"`{Named(reference)}` moved as it went, so what its proof rested on no longer stands";
        }

        return $"git would not delete it: {FirstLine(err)}";
    }

    /// <summary>
    /// The branch made again at the commit judged after a late checkout (FABRIC-END4a review), only where nothing stands at its name, the
    /// empty old value being git's "must not exist"; git's answer read, so a lock or a branch made meanwhile is said as such.
    /// </summary>
    private static async Task<string> PutBackAsync(string root, string branch, string judged, string why)
    {
        var none = CancellationToken.None;
        var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", $"refs/heads/{branch}", judged, ""], none).ConfigureAwait(false);
        if (code == 0) return $"{why}, so it was put back where it was";
        var (exists, now, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], none).ConfigureAwait(false);
        // Another restorer made it at the same commit: it stands where it was.
        if (exists == 0 && now.Trim() == judged) return $"{why}, so it was put back where it was";
        var standing = exists == 0 ? $"a branch of that name was made at {Short(now.Trim())} meanwhile" : FirstLine(err);
        return $"{why}, and Daoris could not put it back at {Short(judged)} ({standing}); its commits stay where its proof found them held";
    }

    private static string Short(string commit) => commit.Length > 12 ? commit[..12] : commit;

    /// <summary>
    /// What an unforced removal of a tree would take that no commit holds (SQUASHTIDY1c), asked of git so no setting hides it:
    /// every tracked change and untracked file (<c>--untracked-files=all</c>, whatever <c>status.showUntrackedFiles</c> says),
    /// and every ignored path (<c>--ignored=matching</c>, a folder ignored whole named once).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>An ignored path the registered checkout holds too goes with the tree</b>: it is what the repository leaves in any
    /// checkout it is built in (installed dependencies, a build's output), and the checkout keeps its own. One only the session's
    /// tree holds (a local database, an export) was made there alone, and nothing says it can be made again, so it keeps the
    /// tree, named. Null is git unable to say, which keeps the tree too. 🔴 <b>Except a session's own scratch in such a folder</b>
    /// (TREEKEEP1): under a build folder (<see cref="TreeScratch.BuildFolders"/>) the path goes as ever; under any other, each file the tree
    /// wrote after it opened that the checkout does not hold as written (absent, or different bytes) is the tree's own, as an ignored path only it holds is.
    /// </remarks>
    internal static async Task<TreeHolds?> HoldsAsync(string tree, string root, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(
            tree, ["status", "--porcelain", "-z", "--untracked-files=all", "--ignored=matching", "--ignore-submodules=none"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var uncommitted = new List<string>();
        var ignored = new List<string>();
        var shared = new List<string>();
        var written = new List<string>();
        var tooLarge = new List<string>();
        // When the tree opened: its folder was made by the worktree add and nothing since recreates it (TREEKEEP1).
        var opened = Directory.GetCreationTimeUtc(tree);
        var entries = porcelain.Split('\0');
        for (var at = 0; at < entries.Length; at++)
        {
            var entry = entries[at];
            if (entry.Length < 4) continue;
            var path = entry[3..];
            if (entry.StartsWith("!! ", StringComparison.Ordinal))
            {
                var there = Path.Combine(root, path.TrimEnd('/'));
                if (!File.Exists(there) && !Directory.Exists(there))
                {
                    ignored.Add(path);
                }
                else if (path.EndsWith('/') && !TreeScratch.UnderBuildFolder(path))
                {
                    // Not a build's folder: what the session wrote in it is its own (TREEKEEP1), unless the checkout holds it as written.
                    var mine = TreeScratch.WrittenSince(tree, root, path, opened, out var large);
                    if (large) tooLarge.Add(path);
                    else if (mine.Count > 0) written.AddRange(mine);
                    else shared.Add(path);
                }
                else
                {
                    shared.Add(path);
                }

                continue;
            }

            uncommitted.Add(path);
            // A rename's or a copy's source follows its entry, and is no path of its own.
            if (entry[0] is 'R' or 'C' || entry[1] is 'R' or 'C') at++;
        }

        return new(uncommitted, ignored, shared, written, tooLarge);
    }

    /// <summary>
    /// What a tree holds that no commit does (SQUASHTIDY1c): uncommitted paths, and ignored paths only it holds; and the ignored
    /// paths the checkout holds too, which a press lets go with the tree. The look's own tidy moves the folder aside whole, so for
    /// it no ignored path keeps the tree (AUTOTIDY2a).
    /// </summary>
    /// <remarks>
    /// <paramref name="Written"/> and <paramref name="TooLarge"/> are a shared ignored folder's own part (TREEKEEP1): files the
    /// session wrote there after the tree opened that the checkout does not hold as written, and folders too large to look through. They
    /// are no longer in <paramref name="Shared"/>: they keep the tree like <paramref name="Ignored"/> does, which is
    /// <see cref="OwnSaid"/>.
    /// </remarks>
    internal sealed record TreeHolds(
        IReadOnlyList<string> Uncommitted, IReadOnlyList<string> Ignored, IReadOnlyList<string> Shared,
        IReadOnlyList<string> Written, IReadOnlyList<string> TooLarge)
    {
        /// <summary>The ignored paths, the first three named, in a clause.</summary>
        public string IgnoredSaid => $"{Ignored.Count} ignored path(s) your checkout does not have: "
            + string.Join(", ", Ignored.Take(3)) + (Ignored.Count > 3 ? $" and {Ignored.Count - 3} more" : "");

        /// <summary>The session's own files in a shared ignored folder, the first three named, in a clause (TREEKEEP1).</summary>
        public string? WrittenSaid
        {
            get
            {
                var said = new List<string>();
                if (Written.Count > 0)
                {
                    said.Add($"{Written.Count} file(s) written in the tree after it opened, which your checkout does not hold as written: "
                        + string.Join(", ", Written.Take(3)) + (Written.Count > 3 ? $" and {Written.Count - 3} more" : ""));
                }

                if (TooLarge.Count > 0)
                {
                    said.Add($"{TooLarge.Count} ignored folder(s) too large to look through, which count as the tree's own: "
                        + string.Join(", ", TooLarge.Take(3)) + (TooLarge.Count > 3 ? $" and {TooLarge.Count - 3} more" : ""));
                }

                return said.Count == 0 ? null : string.Join(" and ", said);
            }
        }

        /// <summary>Everything no checkout holds that the tree does and nothing re-makes, in a clause; null where it holds none.</summary>
        public string? OwnSaid => (Ignored.Count > 0, WrittenSaid) switch
        {
            (false, null) => null,
            (true, null) => IgnoredSaid,
            (false, { } written) => written,
            (true, { } written) => $"{IgnoredSaid}, and {written}",
        };
    }
}
