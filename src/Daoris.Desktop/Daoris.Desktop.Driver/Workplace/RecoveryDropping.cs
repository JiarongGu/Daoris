namespace Daoris.Driver;

/// <summary>A recovery ref the person asked to delete, with the commit they were shown it at (RECOVERYDROP1); null where the terminal named it alone.</summary>
public sealed record RecoveryDrop(string Ref, string? Commit);

/// <summary>A recovery ref that was deleted, and the commit it named.</summary>
public sealed record RecoveryDropped(string Ref, string Commit);

/// <summary>A recovery ref that was kept, and why, in a sentence both doors say.</summary>
public sealed record RecoverySkipped(string Ref, string Why);

/// <summary>What a press deleted and what it skipped.</summary>
public sealed record RecoveryDrops(IReadOnlyList<RecoveryDropped> Dropped, IReadOnlyList<RecoverySkipped> Skipped)
{
    /// <summary>Why the press did nothing at all (no lease, no checkout), where it did not get as far as any ref.</summary>
    public string? Refused { get; init; }
}

/// <summary>
/// The person's press to delete the recovery refs nothing holds (RECOVERYDROP1, amending D102's CLEANUP1 note): the one way past
/// CLEANUP1's rule that a recovery ref whose commit no branch or tag of the person's holds is kept at any age. One act, two doors
/// (D50): the screen's <c>RECOVERY_DROP</c> and the terminal's <c>daoris work recovery drop</c>.
/// </summary>
/// <remarks>
/// <para><b>Only what nothing holds, at the moment of the delete.</b> The refs are read again here (<see cref="RecoveryRetention.ReadAsync"/>),
/// never taken from the page's list: a ref a branch or a tag of the person's holds by then is the look's tidy's, which removes it past
/// its retention with that holder verified, so this press never takes what a holder keeps. A ref whose holders git did not read is kept.</para>
///
/// <para><b>Each delete verifies what the person was shown</b>: one <c>update-ref --stdin</c> transaction deleting the ref at the
/// commit it was shown (<see cref="ContentHolds.DropKeptVerifiedAsync"/>'s form), so a ref that moved in between is refused by git and
/// kept. A holder appearing in the instant between the read and the delete is the one case nothing can verify (git cannot assert a ref's
/// absence); the commit then stays reachable from that holder, and what the delete removed is the recovery ref alone.</para>
///
/// <para><b>Only the lease's holder acts</b> (<see cref="WithoutHome"/>), as the end's keeping does (FABRIC-END2).</para>
///
/// <para><b>Transitional (ONEWAY1r, D228):</b> the journal lists every delete with its Undo (<see cref="DeletedBranches"/>) and every keep goes
/// through an act it records (<see cref="RecoveryKeeps"/>), so this press is kept for the recovery refs no journal record names, which its
/// plan counts (<see cref="RecoveryCensus"/>). It ends with <see cref="RecoveryRetention"/>'s reflog reader when that census reads zero in
/// every repository on the install; ONEWAY1af deletes both.</para>
/// </remarks>
public static class RecoveryDropping
{
    /// <summary>The sentence a press refuses with where this process does not hold the home's lease; null where it does.</summary>
    public static string? WithoutHome(string home) => HomeLease.HeldHere(home)
        ? null
        : "this process does not hold the home, so it deletes nothing: the desktop holds it while it runs, so press it there, or close the desktop and run this again.";

    /// <summary>
    /// Delete each asked ref that nothing of the person's holds at this moment, each in one <c>update-ref --stdin</c> transaction that
    /// verifies the ref still names the commit it was shown at. A ref that moved, one a branch or a tag now holds, one whose holders
    /// were not read, one gone already and one outside <c>refs/daoris/discarded/</c> is skipped and said.
    /// </summary>
    public static async Task<RecoveryDrops> DropAsync(
        string root, string repository, IReadOnlyCollection<string> landed, IReadOnlyList<RecoveryDrop> asked, CancellationToken ct,
        IReadOnlyDictionary<string, DateTimeOffset>? recorded = null)
    {
        var dropped = new List<RecoveryDropped>();
        var skipped = new List<RecoverySkipped>();
        var (refs, unread) = await RecoveryRetention.ReadAsync(root, repository, landed, ct, recorded).ConfigureAwait(false);
        foreach (var each in asked.DistinctBy(drop => drop.Ref, StringComparer.Ordinal))
        {
            if (!each.Ref.StartsWith(ContentHolds.DiscardedPrefix, StringComparison.Ordinal))
            {
                skipped.Add(new(each.Ref, "it is no recovery ref (those are under `refs/daoris/discarded/`), so it is not touched here."));
                continue;
            }

            if (refs is null)
            {
                skipped.Add(new(each.Ref, $"git could not list `{repository}`'s recovery refs ({unread}), so it is kept."));
                continue;
            }

            if (refs.FirstOrDefault(read => string.Equals(read.Ref, each.Ref, StringComparison.Ordinal)) is not { Commit: { } commit } now)
            {
                skipped.Add(new(each.Ref, "it is no longer there: it was deleted already."));
                continue;
            }

            if (each.Commit is { } shown && !string.Equals(shown, commit, StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add(new(each.Ref, "it moved since you were shown it, so it is kept: look at the list again."));
                continue;
            }

            if (now.Held != false)
            {
                skipped.Add(new(each.Ref, now.Held == true
                    ? "a branch or a tag of yours holds its commit now, so it is kept; the look's tidy removes it once it is past its days."
                    : "whether a branch or a tag of yours holds its commit was not read, so it is kept."));
                continue;
            }

            // Verified in the delete's own transaction: git refuses it if the ref moved off the commit read in the meantime.
            var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", "--stdin"], $"delete {each.Ref} {commit}\n", ct).ConfigureAwait(false);
            if (code == 0) dropped.Add(new(each.Ref, commit));
            else skipped.Add(new(each.Ref, $"git refused the delete ({ContentHolds.Said(err)}), so it is kept."));
        }

        return new(dropped, skipped);
    }

    /// <summary>
    /// Every recovery ref nothing holds whose work <paramref name="line"/> holds (<see cref="RecoveryWorks"/>: by patch, else by content),
    /// read now and deleted at the commit read. A ref that holds work the line lacks, and every ref where no line is known, is kept and said.
    /// </summary>
    public static async Task<RecoveryDrops> DropUnheldAsync(
        string root, string repository, IReadOnlyCollection<string> landed, string? line, CancellationToken ct, IReadOnlyDictionary<string, DateTimeOffset>? recorded = null)
    {
        var (refs, unread) = await RecoveryRetention.ReadAsync(root, repository, landed, ct, recorded).ConfigureAwait(false);
        if (refs is null) return new([], [new(ContentHolds.DiscardedPrefix, $"git could not list `{repository}`'s recovery refs ({unread}), so none is deleted.")]);
        var unheld = refs.Where(each => each.Held == false && each.Commit is not null).ToList();
        var held = new List<RecoverySkipped>();
        var asked = new List<RecoveryDrop>();
        foreach (var each in unheld)
        {
            var work = line is null ? null : await RecoveryWorks.ReadAsync(root, line, each.Commit!, ct).ConfigureAwait(false);
            if (work is { OnLine: true }) asked.Add(new(each.Ref, each.Commit));
            else held.Add(new(each.Ref, work is null ? NoLine : $"{work.Words}, so it is kept."));
        }

        var dropped = await DropAsync(root, repository, landed, asked, ct, recorded).ConfigureAwait(false);
        return new([.. dropped.Dropped], [.. held, .. dropped.Skipped]);
    }

    /// <summary>Why every unheld ref is kept where the repository's line is not known here.</summary>
    internal const string NoLine = "the repository's line is not known here (no line set, and no `main` or `master`), so its work cannot be compared with it and it is kept.";

    /// <summary>
    /// The press of both doors: refused unless this process holds the home, else <paramref name="asked"/> or, where null, every unheld ref
    /// of <paramref name="repository"/> whose work <paramref name="line"/> holds (<see cref="DropUnheldAsync"/>), in its checkout
    /// <paramref name="root"/> (null where it has none here).
    /// </summary>
    public static async Task<RecoveryDrops> PressAsync(
        string home, string? root, string repository, IReadOnlyList<RecoveryDrop>? asked, CancellationToken ct, string? line = null)
    {
        if (WithoutHome(home) is { } refused) return new([], []) { Refused = refused };
        if (root is null) return new([], []) { Refused = $"`{repository}` has no checkout on this machine, so its recovery refs cannot be deleted here." };
        var landed = RecoveryRetention.Made(new LandedBranches(home).Entries(), repository);
        var recorded = RecoveryKeeps.Recorded(home, repository);
        return asked is null
            ? await DropUnheldAsync(root, repository, landed, line, ct, recorded).ConfigureAwait(false)
            : await DropAsync(root, repository, landed, asked, ct, recorded).ConfigureAwait(false);
    }

    /// <summary>
    /// What a press would do now, a line each recovery ref of <paramref name="repository"/> (<c>--plan</c>): for an unheld ref its age and
    /// commit count and what its work is to <paramref name="line"/> (<see cref="RecoveryWorks"/>), then the census of those no journal
    /// record names (<see cref="RecoveryCensus"/>). It changes nothing, so it needs no lease.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PlanAsync(string home, string root, string repository, string? line, CancellationToken ct)
    {
        var (refs, unread) = await RecoveryRetention.ReadAsync(
            root, repository, RecoveryRetention.Made(new LandedBranches(home).Entries(), repository), ct, RecoveryKeeps.Recorded(home, repository)).ConfigureAwait(false);
        var census = RecoveryCensus.Words(await RecoveryCensus.CountAsync(home, root, repository, ct).ConfigureAwait(false));
        if (refs is null) return [$"git could not list `{repository}`'s recovery refs ({unread}).", census];
        if (refs.Count == 0) return [$"`{repository}` has no recovery ref.", census];

        var now = DateTimeOffset.UtcNow;
        var lines = new List<string>();
        foreach (var each in refs)
        {
            if (each.Held != false || each.Commit is null)
            {
                lines.Add(each.Held == true
                    ? $"would keep `{each.Ref}` in `{repository}`: a branch or a tag of yours holds its commit."
                    : $"would keep `{each.Ref}` in `{repository}`: whether a branch or a tag of yours holds its commit was not read.");
                continue;
            }

            var work = line is null ? null : await RecoveryWorks.ReadAsync(root, line, each.Commit, ct).ConfigureAwait(false);
            var facts = string.Join(", ", new[] { RecoveryWorks.Age(each.Dated, now), work is null ? null : RecoveryWork.Count(work.Commits, "commit") }.OfType<string>());
            lines.Add($"would {(work is { OnLine: true } ? "delete" : "keep")} `{each.Ref}` in `{repository}`{(facts.Length > 0 ? $" ({facts})" : "")}: "
                + (work is null ? NoLine : work.Words) + ".");
        }

        lines.Add(census);
        return lines;
    }

    /// <summary>The words of an answer, a line each: what was deleted and what was skipped and why.</summary>
    public static IEnumerable<string> Words(string repository, RecoveryDrops drops)
    {
        if (drops.Refused is { } refused)
        {
            yield return refused;
            yield break;
        }

        if (drops.Dropped.Count == 0 && drops.Skipped.Count == 0) yield return $"nothing to delete in `{repository}`: no recovery ref there is held by nothing.";
        foreach (var each in drops.Dropped)
        {
            yield return $"deleted `{each.Ref}` from `{repository}` (it named {TextBounds.ShortCommit(each.Commit, 8)}): its commit may now be lost from this machine once git collects it.";
        }

        foreach (var each in drops.Skipped) yield return $"kept `{each.Ref}` in `{repository}`: {each.Why}";
    }
}
