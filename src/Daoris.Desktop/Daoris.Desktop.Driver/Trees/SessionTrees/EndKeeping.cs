using System.Collections.Concurrent;

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
/// descends from the commit it held at (rewound or replaced), or the repository at the path is another; one that rests on a landing's end
/// (<see cref="EndBy.Inside"/>, <see cref="EndBy.Landed"/>) also lapses once that end no longer holds this pass (FABRIC-END2d). It is then judged again: held
/// anew, or cleared, which a reader reads as not known. A trace is never judged again. A fact that still holds is not written again, so a
/// look that changes nothing writes nothing.</para>
///
/// <para><b>Nothing is removed here</b> (§B8): the look's tidy takes its candidates from what is kept (FABRIC-END4a), and the
/// inventory reads it (MILESTONE1a). <i>Ended</i> is derived at read from the
/// kept fact and the effective cycle, never stored.</para>
/// </remarks>
internal sealed class EndKeeping(TreeContext context, PullRequestAsks asks)
{
    // One judgement of a repository at a time in this process (FABRIC-END3): the look's tidy, Accept and the ref watch each keep ends, and
    // two keepers of one repository at once would each write what it read before the other wrote.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Judging = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Judge one repository's standing branches and keep what the line holds; how many entries were written comes back.</summary>
    /// <param name="root">The repository's main checkout, whose common dir is its <c>.git</c> folder.</param>
    /// <param name="line">The line the cycle names (D86), read by the caller as its own judgement reads it.</param>
    /// <param name="refs">The caller's refs (PROC1d-ii), where it holds them; null takes them here.</param>
    internal async Task<int> KeepAsync(
        string root, string repository, string workspace, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (!HomeLease.HeldHere(context.Home)) return 0;
        var alone = Judging.GetOrAdd($"{Path.GetFullPath(context.Home)}|{repository}", _ => new SemaphoreSlim(1, 1));
        await alone.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await KeepAloneAsync(root, repository, workspace, line, ct, refs).ConfigureAwait(false);
        }
        finally
        {
            alone.Release();
        }
    }

    private async Task<int> KeepAloneAsync(string root, string repository, string workspace, string? line, CancellationToken ct, RefSnapshot? refs)
    {
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

        // FABRIC-END2d: every landing's end this pass, by its branch (its kept fact where that still holds, else this pass's verdict), before
        // any is written: a landing inside another is judged once that one's own proof is known, and a session branch reads its landing's end
        // here rather than judging that landing again.
        var ends = new Dictionary<string, EndHost>(StringComparer.Ordinal);
        var judged = new List<(EndHost Landing, EndFacts Facts)>();
        var insideKept = new List<(LandedBranch Entry, string Tip, HeldFact Kept)>();
        foreach (var entry in landings)
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{entry.Branch}", ct, refs).ConfigureAwait(false) is not { } tip) continue;
            if (entry.Held is { } kept && await HoldsAsync(kept, tip).ConfigureAwait(false))
            {
                // One kept inside another rests on that one's own end too, which is known once every landing is.
                if (kept.By == EndBy.Inside) insideKept.Add((entry, tip, kept));
                else ends[entry.Branch] = new(entry, tip, Kept(kept));
                continue;
            }

            await JudgeLandingAsync(entry, tip).ConfigureAwait(false);
        }

        foreach (var (entry, tip, kept) in insideKept)
        {
            if (ends.TryGetValue(kept.On, out var host) && EndJudgement.OwnEnd(host.End)) ends[entry.Branch] = new(entry, tip, Kept(kept));
            else await JudgeLandingAsync(entry, tip).ConfigureAwait(false);
        }

        // FABRIC-END2d (D102's group, as the clean-up's `Inside` proves it): a landing its own proofs did not end, whose files were compared,
        // inside one whose own proof did. The landings' order, as the clean-up asks them.
        var hosts = landings.Where(entry => ends.TryGetValue(entry.Branch, out var end) && EndJudgement.OwnEnd(end.End)).Select(entry => ends[entry.Branch]).ToList();
        for (var at = 0; at < judged.Count && hosts.Count > 0; at++)
        {
            var (landing, facts) = judged[at];
            if (landing.End.Ended || !facts.Judgeable) continue;
            if (await EndJudgement.WithinAsync(root, landing.Tip, hosts.Where(host => host.Entry.Branch != landing.Entry.Branch), ct).ConfigureAwait(false) is not { } within) continue;
            var inside = facts with { Within = within };
            var subject = new EndSubject(landing.Entry.Branch, landing.Tip, EndRole.Landing, landing.Entry);
            judged[at] = (landing with { End = EndJudgement.Judge(EndCycle.Recorded(landing.Entry), subject, inside) }, inside);
            ends[landing.Entry.Branch] = judged[at].Landing;
        }

        foreach (var (landing, _) in judged)
        {
            var next = Next(landing.Entry.Held, landing.End, await OnAtAsync(landing.End).ConfigureAwait(false), identity, now);
            if (next == landing.Entry.Held) continue;
            context.Recorded.Hold(landing.Entry, next);
            written++;
        }

        foreach (var grown in context.Grown.All().Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase)))
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{grown.Branch}", ct, refs).ConfigureAwait(false) is not { } tip) continue;
            // FABRIC-END2d: a fact ended through a landing rests on that landing's end, so it holds while that end holds this pass.
            if (grown.Held is { } kept && await HoldsAsync(kept, tip).ConfigureAwait(false)
                && (kept.By != EndBy.Landed || (ends.TryGetValue(kept.On, out var on) && on.End.Ended))) continue;
            // The landing that carries its unit's work, where one stands: its cycle is what that landing recorded (§B3).
            var landing = grown.Unit is { } unit ? landings.LastOrDefault(entry => entry.Units.Contains(unit, StringComparer.Ordinal)) : null;
            var landingEnd = landing is not null && ends.TryGetValue(landing.Branch, out var its) ? its.End : null;
            var cycle = landing is not null ? EndCycle.Recorded(landing) : EndCycle.Accepts(config ??= context.Config(), repository, workspace);
            carriers ??= await asks.CarriersAsync(root, repository, line, ct, refs).ConfigureAwait(false);
            var subject = new EndSubject(grown.Branch, tip, EndRole.Session, landing) { Worked = !string.Equals(tip, grown.From, StringComparison.Ordinal) };
            var verdict = await EndJudgement.JudgeAsync(root, cycle, subject, line, notHolders, carriers, ct, refs, landingEnd).ConfigureAwait(false);
            // FABRIC-END2d: where its own proofs and its unit's landing end nothing (no unit names a landing on an install from before END2, or the
            // one it names has not ended), the landing whose record holds its work and whose own proof ended it carries it: what the clean-up's
            // press finds as the landing holding its tip (TreeSweeps.LandingHoldingAsync), and the squash's record vouching for its session
            // (SquashedHoldAsync). HolderAsync's ancestry is the inside fact the landed proof reads, so the verdict is judged on it alone.
            if (!verdict.Ended && landingEnd is not { Ended: true } && await EndJudgement.HolderAsync(root, tip, hosts, ct).ConfigureAwait(false) is { } holder)
            {
                verdict = EndJudgement.Judge(EndCycle.Recorded(holder.Entry), subject with { Landing = holder.Entry },
                    new EndFacts(line) { Inside = true, LandingEnd = holder.End });
            }

            var next = Next(grown.Held, verdict, await OnAtAsync(verdict).ConfigureAwait(false), identity, now);
            if (next == grown.Held) continue;
            context.Grown.Hold(grown.Repository, grown.Branch, next);
            written++;
        }

        return written;

        // One landing judged on its own proofs, kept for the inside pass and the write.
        async Task JudgeLandingAsync(LandedBranch entry, string tip)
        {
            carriers ??= await asks.CarriersAsync(root, repository, line, ct, refs).ConfigureAwait(false);
            var subject = new EndSubject(entry.Branch, tip, EndRole.Landing, entry);
            var facts = await EndJudgement.GatherAsync(root, subject, line, notHolders, carriers, ct, refs).ConfigureAwait(false);
            var landing = new EndHost(entry, tip, EndJudgement.Judge(EndCycle.Recorded(entry), subject, facts));
            judged.Add((landing, facts));
            ends[entry.Branch] = landing;
        }

        // A kept fact that still holds, as the end a landing inside it or a session branch it carries reads.
        static EndVerdict Kept(HeldFact kept) =>
            new(EndState.Ended, kept.By, kept.On, $"its end is kept ({kept.By} on `{kept.On}`)") { Tip = kept.Tip, Line = kept.Line };

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
    /// CLEANUP1b: one landing whose branch git said is missing (<see cref="LandedGone.Missing"/>), judged through <see cref="EndJudgement"/>
    /// at the tip its landing recorded, where the line's ancestry of that commit is its end (§B5), or (CLEANUP1c) that commit's content
    /// proof on a form of the line, as a platform's squash merge leaves it. Where the line holds it, the fact is written as the entry retires
    /// (<see cref="LandedBranches.HoldGone"/>): ended, so nothing of it is left to clean, and no recovery ref is made, since the line holds
    /// its work. Asked by the clean-up and the look before they read where its work went (<see cref="LandedCleanups"/>), so a branch merged
    /// into the line is never read as moved to it, nor a squashed one as dropped.
    /// </summary>
    /// <remarks>
    /// Only the lease's holder writes, as <see cref="KeepAsync"/>. An entry with a fact already is the existing judgement's, and one Daoris
    /// removed is ended by its removal: neither is judged here. One <c>merge-base --is-ancestor</c> for each form of the line, once a landing;
    /// where none holds it, the content proof's reads (<see cref="LandedProofs.ProveAsync"/>: two diffs and a merge base for each form).
    /// </remarks>
    /// <returns>Whether its end was kept and the entry retired with it; false writes nothing, and the caller reads where it went.</returns>
    internal async Task<bool> KeepGoneAsync(string root, string repository, LandedBranch entry, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        if (entry.Held is not null || entry.RemovedAs is not null || !HomeLease.HeldHere(context.Home)) return false;
        var alone = Judging.GetOrAdd($"{Path.GetFullPath(context.Home)}|{repository}", _ => new SemaphoreSlim(1, 1));
        await alone.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (FolderIdentity.Of(Path.Combine(root, ".git")) is not { } identity) return false;
            refs ??= RefSnapshot.Take(root);
            var subject = new EndSubject(entry.Branch, entry.Tip, EndRole.Landing, entry) { Gone = true };
            var verdict = await EndJudgement.JudgeAsync(root, EndCycle.Recorded(entry), subject, line, NoHolders, carriers: null, ct, refs)
                .ConfigureAwait(false);
            if (verdict is not { Ended: true, Where: { } where }) return false;
            var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);
            var on = forms.FirstOrDefault(form => string.Equals(form.Name, where, StringComparison.Ordinal))?.Ref;
            var onAt = on is null ? null : await TreeRefs.CommitOfAsync(root, on, ct, refs).ConfigureAwait(false);
            return Next(null, verdict, onAt, identity, DateTimeOffset.UtcNow) is { } held && context.Recorded.HoldGone(entry, held);
        }
        finally
        {
            alone.Release();
        }
    }

    // A landing's judgement reads no holder by content, so none is named.
    private static readonly IReadOnlySet<string> NoHolders = new HashSet<string>();

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
