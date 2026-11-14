using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The proof a landed branch's work reached the line (out of <c>SessionTrees.Landed.cs</c>): the line's forms
/// this checkout has, the proof by content against them, and the reads of git every landing class shares. Static, since none of it
/// reads the trees' context: the clean-up, the pull request's asks, the content proof, the hand-off, the squash, the sync's judging
/// and the landed review name it by this class.
/// </summary>
internal static class LandedProofs
{
    /// <summary>
    /// One form of the line this checkout has: its name as a person reads it (<c>main</c>, <c>origin/main</c>), and the
    /// reference git is asked by. Never the short name to git: git reads a tag of that name before the branch.
    /// </summary>
    internal sealed record LineForm(string Name, string Ref)
    {
        /// <summary>
        /// The commit it is read at, where <see cref="LineFormsAsync"/> read it: what every proof over it compares
        /// with, and what a removal by such a proof verifies, so the commit verified is the commit proved. Null for a form a caller named.
        /// </summary>
        internal string? At { get; init; }

        /// <summary>What git is asked by: the commit it is read at, else its reference.</summary>
        internal string Rev => At ?? Ref;

        /// <summary>Its name, as every sentence says it.</summary>
        public override string ToString() => Name;
    }

    /// <summary>What the proof found, before anything that keeps a branch is asked.</summary>
    /// <param name="Judgeable">Whether the branch may still be inside another: its line is read and its files are compared.</param>
    internal sealed record Proof(string Kind, string? Where, IReadOnlyList<string> Files, string? Detail, int Commits, bool Judgeable)
    {
        /// <summary>
        /// What a removal by it rests on: the form of the line it found the work on, at the commit it compared.
        /// Empty where it found none, or where the form is a caller's, read at no commit.
        /// </summary>
        internal IReadOnlyList<(string Ref, string Commit)> Rests { get; init; } = [];
    }

    /// <summary>The form at the commit it is read at, as a removal verifies it; none where it is read at none.</summary>
    internal static IReadOnlyList<(string Ref, string Commit)> RestsOn(LineForm? form) => form?.At is { } at ? [(form.Ref, at)] : [];

    /// <summary>The landing's own branches of one repository the record holds, standing or not.</summary>
    internal static List<LandedBranch> EntriesOf(IReadOnlyList<LandedBranch> record, string repository) =>
        // A pattern under `daoris/` would make a session branch's name, which the session branches' own judgement already covers.
        [.. record.Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                  && !entry.Branch.StartsWith("daoris/", StringComparison.Ordinal))];

    /// <summary>The session branches (<c>daoris/…</c>) this checkout holds, by their short names; none where git could not say.</summary>
    /// <param name="refs">The judgement's refs, where it holds them; null asks git.</param>
    internal static async Task<IReadOnlyList<string>> SessionBranchesAsync(string root, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (refs is not null && await refs.BranchesAsync(TreeFollows.SessionPrefix, ct).ConfigureAwait(false) is { } held) return held;
        var (code, output, _) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/"], ct).ConfigureAwait(false);
        return code != 0 ? [] : output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// The line in both its forms that this checkout has: its own branch, and origin's copy — a
    /// platform's squash merge reaches the second before anyone pulls it into the first.
    /// </summary>
    /// <param name="refs">The judgement's refs, where it holds them; null asks git.</param>
    internal static async Task<IReadOnlyList<LineForm>> LineFormsAsync(string root, string? line, CancellationToken ct, RefSnapshot? refs = null) =>
        (await ReadLineFormsAsync(root, line, ct, refs).ConfigureAwait(false)).Forms;

    /// <summary>
    /// <see cref="LineFormsAsync"/> with what it could not read: git's exit 1 from <c>rev-parse --verify --quiet</c> is
    /// "there is no such ref", and any other failure (git missing, a timeout, a repository it cannot open) is a read not
    /// made, which must not read as a line with no forms.
    /// </summary>
    internal static async Task<(IReadOnlyList<LineForm> Forms, string? Unread)> ReadLineFormsAsync(
        string root, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        var forms = new List<LineForm>();
        string? unread = null;
        if (line is null) return (forms, null);
        foreach (var (reference, name) in new[] { ($"refs/heads/{line}", line), ($"refs/remotes/origin/{line}", $"origin/{line}") })
        {
            // The commit is read with the form, so a proof compares with it and a removal verifies it.
            bool? there = null;
            string? at = null;
            if (refs is not null && await refs.CommitAsync(reference, ct).ConfigureAwait(false) is { Known: true } read)
            {
                (there, at) = (read.Commit is not null, read.Commit);
            }

            if (there is null)
            {
                var (code, sha, err) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{reference}^{{commit}}"], ct).ConfigureAwait(false);
                (there, at) = (code == 0, FormOf(name, reference, code, sha)?.At);
                if (code is not (0 or 1)) unread ??= $"git could not look for `{name}`: {(string.IsNullOrWhiteSpace(err) ? "git said nothing" : FirstLine(err))}";
            }

            if (there == true) forms.Add(new LineForm(name, reference) { At = at });
        }

        return (forms, unread);
    }

    /// <summary>The form a <c>rev-parse --verify --quiet</c> of its commit read (Review's graph reads it too): none where git did not answer 0.</summary>
    internal static LineForm? FormOf(string name, string reference, int code, string sha) =>
        code == 0 ? new LineForm(name, reference) { At = sha.Trim() } : null;

    /// <summary>
    /// The proof by content: the files the branch changed since it left the line — both paths of a
    /// rename, and a deletion as a path the line must not hold — compared as blobs with the line, in each of
    /// its forms. A commit of it the line holds already is merged, which needs no content.
    /// </summary>
    internal static async Task<Proof> ProveAsync(string root, string tip, string? line, IReadOnlyList<LineForm> forms, CancellationToken ct)
    {
        if (forms.Count == 0)
        {
            return new(LandedKind.Unknown, null, [], line is null
                ? "no line is set and git names none"
                : $"there is no `{line}` here, nor `origin/{line}`, to compare it with", 0, false);
        }

        // Each form is asked at the commit it is read at (Rev), never by its live name, and the proof names
        // that form and commit as what a removal by it rests on.
        var repository = MachinePorts.Repository;
        foreach (var form in forms)
        {
            if (await repository.IsAncestorAsync(root, tip, form.Rev, ct).ConfigureAwait(false) == true)
            {
                return new(LandedKind.Merged, form.Name, [], null, 0, false) { Rests = RestsOn(form) };
            }
        }

        var commits = await repository.CountAsync(root, tip, [.. forms.Select(form => form.Rev)], ct).ConfigureAwait(false) ?? 0;
        Proof? closest = null;
        string? failed = null;
        foreach (var form in forms)
        {
            if (await repository.MergeBaseAsync(root, tip, form.Rev, ct).ConfigureAwait(false) is not { } shared)
            {
                failed = $"git found no point where it left `{form}`";
                continue;
            }

            var changed = await NamesAsync(root, shared, tip, ct).ConfigureAwait(false);
            var differing = await NamesAsync(root, tip, form.Rev, ct).ConfigureAwait(false);
            if (changed is null || differing is null)
            {
                failed = $"git could not compare it with `{form}`";
                continue;
            }

            if (changed.Count == 0)
            {
                // Commits that change nothing leave no file to find on the line: not proven, and not a loss either.
                failed = "its commits change no file, so there is nothing to find on the line";
                closest ??= new(LandedKind.Unknown, null, [], failed, commits, true);
                continue;
            }

            var still = changed.Where(differing.Contains).ToList();
            if (still.Count == 0) return new(LandedKind.OnLine, form.Name, [], null, commits, false) { Rests = RestsOn(form) };
            if (closest is null || closest.Kind == LandedKind.Unknown || still.Count < closest.Files.Count)
            {
                closest = new(LandedKind.Differs, form.Name, still, null, commits, true);
            }
        }

        return closest ?? new(LandedKind.Unknown, null, [], failed, commits, false);
    }

    /// <summary>The paths that differ between two commits, a rename as its two paths and a submodule's change included; null where git could not say.</summary>
    internal static Task<IReadOnlyList<string>?> NamesAsync(string root, string from, string to, CancellationToken ct) =>
        MachinePorts.Repository.ChangedAsync(root, from, to, ct);

    /// <summary>The landed branch's tip where it stands holding the commit the landing recorded, else null.</summary>
    /// <param name="refs">The judgement's refs, where it holds them; null asks git.</param>
    internal static async Task<string?> StandingTipAsync(string root, LandedBranch entry, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{entry.Branch}", ct, refs).ConfigureAwait(false) is not { } tip) return null;
        return await MachinePorts.Repository.IsAncestorAsync(root, entry.Tip, tip, ct).ConfigureAwait(false) == true ? tip : null;
    }
}
