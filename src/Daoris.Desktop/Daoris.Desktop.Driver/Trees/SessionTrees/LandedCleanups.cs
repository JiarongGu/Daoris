using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>What a landed branch holds, for the clean-up's list (WSR5). Only the first three go.</summary>
public static class LandedKind
{
    /// <summary>Every file it changed reads on the line as it left it — a squash merge's pull request reached the line.</summary>
    public const string OnLine = "on-line";

    /// <summary>Every commit of it is on the line.</summary>
    public const string Merged = "merged";

    /// <summary>Its history is inside another landed branch whose work is on the line.</summary>
    public const string Inside = "inside";

    /// <summary>
    /// Its pull request completed, as the plugin that pushed it answered, and git confirms it here (PLUGHOOK1a, D148 point 4): the
    /// merge commit is on the line and the branch stands at or under the commit it merged. What a squash leaves once the line
    /// has moved on.
    /// </summary>
    public const string PullRequest = "pull-request";

    /// <summary>Some file it changed reads otherwise on the line — kept, and the files named.</summary>
    public const string Differs = "differs";

    /// <summary>A working tree has it checked out, the repository's own checkout included — kept.</summary>
    public const string CheckedOut = "checked-out";

    /// <summary>It holds commits its remote-tracking branch does not: pushed, then moved — kept.</summary>
    public const string AheadOfRemote = "ahead-of-remote";

    /// <summary>A session branch that stays holds commits only this branch holds, and D88's proof for it leans on this one — kept.</summary>
    public const string LeanedOn = "leaned-on";

    /// <summary>Git could not say — kept.</summary>
    public const string Unknown = "unknown";
}

/// <summary>
/// Why a landing's branch went where Daoris did not remove it (CLEANUP1, <see cref="LandedBranch.GoneAs"/>), each on git's own word
/// (the knowledge note <c>git-ref-absence</c>).
/// </summary>
public static class LandedGone
{
    /// <summary>
    /// git says its branch is missing (<c>show-ref --exists</c> exit 2), and no branch of its name in another case stands: the person
    /// removed it, which is the person's end of it.
    /// </summary>
    public const string Missing = "missing";

    /// <summary>A branch of its name stands that is not the landing's (another commit, or the name in another case): kept, the person's to say.</summary>
    public const string Stands = "stands";

    /// <summary>
    /// git says its branch is missing, and another local branch holds the landing's recorded tip by ancestry (CLEANUP1, after review): the
    /// work moved there, a rename or a branch made from it, so it is kept, never read as the person's end (<see cref="LandedBranch.MovedTo"/>).
    /// </summary>
    public const string Moved = "moved";
}

/// <param name="Where">On the line: the form of the line it was proven on (`main`, `origin/main`). Inside: the branch it is inside. Leaned on: the session branch.</param>
/// <param name="Files">Differs: the files it changed that read otherwise on the line.</param>
/// <param name="Detail">Git's own words where it could not say.</param>
/// <param name="PullRequest">The pull request a plugin answered with when it pushed the branch, where the record knows one.</param>
/// <param name="Commits">Ahead of its remote: the commits the remote lacks. Otherwise: the commits it holds that are not on the line.</param>
public sealed record LandedItem(
    string Repository, string Workspace, string Branch, string Kind, string? Where, IReadOnlyList<string> Files,
    string? Detail, string? PullRequest, int Commits)
{
    public bool Removable => Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.PullRequest;

    /// <summary>
    /// The latest answer about its pull request, where one is kept (PLUGHOOK1a, design §2.5): what a row says the platform
    /// answered, and when.
    /// </summary>
    public PullRequestState? State { get; init; }

    /// <summary>
    /// Why a kept answer does not clear it, where one is kept and does not (design §2.3): the state (<c>open</c>, <c>abandoned</c>,
    /// <c>unknown</c>) or one of <see cref="PullRequestCodes"/>. Null where nothing is kept, or the answer cleared it.
    /// </summary>
    public string? StateCode { get; init; }

    /// <summary>The latest ask that failed since that answer, where one did (design §2.4).</summary>
    public PullRequestAskFailed? AskFailed { get; init; }

    /// <summary>
    /// Why this look did not ask about its pull request, where it would have (PLUGHOOK1c, design §2.1): no plugin to ask, the
    /// plugin not ready, or the occasion's bound reached first. Null where it was asked, was not due, or its work reads on the line.
    /// </summary>
    public PullRequestNotAsked? NotAsked { get; init; }
}

/// <summary>What the clean-up did with one landed branch, in the driver's words.</summary>
public sealed record LandedResult(LandedItem Item, bool Removed, string Message)
{
    /// <summary>
    /// The session whose work the landing carried, from the record (AUTOTIDY2b): what the look's machine log names the branch by,
    /// since the branch's own name carries the title's words. Null where the record no longer holds it.
    /// </summary>
    public string? Session { get; init; }

    /// <summary>Removed though not merged (SQUASHTIDY1e): the ref its commits were kept on, for the look's report (AUTOTIDY2b); null where none was kept.</summary>
    public string? KeptRef { get; init; }
}

/// <summary>The clean-up's whole list (D88, WSR5): the session branches, then the branches landings made.</summary>
public sealed record SweepPlan(IReadOnlyList<SweepItem> Sessions, IReadOnlyList<LandedItem> Landed);

/// <summary>What one press of the clean-up did, both groups.</summary>
/// <param name="Folders">The empty folders trees left behind, each removed or still held, in a sentence.</param>
public sealed record SweepDone(IReadOnlyList<SweepResult> Sessions, IReadOnlyList<LandedResult> Landed, IReadOnlyList<string>? Folders = null);

/// <summary>A landed branch's row in words — the terminal's line, and the sentence the screen's row says in its own catalogue.</summary>
public static class LandedWords
{
    /// <summary>
    /// The row: what proved it or keeps it, and on a row that is kept what its pull request's answer was and why it does not
    /// clear the branch, or why nothing was asked (PLUGHOOK1c, design §2.5). A row that goes says what proved it, and no more.
    /// </summary>
    public static string Describe(LandedItem item) => Proof(item)
        + (item.Removable ? "" : PullRequestWords.Row(item.State, item.StateCode, item.AskFailed, item.NotAsked));

    private static string Proof(LandedItem item) => $"{item.Repository}  {item.Branch}  " + item.Kind switch
    {
        LandedKind.OnLine => $"its pull request reached `{item.Where}`: every file it changed reads there as it left it",
        LandedKind.Merged => $"merged into `{item.Where}`",
        LandedKind.Inside => $"inside `{item.Where}`, whose work is on the line",
        LandedKind.PullRequest => "its pull request completed" + (item.State?.How is { } how ? $" by {how}" : "")
            + (item.State?.Plugin is { } plugin ? $", as `{plugin}` answered," : "") + $" and its merge commit is on `{item.Where}`",
        LandedKind.Differs => (item.Files.Count == 1 ? "1 file still differs" : $"{item.Files.Count} files still differ")
            + $" on the line: {string.Join(", ", item.Files.Take(3))}" + (item.Files.Count > 3 ? $" and {item.Files.Count - 3} more" : ""),
        LandedKind.CheckedOut => "checked out in a working tree",
        LandedKind.AheadOfRemote => $"{item.Commits} commit(s) its remote does not have",
        LandedKind.LeanedOn => $"session branch `{item.Where}` still leans on it",
        _ => $"git could not tell: {item.Detail}",
    };
}

/// <summary>
/// The clean-up of the branches landings made (D88, WSR5; PART10l, out of <c>SessionTrees.Landed.cs</c>): the list, the press, and
/// each recorded branch judged over the whole set. Over the trees' context, the pull request's asks (the look's one occasion), the
/// content proof's keep and delete, the replay's record tidying, and the session branches' half of the clean-up
/// (<see cref="TreeSweeps"/>).
/// </summary>
internal sealed class LandedCleanups(
    TreeContext context, PullRequestAsks asks, ContentHolds content, BranchReplays replays, TreeSweeps sweeps)
{
    /// <summary>
    /// The clean-up's list (D88, WSR5): every session branch with what it holds, then every branch a landing
    /// made and recorded, with whether its work reached the line. Nothing is changed.
    /// </summary>
    internal async Task<SweepPlan> CleanPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default)
    {
        var known = repositories.ToList();
        // One look: git is asked each checkout's line once, not by each of the three readers below (UIFREEZE1).
        using var look = context.OneLook();
        // The look may remove, so it asks first where git cannot tell, as one occasion over every repository (PLUGHOOK1a, design
        // §2.1 occasion 2); the press asks nothing.
        var asked = await asks.AskStatesAsync(
            known.Where(each => !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root))
                .Select(each => (each.Root!, each.Repository, RemoteTarget.Workspace(each.Workspace))),
            landedMayGo: true, ct).ConfigureAwait(false);

        var sessions = await sweeps.SweepPlanAsync(known, inUse, ct).ConfigureAwait(false);
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
            // Each row says why its pull request was not asked about, where it would have been (PLUGHOOK1c, design §2.1).
            landed.AddRange(judged.Select(each => each.Item with { NotAsked = NotAskedOf(each.Item, asked, config) }));
        }

        return new(sessions, landed);
    }

    /// <summary>
    /// Why an occasion did not ask about a landed row's pull request, where it would have (PLUGHOOK1c, design §2.1): what the
    /// occasion said of its entry (the plugin not ready, or its bound reached first); else, on a row kept for want of D102's proof
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
        var rule = LandingRules.Choose(config, item.Repository, item.Workspace).Rule;
        return PullRequestAsking.PluginFor(entry, rule) is null ? new(PullRequestCodes.NoPlugin, PullRequestWords.NoPlugin) : null;
    }

    /// <summary>
    /// The clean-up's press (D88, WSR5): the session branches the proof clears go first — their proof may
    /// count a landed branch as holding their commits — then the landed branches whose work reached the line.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Judged over the whole set, then again right before each goes</b>, because the list is a fact about a
    /// moment and "inside another landed branch" is a fact about the set: the branches that are inside another
    /// go before the one they are inside, and each re-judgment proves that one again. Removal deletes exactly
    /// the branch judged, only while it is the commit it was judged at (<c>git update-ref -d</c> with that id,
    /// SQUASHTIDY1c), in the repository's own checkout — no working tree, no other ref, and never a remote branch.
    /// </remarks>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    internal async Task<SweepDone> CleanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default)
    {
        var known = repositories.ToList();
        // One press, one look at each checkout's line (UIFREEZE1): the proofs are still judged again before each removal.
        using var look = context.OneLook();
        var sessions = await sweeps.SweepAsync(known, inUse, only, ct).ConfigureAwait(false);
        // The record of where session branches grew from drops what is gone (LAND3), this press's removals among them.
        foreach (var (repository, _, root) in known)
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) await replays.ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        }

        var landed = await CleanLandedAsync(known, only, ct).ConfigureAwait(false);
        // The empty folders trees left where something held them open (the first real post-merge run), tried again.
        return new(sessions, landed, context.Leftovers.EmptyFoldersGone());
    }

    /// <summary>
    /// The landed half of the clean-up's press (WSR5): the recorded branches whose work reached the line go, each
    /// judged over the whole set and again right before it goes. Bringing a repository up to date presses this
    /// half alone (WSR6), after its rebases.
    /// </summary>
    internal async Task<List<LandedResult>> CleanLandedAsync(
        IReadOnlyList<(string Repository, string? Workspace, string? Root)> known, IReadOnlySet<string>? only, CancellationToken ct)
    {
        var results = new List<LandedResult>();
        foreach (var (repository, space, root) in known)
        {
            if (LandedProofs.EntriesOf(context.Recorded.All(), repository).Count == 0 || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            var workspace = RemoteTarget.Workspace(space);
            var stale = new List<(string Branch, string Why)>();
            var removed = new List<string>();
            var proven = new List<LandedItem>();
            // The session each branch's newest landing carried (AUTOTIDY2b), read before a removal makes the entry a trace: what the
            // look's machine log names a removal by, since the branch's own name carries the title's words.
            var sessions = LandedProofs.EntriesOf(context.Recorded.All(), repository)
                .GroupBy(entry => entry.Branch, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.MaxBy(entry => entry.LandedAt)!.Session, StringComparer.Ordinal);
            // The refs read once for the judgement over the whole set, and afresh for each judgement again (PROC1d-ii).
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

                // Only while it is still the commit judged (SQUASHTIDY1c): git's own -d asks only whether HEAD holds the
                // commits, and a squash merge put none of them anywhere, so the proof made here is the one that counts.
                // Its commits are kept on a ref of their own first (SQUASHTIDY1e), as a session branch's are (SQUASHTIDY1c): a squash
                // carries the aggregate content only, so their own history and messages live on this branch alone. A merged
                // branch's tip is on the line, so nothing is lost and no ref is made.
                string? keptAt = null;
                if (now.Item.Kind != LandedKind.Merged)
                {
                    var (reference, why) = await ContentHolds.KeepDiscardedAsync(root, item.Branch, now.Tip, CancellationToken.None).ConfigureAwait(false);
                    if (reference is null)
                    {
                        results.Add(new(now.Item, false, $"Daoris could not keep its commits on a ref of their own ({why}), so it is kept") { Session = session });
                        continue;
                    }

                    keptAt = reference;
                }

                // FABRIC-END4a, third review: what the judgement again's proof returned, each ref at the commit it read before the recovery
                // ref was made, verified in the delete's own transaction; nothing is read again here.
                var kept = await content.DeleteJudgedAsync(root, item.Branch, now.Tip, now.Rests, ct).ConfigureAwait(false);
                if (kept is null)
                {
                    removed.Add(item.Branch);
                    proven.Add(now.Item);
                }
                else
                {
                    await ContentHolds.DropKeptAsync(root, item.Branch, keptAt, now.Tip, CancellationToken.None).ConfigureAwait(false);
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
            // trace with what the proof found, so the review of the session that landed it says where its work went (D113).
            foreach (var item in proven) context.Recorded.Removed(repository, item.Branch, item.Kind, item.Where);
            // CLEANUP1: with why, on git's word: missing (the person's end of it), or a branch of its name standing that is not the landing's.
            foreach (var same in stale.GroupBy(each => each.Why, StringComparer.Ordinal))
            {
                context.Recorded.Gone(repository, [.. same.Select(each => each.Branch)], same.Key);
            }
        }

        return results;
    }

    private const string ChangedSince = "it changed since the list, and is kept";

    /// <summary>A landed branch as judged, and the commit it was judged at — the one a removal must still find.</summary>
    internal sealed record Judged(LandedItem Item, string Tip)
    {
        /// <summary>
        /// The form of the line its own proof found its work on (AUTOTIDY2b): on the line by content, merged, or its pull request's
        /// completed merge, before anything that keeps the branch itself is asked. Null where its proof did not find it there; a
        /// branch inside another is found through that one.
        /// </summary>
        internal string? Line { get; init; }

        /// <summary>
        /// What its removal rests on (FABRIC-END4a, third review), as its proof returned it: the form of the line it was proved on, or the
        /// landing it is inside at the tip judged with what that one rests on, each at the commit read; its delete verifies exactly these.
        /// </summary>
        internal IReadOnlyList<(string Ref, string Commit)> Rests { get; init; } = [];
    }

    /// <summary>
    /// Judge every recorded branch of one repository (WSR5): each by content against the line, then those
    /// inside another that passed, then what must keep them.
    /// </summary>
    /// <param name="staying">The session branches that stay: a landed branch one of them leans on stays too.</param>
    /// <param name="stale">Where a branch that is gone, or no longer the landing's, is named for the record to forget; null to only look.</param>
    /// <param name="refs">The refs the caller's judgement read (PROC1d-ii); null takes this judgement's own, so each judgement again reads afresh.</param>
    internal async Task<List<Judged>> JudgeLandedAsync(
        string root, string repository, string workspace, IReadOnlyList<LandedBranch> entries, IReadOnlyCollection<string> staying,
        List<(string Branch, string Why)>? stale, CancellationToken ct, RefSnapshot? refs = null)
    {
        // One read of the refs and the working trees for every branch judged here (PROC1d-ii, -iii); a write in this flow drops it.
        refs ??= RefSnapshot.Take(root);
        // A list git could not give is never "no tree here" (AUTOTIDY1): every branch is then kept as one checked out.
        var worktrees = await context.Worktrees.ReadAsync(root, ct, refs).ConfigureAwait(false);
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var forms = await LandedProofs.LineFormsAsync(root, line, ct, refs).ConfigureAwait(false);

        var judged = new List<(LandedBranch Entry, string Tip, LandedProofs.Proof Proof, string? Keep, int Ahead, PullRequestVerdict? Verdict)>();
        // RECORDUNREAD1: a branch git could not read is kept and said, never retired: only git's own word retires a record.
        var unread = new List<Judged>();
        foreach (var entry in entries)
        {
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{entry.Branch}", ct, refs).ConfigureAwait(false) is not { } tip)
            {
                // A broken ref resolves to nothing and is left out of the refs' list, so it is gone only where git says it is missing.
                var (gone, said) = await AbsenceAsync(root, entry.Branch, refs, ct).ConfigureAwait(false);
                if (gone is not null) stale?.Add((entry.Branch, gone));
                else unread.Add(Unread(repository, workspace, entry, $"its branch did not read ({said}), so it is kept as unread"));
                continue;
            }

            // 🔴 A person's own branch is never judged: one that took the recorded name since does not hold the
            // commit the landing made it at, whatever its files say. Only git's "not an ancestor" (exit 1) says so (RECORDUNREAD1).
            var (ours, _, oursErr) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", entry.Tip, tip], ct).ConfigureAwait(false);
            if (ours == 1)
            {
                stale?.Add((entry.Branch, LandedGone.Stands));
                continue;
            }

            if (ours != 0)
            {
                unread.Add(Unread(repository, workspace, entry,
                    $"whether its branch still holds the commit the landing made did not read ({ContentHolds.Said(oursErr)}), so it is kept as unread"));
                continue;
            }

            var proof = await LandedProofs.ProveAsync(root, tip, line, forms, ct).ConfigureAwait(false);
            // PLUGHOOK1a: a fourth proof, the platform's word confirmed by git, where D102's could not see the work on the line.
            var verdict = entry.PullRequestState is { } kept
                ? await PullRequestAsks.VerdictAsync(root, line, forms, kept, tip, ct).ConfigureAwait(false)
                : null;
            if (proof.Kind is not (LandedKind.OnLine or LandedKind.Merged) && verdict is { Clears: true })
            {
                proof = proof with
                {
                    Kind = LandedKind.PullRequest, Where = verdict.Form, Files = [], Detail = null, Judgeable = false,
                    // FABRIC-END4a, third review: the form its merge commit was found on, at the commit read.
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
                    // FABRIC-END4a, third review: it rests on that branch at the tip judged, and on what that one's proof rests on.
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
                    // What the platform last said, and why it does not clear the branch where it does not (design §2.3, §2.5).
                    State = entry.PullRequestState,
                    StateCode = verdict is { Clears: false } ? verdict.Code : null,
                    AskFailed = entry.PullRequestAskFailed,
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
    /// The landings of one repository whose branch went outside Daoris, said on git's own word (CLEANUP1): a standing entry whose branch the
    /// refs do not list is retired as a trace once git says it is missing, and a trace from before <see cref="LandedBranch.GoneAs"/> was kept
    /// is asked once. Missing, with no branch of its name in another case standing, is <see cref="LandedGone.Missing"/>, the person's end of
    /// it; a branch of its name standing is <see cref="LandedGone.Stands"/>; a read git could not make writes nothing, and is asked again at
    /// the next look. No git where nothing needs asking.
    /// </summary>
    /// <returns>How many entries were written.</returns>
    internal async Task<int> MarkAbsentAsync(string root, string repository, CancellationToken ct, RefSnapshot? refs = null)
    {
        // Asked: a standing entry (its end held or not), and a trace that says no why, neither removed by Daoris. A held trace reads ended.
        var asking = context.Recorded.Entries()
            .Where(each => string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase) && each.RemovedAs is null && each.GoneAs is null
                           && (each.GoneAt is null || each.Held is null))
            .ToList();
        if (asking.Count == 0) return 0;
        refs ??= RefSnapshot.Take(root);
        var written = 0;
        foreach (var branch in asking.Select(each => each.Branch).Distinct(StringComparer.Ordinal))
        {
            var standing = asking.Any(each => each.Branch == branch && each.GoneAt is null);
            if (await TreeRefs.CommitOfAsync(root, $"refs/heads/{branch}", ct, refs).ConfigureAwait(false) is not null)
            {
                // A standing entry whose branch stands is the clean-up's to judge; a trace whose name stands again is a doubt, said once.
                if (standing) continue;
                context.Recorded.GoneAs(repository, branch, LandedGone.Stands);
                written++;
                continue;
            }

            if ((await AbsenceAsync(root, branch, refs, ct).ConfigureAwait(false)).Why is not { } why) continue;
            context.Recorded.GoneAs(repository, branch, why);
            written++;
        }

        return written;
    }

    /// <summary>
    /// Git's word on a landing's branch that did not resolve (CLEANUP1): <see cref="LandedGone.Missing"/> where <c>show-ref --exists</c>
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
    /// A recorded branch git could not read (RECORDUNREAD1): kept as <see cref="LandedKind.Unknown"/> with why, at no tip, so no removal and
    /// no judgement again takes it, and its record stays for the next read.
    /// </summary>
    private static Judged Unread(string repository, string workspace, LandedBranch entry, string why) =>
        new(new LandedItem(repository, workspace, entry.Branch, LandedKind.Unknown, null, [], why, entry.PullRequest, 0)
        {
            State = entry.PullRequestState,
            AskFailed = entry.PullRequestAskFailed,
        }, "");

    /// <summary>
    /// How many commits the branch holds that its remote-tracking branch does not, where it has one — its
    /// upstream, else <c>origin/&lt;branch&gt;</c> — or null where it has none: never pushed, or its remote
    /// branch deleted and pruned since.
    /// </summary>
    /// <param name="refs">The judgement's refs (PROC1d-ii), where they hold the upstream and origin's copy; null asks git.</param>
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
    /// A session branch that stays and shares commits with this one that the line does not hold: D88 counts
    /// this branch as holding them, so removing it would leave that session's work looking unlanded. The look asks it of the
    /// session branches it leaves (AUTOTIDY2b).
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
