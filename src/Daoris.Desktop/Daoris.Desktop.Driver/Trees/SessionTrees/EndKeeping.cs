namespace Daoris.Driver;

/// <summary>
/// The end judgement's one writer (FABRIC-END2; the fabric events design §B5 and §C7): for one repository, each standing landing branch
/// and each standing session branch judged through <see cref="EndJudgement"/>, and <see cref="HeldFact"/> written on its entry where the
/// line holds it, kept while it still holds, and cleared where it stopped holding. Called where the look's tidy and <i>Accept</i>'s tidy
/// judge the work (<see cref="AutoTidies"/>, <see cref="TreeLands"/>), before either removes anything, so an ended entry outlives its branch
/// as a trace (<see cref="SessionBranches.Forget"/>).
/// </summary>
/// <remarks>
/// <para>🔴 <b>Only the lease's holder writes</b> (canary 9): a process that does not hold the home's lease, which is a terminal's verb
/// beside a running instance, writes nothing here. A verb alone, under the lease it took, may. The two records' edits are serialized
/// across processes by their lock (<see cref="RecordLock"/>) whoever makes them.</para>
///
/// <para><b>A fact lapses on a standing branch only</b> (canary 7): its tip moved, its cycle names another line, its line's form no longer
/// descends from the commit it held at (rewound or replaced), or the repository at the path is another. It is then judged again: held
/// anew, or cleared, which a reader reads as not known. A trace is never judged again. A fact that still holds is not written again, so a
/// look that changes nothing writes nothing.</para>
///
/// <para><b>Nothing is removed here</b> (§B8), and no reader reads it yet (MILESTONE1a does). <i>Ended</i> is derived at read from the
/// kept fact and the effective cycle, never stored.</para>
/// </remarks>
internal sealed class EndKeeping(TreeContext context, PullRequestAsks asks)
{
    /// <summary>Judge one repository's standing branches and keep what the line holds; how many entries were written comes back.</summary>
    /// <param name="root">The repository's main checkout, whose common dir is its <c>.git</c> folder.</param>
    /// <param name="line">The line the cycle names (D86), read by the caller as its own judgement reads it.</param>
    /// <param name="refs">The caller's refs (PROC1d-ii), where it holds them; null takes them here.</param>
    internal async Task<int> KeepAsync(
        string root, string repository, string workspace, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (!HomeLease.HeldHere(context.Home)) return 0;
        // The repository judged, by its common dir's identity: a folder that cannot be read is no repository to keep an end for.
        if (FolderIdentity.Of(Path.Combine(root, ".git")) is not { } identity) return 0;
        refs ??= RefSnapshot.Take(root);
        var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);
        var standing = context.Recorded.All();
        var landings = LandedProofs.EntriesOf(standing, repository);
        var notHolders = landings.Select(entry => entry.Branch).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<PullRequestCarrier>? carriers = null;
        DriverConfig? config = null;
        var now = DateTimeOffset.UtcNow;
        var written = 0;

        foreach (var entry in landings)
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{entry.Branch}", ct, refs).ConfigureAwait(false) is not { } tip) continue;
            if (entry.Held is { } kept && await HoldsAsync(kept, tip).ConfigureAwait(false)) continue;
            carriers ??= await asks.CarriersAsync(root, repository, line, ct, refs).ConfigureAwait(false);
            var verdict = await EndJudgement.JudgeAsync(
                root, EndCycle.Recorded(entry), new EndSubject(entry.Branch, tip, EndRole.Landing, entry), line, notHolders, carriers, ct, refs)
                .ConfigureAwait(false);
            var next = Next(entry.Held, verdict, await OnAtAsync(verdict).ConfigureAwait(false), identity, now);
            if (next == entry.Held) continue;
            context.Recorded.Hold(entry, next);
            written++;
        }

        foreach (var grown in context.Grown.All().Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)))
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{grown.Branch}", ct, refs).ConfigureAwait(false) is not { } tip) continue;
            if (grown.Held is { } kept && await HoldsAsync(kept, tip).ConfigureAwait(false)) continue;
            // The landing that carries its unit's work, where one stands: its cycle is what that landing recorded (§B3).
            var landing = grown.Unit is { } unit ? landings.LastOrDefault(entry => entry.Units.Contains(unit, StringComparer.Ordinal)) : null;
            var cycle = landing is not null ? EndCycle.Recorded(landing) : EndCycle.Accepts(config ??= context.Config(), repository, workspace);
            carriers ??= await asks.CarriersAsync(root, repository, line, ct, refs).ConfigureAwait(false);
            var subject = new EndSubject(grown.Branch, tip, EndRole.Session, landing) { Worked = !string.Equals(tip, grown.From, StringComparison.Ordinal) };
            var verdict = await EndJudgement.JudgeAsync(root, cycle, subject, line, notHolders, carriers, ct, refs).ConfigureAwait(false);
            var next = Next(grown.Held, verdict, await OnAtAsync(verdict).ConfigureAwait(false), identity, now);
            if (next == grown.Held) continue;
            context.Grown.Hold(grown.Repository, grown.Branch, next);
            written++;
        }

        return written;

        // Whether a kept fact still holds at the branch's tip now: the cheap fields first, then where it held, read from the refs.
        async Task<bool> HoldsAsync(HeldFact kept, string tip)
        {
            if (!kept.HoldsAt(tip, line, identity, onDescends: true)) return false;
            var at = await TreeRefs.CommitOfAsync(root, RefOf(kept.On), ct, refs).ConfigureAwait(false);
            return kept.HoldsAt(tip, line, identity, at is null ? null
                : string.Equals(at, kept.OnAt, StringComparison.Ordinal) || await DescendsAsync(kept.OnAt, at).ConfigureAwait(false) is true);
        }

        // The commit of where an end held, when it was judged.
        async Task<string?> OnAtAsync(EndVerdict verdict) =>
            verdict is { Ended: true, Where: { } where } ? await TreeRefs.CommitOfAsync(root, RefOf(where), ct, refs).ConfigureAwait(false) : null;

        // A form of the line by its name (`main`, `origin/main`), else a branch here (the landing branch that carried it).
        string RefOf(string on) => forms.FirstOrDefault(form => string.Equals(form.Name, on, StringComparison.Ordinal))?.Ref ?? $"refs/heads/{on}";

        async Task<bool?> DescendsAsync(string from, string to)
        {
            var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", from, to], ct).ConfigureAwait(false);
            return code switch { 0 => true, 1 => false, _ => null };
        }
    }

    /// <summary>
    /// The fact a judgement leaves (§C7), given the one kept: an end held with its evidence, <c>Since</c> kept where the tip, the line and
    /// the repository are the ones it was first held at; null where the verdict is not an end, or its evidence could not be read.
    /// </summary>
    internal static HeldFact? Next(HeldFact? kept, EndVerdict verdict, string? onAt, string repository, DateTimeOffset now)
    {
        if (verdict is not { Ended: true, By: { } by, Where: { } on, Tip: { } tip } || onAt is null) return null;
        var since = kept is not null && kept.Tip == tip && kept.Line == verdict.Line && kept.Repository == repository ? kept.Since : now;
        return new HeldFact(now, since, by, tip, verdict.Line, on, onAt, repository);
    }
}
