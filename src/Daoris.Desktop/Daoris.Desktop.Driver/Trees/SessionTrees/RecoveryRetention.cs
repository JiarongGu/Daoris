namespace Daoris.Driver;

/// <summary>A recovery ref the look removed past its retention (CLEANUP1): the ref, the commit it kept, and the branch or tag that holds it.</summary>
public sealed record RecoveryGone(string Ref, string Commit, string Holder);

/// <summary>
/// The recovery refs' retention (CLEANUP1, amending D102's SQUASHTIDY1c note): a ref under <c>refs/daoris/discarded/</c> whose commit a
/// branch or a tag of the person's holds by ancestry keeps nothing no other ref keeps, and goes at the look's tidy once it is past
/// <see cref="Kept"/>; one whose commit nothing else holds is kept until the person deletes it, as before.
/// </summary>
/// <remarks>
/// 🔴 <b>Only the person's refs hold</b> (after review): a tag, or a local branch outside <c>daoris/</c> that no landing of Daoris's made
/// (<see cref="Holds"/>). Never a session branch or a landing branch, which Daoris itself removes on a proof, a squash's record among them,
/// so the same look could remove the holder after the ref; never a remote-tracking ref, which a fetch moves or prunes (SQUASHTIDY1c 6);
/// never another recovery ref. A ref named by hand under the prefix is treated as Daoris's own, since git keeps no word of who made it
/// (no reflog is written outside <c>refs/heads/</c>): it goes only on the same rule, its commit kept by the person's own ref.
/// </remarks>
public static class RecoveryRetention
{
    /// <summary>How long a recovery ref another ref holds is kept: git's own default for an unreachable reflog entry (<c>gc.reflogExpireUnreachable</c>).</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromDays(30);

    /// <summary>
    /// The date from which the look removes it, where a branch or a tag holds its commit and its date was read; null otherwise. Counted from
    /// the commit's date, since git keeps none for a ref: a ref made for an older commit may go sooner after it was made, and loses nothing,
    /// since the holder keeps every commit and message it kept.
    /// </summary>
    public static DateTimeOffset? Until(RecoveryRef each) => each is { Held: true, Dated: { } dated } ? dated + Kept : null;

    /// <summary>The branches Daoris's landings made in <paramref name="repository"/>, standing and traces: never a recovery ref's holder.</summary>
    internal static IReadOnlySet<string> Made(IEnumerable<LandedBranch> landings, string repository) =>
        landings.Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(each => each.Branch).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="reference"/> may hold a recovery ref's commit (after review): a tag, or a local branch outside
    /// <c>daoris/</c> that is not among <paramref name="landed"/>, compared in any case. Nothing else.
    /// </summary>
    internal static bool Holds(string reference, IReadOnlyCollection<string> landed) =>
        reference.StartsWith("refs/tags/", StringComparison.Ordinal)
        || (reference.StartsWith("refs/heads/", StringComparison.Ordinal)
            && !reference.StartsWith($"refs/heads/{TreeFollows.SessionPrefix}", StringComparison.Ordinal)
            && !landed.Contains(reference["refs/heads/".Length..], StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Every recovery ref of one checkout, with its commit, that commit's date, and whether a branch or a tag of the person's holds it
    /// (<see cref="Holds"/>): one <c>for-each-ref</c>, and one <c>rev-list</c> where there are refs. Null with git's words where the list
    /// did not read; where the holders did not read, each ref's <see cref="RecoveryRef.Held"/> is null, never false (D57).
    /// </summary>
    /// <param name="landed">The branches Daoris's landings made here (<see cref="Made"/>): no holders.</param>
    internal static async Task<(IReadOnlyList<RecoveryRef>? Refs, string? Unread)> ReadAsync(
        string root, string repository, IReadOnlyCollection<string> landed, CancellationToken ct)
    {
        var (code, listed, err) = await WorkingTree.GitAsync(
            root, ["for-each-ref", "--format=%(objectname) %(committerdate:unix) %(refname)", ContentHolds.DiscardedPrefix], ct).ConfigureAwait(false);
        if (code != 0) return (null, ContentHolds.Said(err));
        var rows = listed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(row => row.Split(' ', 3)).Where(parts => parts.Length == 3 && WorkingTree.IsCommitId(parts[0])).ToList();
        if (rows.Count == 0) return ([], null);

        // The commits they reach that no branch and no tag of the person's reaches: a ref whose own commit is among them is held by none.
        // Session branches and the landings' branches are left out of `--branches` by name (git's `--exclude`, whose `*` crosses `/`, and
        // a ref name holds no glob character); remote-tracking refs and the recovery refs themselves are never listed (Holds).
        string[] excluded = [$"--exclude={TreeFollows.SessionPrefix}*", .. landed.Select(name => $"--exclude={name}")];
        var (listedCode, unheld, _) = await WorkingTree.GitAsync(
            root, ["rev-list", .. rows.Select(parts => parts[0]).Distinct(StringComparer.Ordinal), "--not", .. excluded, "--branches", "--tags"], ct)
            .ConfigureAwait(false);
        var alone = listedCode == 0
            ? unheld.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal)
            : null;
        return ([.. rows.Select(parts => new RecoveryRef(repository, parts[2])
        {
            Commit = parts[0],
            Dated = long.TryParse(parts[1], out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null,
            Held = alone is null ? null : !alone.Contains(parts[0]),
        })], null);
    }

    /// <summary>
    /// The look's half: each recovery ref past its retention whose commit a branch or a tag of the person's holds (<see cref="Holds"/>),
    /// removed in one ref transaction that verifies that holder at the commit read (<see cref="ContentHolds.DropKeptVerifiedAsync"/>), so a
    /// holder moved off it meanwhile keeps the ref. Nothing else goes: a ref whose commit nothing of the person's holds is never removed
    /// without the person.
    /// </summary>
    /// <param name="landed">The branches Daoris's landings made here (<see cref="Made"/>): no holders.</param>
    internal static async Task<IReadOnlyList<RecoveryGone>> TidyAsync(
        string root, string repository, IReadOnlyCollection<string> landed, DateTimeOffset now, CancellationToken ct)
    {
        var (refs, _) = await ReadAsync(root, repository, landed, ct).ConfigureAwait(false);
        var gone = new List<RecoveryGone>();
        foreach (var each in refs ?? [])
        {
            if (each.Commit is not { } commit || Until(each) is not { } until || until > now) continue;
            if (await ContentHolds.HoldersAsync(root, commit, branch: "", ct).ConfigureAwait(false) is not { } holders) continue;
            var holder = holders.FirstOrDefault(pair => Holds(pair.Ref, landed));
            if (holder.Ref is null) continue;
            if (await ContentHolds.DropKeptVerifiedAsync(root, each.Ref, commit, [holder], ct).ConfigureAwait(false)) gone.Add(new(each.Ref, commit, holder.Ref));
        }

        return gone;
    }
}
