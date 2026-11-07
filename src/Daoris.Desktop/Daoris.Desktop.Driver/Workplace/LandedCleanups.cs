using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The clean-up of the branches landings made (out of <c>SessionTrees.Landed.cs</c>): the list, the press, and
/// each recorded branch judged over the whole set. Over the trees' context, the pull request's asks (the look's one occasion), the
/// content proof's keep and delete, the replay's record tidying, the session branches' half of the clean-up
/// (<see cref="TreeSweeps"/>), and the end judgement's keeper for a landing whose branch went (<see cref="EndKeeping.KeepGoneAsync"/>).
/// </summary>
internal sealed class LandedCleanups(
    TreeContext context, PullRequestAsks asks, ContentHolds content, BranchReplays replays, TreeSweeps sweeps, EndKeeping ends)
{
    /// <summary>
    /// The clean-up's list: every session branch with what it holds, then every branch a landing
    /// made and recorded, with whether its work reached the line. Nothing is changed.
    /// </summary>
    /// <param name="closedQuests">The quests the service says are closed, which a row's quest is read against; none, none is said closed.</param>
    internal async Task<SweepPlan> CleanPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default, IReadOnlySet<string>? closedQuests = null)
    {
        var known = repositories.ToList();
        // One look: git is asked each checkout's line once, not by each of the three readers below.
        using var look = context.OneLook();
        // The look may remove, so it asks first where git cannot tell, as one occasion over every repository; the press asks nothing.
        var asked = await asks.AskStatesAsync(
            known.Where(each => !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root))
                .Select(each => (each.Root!, each.Repository, RemoteTarget.Workspace(each.Workspace))),
            landedMayGo: true, ct).ConfigureAwait(false);

        var sessions = await sweeps.SweepPlanAsync(known, inUse, ct).ConfigureAwait(false);
        return new(sessions, await LandedPlanAsync(known, sessions, asked, ct, closedQuests).ConfigureAwait(false));
    }

    /// <summary>
    /// The clean-up's list of one repository as the machine's facts judge it: its git half alone, every session branch
    /// and every branch a landing made, each judged by the pull-request answers the landing record keeps. It asks no plugin: asking
    /// is kept to an occasion that may remove, and a fact judged at each ref's move and at a deadline is none. The
    /// person's look (<see cref="CleanPlanAsync"/>) asks first and keeps what it heard, which this reads at its next judgement.
    /// Nothing is changed.
    /// </summary>
    /// <param name="inUse">The trees sessions still running or waiting name, as the service's answers the facts hold say.</param>
    /// <param name="closedQuests">The quests the service says are closed, as the facts' part reads them; none, none is said closed.</param>
    internal async Task<SweepPlan> SweepPartAsync(
        string repository, string? workspace, string root, IReadOnlySet<string> inUse, CancellationToken ct = default,
        IReadOnlySet<string>? closedQuests = null)
    {
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> one = [(repository, workspace, root)];
        // One look at the checkout's line for both halves, as the person's look takes.
        using var look = context.OneLook();
        var sessions = await sweeps.SweepPlanAsync(one, inUse, ct).ConfigureAwait(false);
        // Nothing was asked, so a row says only what holds whatever the occasion: no plugin pushed it and the rule names none.
        return new(sessions, await LandedPlanAsync(one, sessions, [], ct, closedQuests).ConfigureAwait(false));
    }

    /// <summary>
    /// The landed half of the list: every branch a landing made and recorded in these repositories, judged over its repository's
    /// set beside the session branches that stay, each row saying why its pull request was not asked about where <paramref name="asked"/>
    /// or the rule says so. Nothing is changed.
    /// </summary>
    /// <param name="sessions">The session branches the list judged: those it keeps may lean on a landed branch.</param>
    /// <param name="asked">What the occasion asked and heard; none where nothing was asked (<see cref="SweepPartAsync"/>).</param>
    private async Task<List<LandedItem>> LandedPlanAsync(
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> known, IReadOnlyList<SweepItem> sessions,
        IReadOnlyList<StateAnswer> asked, CancellationToken ct, IReadOnlySet<string>? closedQuests = null)
    {
        var record = context.Recorded.All();
        var config = context.Config();
        var landed = new List<LandedItem>();
        foreach (var (repository, workspace, root) in known)
        {
            var entries = LandedProofs.EntriesOf(record, repository);
            if (entries.Count == 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            // What stays after this press: the session branches it keeps, whose proof may lean on a landed branch.
            var staying = sessions
                .Where(item => string.Equals(item.Repository, repository, StringComparison.OrdinalIgnoreCase) && !item.Removable)
                .Select(item => item.Branch).ToList();
            var judged = await JudgeLandedAsync(root, repository, RemoteTarget.Workspace(workspace), entries, staying, stale: null, ct)
                .ConfigureAwait(false);
            // Each row says why its pull request was not asked about, where it would have been.
            // And whether its quest is closed, as the caller read the service once.
            landed.AddRange(judged.Select(each => each.Item with
            {
                NotAsked = NotAskedOf(each.Item, asked, config),
                QuestClosed = each.Item.Quest is { } quest && closedQuests?.Contains(quest) == true,
            }));
        }

        return landed;
    }

    /// <summary>
    /// Why an occasion did not ask about a landed row's pull request, where it would have: what the
    /// occasion said of its entry (the plugin not ready, or its bound reached first); else, on a row kept for want of the end proof
    /// with nothing final kept, that no plugin pushed it and the rule names none. A row asked about, or not due, says nothing.
    /// </summary>
    internal PullRequestNotAsked? NotAskedOf(LandedItem item, IReadOnlyList<StateAnswer> asked, DriverConfig config)
    {
        var said = asked.LastOrDefault(answer => answer.Answer is null && answer.Failure is PullRequestCodes.Unready or PullRequestCodes.NotAsked
            && answer.Ask.Entry.GoneAt is null && string.Equals(answer.Ask.Entry.Branch, item.Branch, StringComparison.Ordinal)
            && string.Equals(answer.Ask.Entry.Repository, item.Repository, StringComparison.OrdinalIgnoreCase));
        if (said is not null) return new(said.Failure!, said.Sentence ?? said.Failure!);
        if (item.Kind is not (LandedKind.Differs or LandedKind.Unknown)) return null;
        if (context.Recorded.Of(item.Repository, item.Branch) is not { } entry || entry.PullRequestState?.State == PullRequestStates.Completed) return null;
        return PullRequestAsking.PluginFor(entry) is null ? new(PullRequestCodes.NoPlugin, PullRequestWords.NoPlugin) : null;
    }

    /// <summary>
    /// The clean-up's press: the session branches the proof clears go first — their proof may
    /// count a landed branch as holding their commits — then the landed branches whose work reached the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Judged over the whole set, then again right before each goes</b>, because the list is a fact about a
    /// moment and "inside another landed branch" is a fact about the set: the branches that are inside another
    /// go before the one they are inside, and each re-judgment proves that one again. Removal deletes exactly
    /// the branch judged, only while it is the commit it was judged at (<c>git update-ref -d</c> with that id),
    /// in the repository's own checkout — no working tree, no other ref, and never a remote branch.
    /// </remarks>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    internal async Task<SweepDone> CleanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default)
    {
        var known = repositories.ToList();
        // One press, one look at each checkout's line: the proofs are still judged again before each removal.
        using var look = context.OneLook();
        var sessions = await sweeps.SweepAsync(known, inUse, only, ct).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone, this press's removals among them.
        foreach (var (repository, _, root) in known)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) await replays.ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        }

        var landed = await CleanLandedAsync(known, only, ct).ConfigureAwait(false);
        // The empty folders trees left where something held them open (the first real post-merge run), tried again.
        return new(sessions, landed, context.Leftovers.EmptyFoldersGone());
    }

    /// <summary>
    /// The landed half of the clean-up's press: the recorded branches whose work reached the line go, each
    /// judged over the whole set and again right before it goes. Bringing a repository up to date presses this
    /// half alone, after its rebases.
    /// </summary>
    internal async Task<List<LandedResult>> CleanLandedAsync(
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> known, IReadOnlySet<string>? only, CancellationToken ct)
    {
        var results = new List<LandedResult>();
        foreach (var (repository, space, root) in known)
        {
            if (LandedProofs.EntriesOf(context.Recorded.All(), repository).Count == 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            var workspace = RemoteTarget.Workspace(space);
            var stale = new List<(LandedBranch Entry, string Why)>();
            var removed = new List<string>();
            var proven = new List<LandedItem>();
            // The session each branch's newest landing carried, read before a removal makes the entry a trace: what the
            // look's machine log names a removal by, since the branch's own name carries the title's words.
            var sessions = LandedProofs.EntriesOf(context.Recorded.All(), repository)
                .GroupBy(entry => entry.Branch, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.MaxBy(entry => entry.LandedAt)!.Session, StringComparer.Ordinal);
            // The refs read once for the judgement over the whole set, and afresh for each judgement again.
            var refs = RefSnapshot.Take(root);
            var first = await JudgeLandedAsync(
                root, repository, workspace, LandedProofs.EntriesOf(context.Recorded.All(), repository),
                await LandedProofs.SessionBranchesAsync(root, ct, refs).ConfigureAwait(false), stale, ct, refs).ConfigureAwait(false);

            // Inside first: each is proven through a branch still standing.
            foreach (var judged in first.OrderBy(each => each.Item.Kind == LandedKind.Inside ? 0 : 1))
            {
                var item = judged.Item;
                var key = $"{repository}:{item.Branch}";
                var named = only?.Contains(key) == true;
                if (only is not null && !named) continue;
                var session = sessions.GetValueOrDefault(item.Branch);
                if (!item.Removable)
                {
                    results.Add(new(item, false, named ? ChangedSince : "kept") { Session = session });
                    continue;
                }

                var fresh = RefSnapshot.Take(root);
                var again = await JudgeLandedAsync(
                    root, repository, workspace,
                    LandedProofs.EntriesOf(context.Recorded.All(), repository).Where(entry => !removed.Contains(entry.Branch)).ToList(),
                    await LandedProofs.SessionBranchesAsync(root, ct, fresh).ConfigureAwait(false), stale: null, ct, fresh).ConfigureAwait(false);
                var now = again.FirstOrDefault(each => each.Item.Branch == item.Branch);
                if (now is null || !now.Item.Removable || now.Tip != judged.Tip)
                {
                    results.Add(new(now?.Item ?? item, false, ChangedSince) { Session = session });
                    continue;
                }

                // Only while it is still the commit judged: git's own -d asks only whether HEAD holds the
                // commits, and a squash merge put none of them anywhere, so the proof made here is the one that counts.
                // Its commits are kept on a ref of their own in the delete's act, as a session branch's are: a
                // squash carries the aggregate content only, so their own history and messages live on this branch alone. A merged
                // branch's tip is on the line, so nothing is lost and no ref is made.
                RecoveryName? keep = null;
                if (now.Item.Kind != LandedKind.Merged)
                {
                    var (name, why) = await RecoveryKeeps.NameAsync(root, item.Branch, now.Tip, CancellationToken.None).ConfigureAwait(false);
                    if (name is null)
                    {
                        results.Add(new(now.Item, false, $"Daoris could not keep its commits on a ref of their own ({why}), so it is kept") { Session = session });
                        continue;
                    }

                    keep = name;
                }

                // What the judgement again's proof returned, each ref at the commit it read, verified in the delete's
                // own transaction beside the recovery ref it makes; nothing is read again here.
                var keptAt = keep?.Ref;
                var kept = await content.DeleteJudgedAsync(root, item.Repository, item.Branch, now.Tip, now.Rests, ct, keep).ConfigureAwait(false);
                if (kept is null)
                {
                    removed.Add(item.Branch);
                    proven.Add(now.Item);
                }

                results.Add(kept is null
                    ? new(now.Item, true, keptAt is null ? "removed" : $"{Sentence("removed")} {ContentHold.KeptAt(item.Branch, keptAt)}")
                    {
                        Session = session,
                        KeptRef = keptAt,
                    }
                    : new(now.Item, false, kept == ContentHolds.MovedSince ? ChangedSince : kept) { Session = session });
            }

            // Gone, or no longer the landing's: never judged again — the name may be the person's now — and kept as a
            // trace with what the proof found, so the review of the session that landed it says where its work went.
            foreach (var item in proven) context.Recorded.Removed(repository, item.Branch, item.Kind, item.Where);
            // With why, on git's word: a branch of its name standing that is not the landing's, or missing, where what holds its
            // recorded tip is asked first: moved to a branch that holds it, or its commit kept on a ref where nothing does.
            // Each written to the landing judged alone, so a new landing of its name keeps its standing. A missing one the line
            // holds is the end judgement's first, kept ended as it retires, never read as moved to the line.
            var line = stale.Any(each => each.Why == LandedGone.Missing)
                ? (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch
                : null;
            foreach (var (entry, why) in stale)
            {
                if (why != LandedGone.Missing) context.Recorded.Gone(entry, why);
                else if (!await ends.KeepGoneAsync(root, repository, entry, line, ct).ConfigureAwait(false)
                         && await WentAsync(context.Home, root, entry, ct).ConfigureAwait(false) is { } went)
                {
                    context.Recorded.Gone(entry, went.Why, went.MovedTo, went.KeptOn, went.MovedToAt);
                }
            }
        }

        return results;
    }

    private const string ChangedSince = "it changed since the list, and is kept";

    /// <summary>A landed branch as judged, and the commit it was judged at — the one a removal must still find.</summary>
    internal sealed record Judged(LandedItem Item, string Tip)
    {
        /// <summary>
        /// The form of the line its own proof found its work on: on the line by content, merged, or its pull request's
        /// completed merge, before anything that keeps the branch itself is asked. Null where its proof did not find it there; a
        /// branch inside another is found through that one.
        /// </summary>
        internal string? Line { get; init; }

        /// <summary>
        /// What its removal rests on, as its proof returned it: the form of the line it was proved on, or the
        /// landing it is inside at the tip judged with what that one rests on, each at the commit read; its delete verifies exactly these.
        /// </summary>
        internal IReadOnlyList<(string Ref, string Commit)> Rests { get; init; } = [];
    }

    /// <summary>
    /// Judge every recorded branch of one repository: each by content against the line, then those
    /// inside another that passed, then what must keep them.
    /// </summary>
    /// <param name="staying">The session branches that stay: a landed branch one of them leans on stays too.</param>
    /// <param name="stale">Where a branch that is gone, or no longer the landing's, is named for the record to forget; null to only look.</param>
    /// <param name="refs">The refs the caller's judgement read; null takes this judgement's own, so each judgement again reads afresh.</param>
    internal async Task<List<Judged>> JudgeLandedAsync(
        string root, string repository, string workspace, IReadOnlyList<LandedBranch> entries, IReadOnlyCollection<string> staying,
        List<(LandedBranch Entry, string Why)>? stale, CancellationToken ct, RefSnapshot? refs = null)
    {
        // One read of the refs and the working trees for every branch judged here (-iii); a write in this flow drops it.
        refs ??= RefSnapshot.Take(root);
        // A list git could not give is never "no tree here": every branch is then kept as one checked out.
        var worktrees = await context.Worktrees.ReadAsync(root, ct, refs).ConfigureAwait(false);
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);

        var judged = new List<(LandedBranch Entry, string Tip, LandedProofs.Proof Proof, string? Keep, int Ahead, PullRequestVerdict? Verdict)>();
        // A branch git could not read is kept and said, never retired: only git's own word retires a record.
        var unread = new List<Judged>();
        foreach (var entry in entries)
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{entry.Branch}", ct, refs).ConfigureAwait(false) is not { } tip)
            {
                // A broken ref resolves to nothing and is left out of the refs' list, so it is gone only where git says it is missing.
                var (gone, said) = await AbsenceAsync(root, entry.Branch, refs, ct).ConfigureAwait(false);
                if (gone is not null) stale?.Add((entry, gone));
                else unread.Add(Unread(repository, workspace, entry, $"its branch did not read ({said}), so it is kept as unread"));
                continue;
            }

            // 🔴 A person's own branch is never judged: one that took the recorded name since does not hold the
            // commit the landing made it at, whatever its files say. Only git's "not an ancestor" (exit 1) says so.
            var (ours, _, oursErr) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip], ct).ConfigureAwait(false);
            if (ours == 1)
            {
                stale?.Add((entry, LandedGone.Stands));
                continue;
            }

            if (ours != 0)
            {
                unread.Add(Unread(repository, workspace, entry,
                    $"whether its branch still holds the commit the landing made did not read ({ContentHolds.Said(oursErr)}), so it is kept as unread"));
                continue;
            }

            var proof = await LandedProofs.ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
            // A fourth proof, the platform's word confirmed by git, where the others could not see the work on the line.
            var verdict = entry.PullRequestState is { } kept
                ? await PullRequestAsks.VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false)
                : null;
            if (proof.Kind is not (LandedKind.OnLine or LandedKind.Merged) && verdict is { Clears: true })
            {
                proof = proof with
                {
                    Kind = LandedKind.PullRequest, Where = verdict.Form, Files = [], Detail = null, Judgeable = false,
                    // The form its merge commit was found on, at the commit read.
                    Rests = LandedProofs.RestsOn(forms.FirstOrDefault(form => form.Name == verdict.Form)),
                };
            }

            string? keep = null;
            var ahead = 0;
            if (worktrees is null || worktrees.ContainsKey(entry.Branch))
            {
                keep = LandedKind.CheckedOut;
            }
            else if (proof.Kind != LandedKind.Merged && await AheadOfRemoteAsync(root, entry.Branch, tip, ct, refs).ConfigureAwait(false) is { } lacking)
            {
                // Merged would lose nothing — every commit is on the line — so only that outranks a move since the push.
                ahead = lacking;
                keep = lacking > 0 ? LandedKind.AheadOfRemote : null;
            }

            judged.Add((entry, tip, proof, keep, ahead, verdict));
        }

        var proven = judged.Where(each => each.Proof.Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.PullRequest).ToList();
        var items = new List<Judged>();
        foreach (var (entry, tip, proof, keep, ahead, verdict) in judged)
        {
            var kind = proof.Kind;
            var where = proof.Where;
            var rests = proof.Rests;
            // Inside another landed branch the proof clears — whether or not that one can go, its history
            // holds these commits and its files read on the line.
            if (proof.Judgeable)
            {
                foreach (var other in proven.Where(other => other.Entry.Branch != entry.Branch))
                {
                    var (inside, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", tip, other.Tip], ct).ConfigureAwait(false);
                    if (inside != 0) continue;
                    kind = LandedKind.Inside;
                    where = other.Entry.Branch;
                    // It rests on that branch at the tip judged, and on what that one's proof rests on.
                    rests = [($"refs/heads/{other.Entry.Branch}", other.Tip), .. other.Proof.Rests];
                    break;
                }
            }

            if (keep is not null)
            {
                kind = keep;
                where = null;
            }

            var removable = kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.PullRequest;
            if (removable && await LeanerAsync(root, tip, staying, forms, ct).ConfigureAwait(false) is { } leaner)
            {
                kind = LandedKind.LeanedOn;
                where = leaner;
            }

            var files = kind == LandedKind.Differs ? proof.Files : [];
            items.Add(new(
                new LandedItem(repository, workspace, entry.Branch, kind, where, files, proof.Detail, entry.PullRequest,
                    kind == LandedKind.AheadOfRemote ? ahead : proof.Commits)
                {
                    // What the platform last said, and why it does not clear the branch where it does not.
                    State = entry.PullRequestState,
                    StateCode = verdict is { Clears: false } ? verdict.Code : null,
                    AskFailed = entry.PullRequestAskFailed,
                    LandedAt = entry.LandedAt,
                    Quest = entry.Quest,
                },
                tip)
            {
                Line = proof.Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.PullRequest ? proof.Where : null,
                Rests = rests,
            });
        }

        items.AddRange(unread);
        return items;
    }

    /// <summary>
    /// The landings of one repository whose branch went outside Daoris, said on git's own word: a standing entry whose branch the
    /// refs do not list is retired as a trace once git says it is missing, and a trace with no <see cref="LandedBranch.GoneAs"/>
    /// is asked once. Missing, with no branch of its name in another case standing, is <see cref="LandedGone.Missing"/>, the person's end of
    /// it, once what holds its recorded tip is asked (<see cref="WentAsync"/>); a branch of its name standing is
    /// <see cref="LandedGone.Stands"/>; a read git could not make writes nothing, and is asked again at the next look. A trace that moved is
    /// followed: asked again once the branch it moved to is no longer listed. Each write names the landing it read, never its name alone,
    /// so a landing recorded since keeps its standing. No git where nothing needs asking.
    /// </summary>
    /// <remarks>
    /// A landing git says is missing is the end judgement's first (<see cref="EndKeeping.KeepGoneAsync"/>): where the line holds
    /// the tip its landing recorded, by ancestry or by content, it is kept ended as it retires, never read as moved to the line
    /// nor as dropped, and no recovery ref is made. A trace an earlier look read as moved to the line, with no fact, is asked again for it.
    /// </remarks>
    /// <param name="line">The line the repository's cycle names, as the look reads it; null where none is set, so nothing is judged ended.</param>
    /// <returns>How many entries were written.</returns>
    internal async Task<int> MarkAbsentAsync(string root, string repository, string? line, CancellationToken ct, RefSnapshot? refs = null)
    {
        // Asked: a standing entry (its end held or not), and a trace that says no why, neither removed by Daoris; a held trace reads ended.
        // And a trace that moved, once the branch it moved to no longer stands at the commit it was found holding the work at (re-check:
        // a name made again elsewhere resolves, and holds nothing of it): read from the refs the look already holds, so a moved trace whose
        // branch stands still costs no git of its own, and one whose branch went on is asked once and written at its new commit.
        var entries = context.Recorded.Entries()
            .Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase) && each.RemovedAs is null)
            .ToList();
        var asking = entries.Where(each => each.GoneAs is null && (each.GoneAt is null || each.Held is null)).ToList();
        var moved = entries.Where(each => each.GoneAs == LandedGone.Moved).ToList();
        if (asking.Count == 0 && moved.Count == 0) return 0;
        refs ??= RefSnapshot.Take(root);
        foreach (var each in moved)
        {
            // Moved to the line with no fact is the end judgement's to prove, whether or not the line has moved since.
            if (each.Held is null && line is not null && string.Equals(each.MovedTo, line, StringComparison.Ordinal))
            {
                asking.Add(each);
                continue;
            }

            var at = each.MovedTo is null ? null : await TreeRefs.CommitOfAsync(root, $"refs/heads/{each.MovedTo}", ct, refs).ConfigureAwait(false);
            if (at is null || !string.Equals(at, each.MovedToAt, StringComparison.OrdinalIgnoreCase)) asking.Add(each);
        }

        var written = 0;
        foreach (var named in asking.GroupBy(each => each.Branch, StringComparer.Ordinal))
        {
            var why = await TreeRefs.CommitOfAsync(root, $"refs/heads/{named.Key}", ct, refs).ConfigureAwait(false) is not null
                ? LandedGone.Stands
                : (await AbsenceAsync(root, named.Key, refs, ct).ConfigureAwait(false)).Why;
            if (why is null) continue;
            foreach (var entry in named)
            {
                if (why == LandedGone.Stands)
                {
                    // A standing entry whose name stands is the clean-up's to judge; a trace whose name stands again is a doubt, said once.
                    if (entry.GoneAt is null) continue;
                    context.Recorded.GoneAs(entry, LandedGone.Stands);
                }
                else if (await ends.KeepGoneAsync(root, repository, entry, line, ct, refs).ConfigureAwait(false))
                {
                    // The line holds what its landing recorded, so its end is kept and the entry retired with it.
                }
                else if (await WentAsync(context.Home, root, entry, ct).ConfigureAwait(false) is { } went)
                {
                    context.Recorded.GoneAs(entry, went.Why, went.MovedTo, went.KeptOn, went.MovedToAt);
                }
                else
                {
                    continue;
                }

                written++;
            }
        }

        return written;
    }

    /// <summary>
    /// What holds a landing's recorded tip once git said its branch is missing, asked before its record retires.
    /// A local branch holding it by ancestry is <see cref="LandedGone.Moved"/> to that branch, a branch of the person's named first: a rename,
    /// or a branch made from it, so the work goes on and is never read as the person's end. Else <see cref="LandedGone.Missing"/>; and where
    /// neither a tag nor a recovery ref holds it, its commit is first kept on a recovery ref of the landing's name
    /// (<see cref="RecoveryKeeps.KeepAsync"/>), which nothing else holds and so stays until the person deletes it: the record never
    /// goes as the only way back to it. A remote-tracking ref does not count, since a fetch moves or prunes it, as the
    /// recovery refs' holders do not (<see cref="RecoveryRetention"/>). A commit this repository no longer has, which git's own collection
    /// took once nothing held it, is missing with nothing to keep. Null, writing nothing, where git could not say or the ref was not made:
    /// asked again at the next look.
    /// </summary>
    /// <remarks>
    /// <para>Re-check: a symbolic ref never holds (git's <c>%(symref)</c>, <see cref="ContentHolds.Unaliased"/>), and only a remote's
    /// <c>HEAD</c> is left out by its name, so a local branch called <c>feature/HEAD</c> is a branch like any other. The branch moved to is
    /// kept with the commit it held the work at (<see cref="LandedBranch.MovedToAt"/>). The recovery is made in one ref transaction with
    /// git's word that the landing's own branch is still absent, so a branch made again after this read refuses it and nothing is written.</para>
    /// <para>One <c>for-each-ref --contains</c> for each landing read missing, once; one <c>update-ref --stdin</c> where nothing holds it.</para>
    /// </remarks>
    private static async Task<(string Why, string? MovedTo, string? KeptOn, string? MovedToAt)?> WentAsync(string home, string root, LandedBranch entry, CancellationToken ct)
    {
        var (code, listed, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--contains", entry.Tip, $"--format={ContentHolds.UnaliasedFormat}"], ct)
            .ConfigureAwait(false);
        if (code != 0)
        {
            // `cat-file -e` exits 1, saying nothing, for an object the repository does not have; every other failure is unread (git-ref-absence).
            var (has, _, _) = await WorkingTree.GitAsync(root, ["cat-file", "-e", entry.Tip], ct).ConfigureAwait(false);
            return has == 1 && WorkingTree.IsCommitId(entry.Tip) ? (LandedGone.Missing, null, null, null) : null;
        }

        // An act's own ref holds an index's blobs until its record expires, never a landing's work.
        var holders = ContentHolds.Unaliased(listed)
            .Where(pair => !(pair.Ref.StartsWith("refs/remotes/", StringComparison.Ordinal) && pair.Ref.EndsWith("/HEAD", StringComparison.Ordinal)))
            .Where(pair => !ActRefs.IsAct(pair.Ref)).ToList();
        var local = holders.Where(pair => pair.Ref.StartsWith("refs/heads/", StringComparison.Ordinal)).ToList();
        if (local.Count > 0)
        {
            var to = local.Where(pair => ContentHolds.PersonsRef(pair.Ref)).Concat(local).First();
            return (LandedGone.Moved, ContentHolds.Named(to.Ref), null, to.Commit);
        }

        if (holders.Any(pair => pair.Ref.StartsWith("refs/tags/", StringComparison.Ordinal) || pair.Ref.StartsWith(ContentHolds.DiscardedPrefix, StringComparison.Ordinal)))
        {
            return (LandedGone.Missing, null, null, null);
        }

        var (kept, _) = await RecoveryKeeps.KeepAsync(home, root, entry.Repository, entry.Branch, entry.Tip, absent: $"refs/heads/{entry.Branch}").ConfigureAwait(false);
        return kept is null ? null : (LandedGone.Missing, null, kept, null);
    }

    /// <summary>
    /// Git's word on a landing's branch that did not resolve: <see cref="LandedGone.Missing"/> where <c>show-ref --exists</c>
    /// says missing and no branch of its name in another case stands (packed refs compare case exactly, so git calls such a name missing on
    /// a disk blind to case); <see cref="LandedGone.Stands"/> where one does; null, with git's words, where either read failed.
    /// </summary>
    private static async Task<(string? Why, string Said)> AbsenceAsync(string root, string branch, RefSnapshot? refs, CancellationToken ct)
    {
        var (missing, said) = await ContentHolds.MissingAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false);
        if (!missing) return (null, said);
        var names = await (refs ?? RefSnapshot.Take(root)).BranchesAsync("", ct).ConfigureAwait(false);
        if (names is null) return (null, "the branches did not list");
        return names.Any(name => string.Equals(name, branch, StringComparison.OrdinalIgnoreCase))
            ? (LandedGone.Stands, said)
            : (LandedGone.Missing, said);
    }

    /// <summary>
    /// A recorded branch git could not read: kept as <see cref="LandedKind.Unknown"/> with why, at no tip, so no removal and
    /// no judgement again takes it, and its record stays for the next read.
    /// </summary>
    private static Judged Unread(string repository, string workspace, LandedBranch entry, string why) =>
        new(new LandedItem(repository, workspace, entry.Branch, LandedKind.Unknown, null, [], why, entry.PullRequest, 0)
        {
            State = entry.PullRequestState,
            AskFailed = entry.PullRequestAskFailed,
            LandedAt = entry.LandedAt,
            Quest = entry.Quest,
        }, "");

    /// <summary>
    /// How many commits the branch holds that its remote-tracking branch does not, where it has one — its
    /// upstream, else <c>origin/&lt;branch&gt;</c> — or null where it has none: never pushed, or its remote
    /// branch deleted and pruned since.
    /// </summary>
    /// <param name="refs">The judgement's refs, where they hold the upstream and origin's copy; null asks git.</param>
    internal static async Task<int?> AheadOfRemoteAsync(string root, string branch, string tip, CancellationToken ct, RefSnapshot? refs = null)
    {
        // Its upstream as `<branch>@{upstream}` resolves it: none, or one whose ref is gone, falls to origin's copy.
        var upstream = refs is null ? default : await refs.UpstreamAsync(branch, ct).ConfigureAwait(false);
        string? remote = null;
        if (!upstream.Known)
        {
            var (code, sha, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"{branch}@{{upstream}}^{{commit}}"], ct)
                .ConfigureAwait(false);
            if (code == 0) remote = sha.Trim();
        }
        else if (upstream.Full is not null)
        {
            remote = await TreeRefs.CommitOfAsync(root, upstream.Full, ct, refs).ConfigureAwait(false);
        }

        remote ??= await TreeRefs.CommitOfAsync(root, $"refs/remotes/origin/{branch}", ct, refs).ConfigureAwait(false);
        if (remote is null) return null;
        var (countCode, count, _) = await WorkingTree.GitAsync(root, ["rev-list", "--count", tip, "--not", remote], ct).ConfigureAwait(false);
        // A count git could not make keeps the branch: an empty answer is not "nothing ahead".
        return countCode == 0 && int.TryParse(count.Trim(), out var ahead) ? ahead : 1;
    }

    /// <summary>
    /// A session branch that stays and shares commits with this one that the line does not hold: the end proof counts
    /// this branch as holding them, so removing it would leave that session's work looking unlanded. The look asks it of the
    /// session branches it leaves.
    /// </summary>
    internal static async Task<string?> LeanerAsync(
        string root, string tip, IReadOnlyCollection<string> staying, IReadOnlyList<LandedProofs.LineForm> forms, CancellationToken ct)
    {
        foreach (var session in staying)
        {
            var (code, bases, _) = await WorkingTree.GitAsync(root, ["merge-base", "--all", session, tip], ct).ConfigureAwait(false);
            if (code != 0) continue;
            foreach (var shared in bases.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var onLine = false;
                foreach (var form in forms)
                {
                    var (held, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", shared, form.Ref], ct).ConfigureAwait(false);
                    if (held != 0) continue;
                    onLine = true;
                    break;
                }

                if (!onLine) return session;
            }
        }

        return null;
    }
}
