using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// Bringing repositories up to date (WSR6, WSR7; PART10j, out of <c>SessionTrees.Sync.cs</c>): which repositories a look or a
/// press takes, the fetch, the look, and the press over each line's pull, each branch's replay and the landed branches that go.
/// </summary>
/// <remarks>
/// It takes the two landing steps it reads through its constructor (PART10l): the pull request's asks (<c>AskStatesAsync</c>, the
/// look's one occasion to ask) and the clean-up (<c>CleanLandedAsync</c>, the press's deletions), and names the landing's
/// <see cref="LandedProofs.EntriesOf"/> by its class.
/// </remarks>
internal sealed class TreeSync(
    TreeContext context, PullRequestAsks asks, LandedCleanups cleanups, BranchJudging judging, BranchReplays replays)
{
    /// <summary>What the press says of a row that is no longer what the person saw listed.</summary>
    internal const string LeftSince = "it changed since the list, and is left as it was";

    /// <summary>
    /// Bringing repositories up to date after a pull request merged (WSR6), listed first: fetch each line from
    /// <c>origin</c> where asked, then say what the press would do — the line's fast-forward, each branch's replay
    /// onto it, and each landed branch whose work reached it. No branch of the person's moves.
    /// </summary>
    /// <param name="fetch">Whether to fetch each line first. A fetch moves only origin's own refs here.</param>
    /// <param name="scope">
    /// Which repositories to take (WSR7, D112); null for the default, those holding a branch of Daoris's. Every other one
    /// comes back in <see cref="SyncPlan.Apart"/>, not fetched and not judged.
    /// </param>
    internal async Task<SyncPlan> SyncPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        bool fetch = true, CancellationToken ct = default, SyncScope? scope = null)
    {
        // One look (UIFREEZE1, PROC1d-i): git's guess at each checkout's line is asked once, not by each judgement.
        using var look = context.OneLook();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var lines = new List<LinePull>();
        var rebases = new List<RebaseItem>();
        var deletes = new List<LandedItem>();
        var (looked, apart) = await ScopedAsync(repositories, scope ?? SyncScope.Held, ct).ConfigureAwait(false);
        // The network first, a few repositories at a time (WSR7); then each judged in turn, from what it fetched.
        var fetches = await FetchedAsync(looked, fetch, ct).ConfigureAwait(false);
        // After its fetch, which is where a merge commit first reaches this machine (D109), the look asks where git cannot tell,
        // as one occasion over every repository it took (PLUGHOOK1c, design §2.1 occasion 3). The press asks nothing, and a look
        // that fetched nothing has nothing new to ask about.
        IReadOnlyList<StateAnswer> asked = fetch
            ? await asks.AskStatesAsync(looked.Select(each => (each.Root, each.Repository, RemoteTarget.Workspace(each.Space))), landedMayGo: true, ct)
                .ConfigureAwait(false)
            : [];
        foreach (var ((repository, space, root, _), fetched) in looked.Zip(fetches))
        {
            var workspace = RemoteTarget.Workspace(space);
            var line = fetched.Line;
            // The repository's refs read once for its line and its branches (PROC1d-ii), after its fetch; nothing here writes.
            var refs = RefSnapshot.Take(root);
            var (asJudged, _) = await LinePulls.JudgePullAsync(root, repository, workspace, line, fetched.Failed, ct, refs).ConfigureAwait(false);
            var pull = fetched.Onto(asJudged);
            lines.Add(pull);

            // What the line will be after the press: origin's tip where the pull moves it, else where it stands.
            var onto = pull.Moves ? pull.To : await OntoAsync(root, line, ct, refs).ConfigureAwait(false);
            var (judged, gone) = await judging.JudgeBranchesAsync(root, repository, workspace, line, onto, busy, ct, asked, refs).ConfigureAwait(false);
            rebases.AddRange(judged.Select(each => each.Item));
            deletes.AddRange(gone);
        }

        return new(lines, rebases, deletes) { Looked = [.. looked.Select(each => each.Known)], Apart = apart };
    }

    /// <summary>
    /// Every repository with a checkout here, and whether each holds a branch of Daoris's (WSR7, D112): what a look takes
    /// by default, read on the machine and never over the network.
    /// </summary>
    internal async Task<IReadOnlyList<SyncRepository>> SyncScopeAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, CancellationToken ct = default) =>
        [.. (await CheckoutsAsync(repositories, ct).ConfigureAwait(false)).Select(each => each.Known)];

    /// <summary>A repository with a checkout here, as a look or a press takes it: its root, and whether it holds Daoris's branches.</summary>
    private sealed record Checkout(string Repository, string? Space, string Root, SyncRepository Known);

    private async Task<List<Checkout>> CheckoutsAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, CancellationToken ct)
    {
        var record = context.Recorded.All();
        var present = repositories
            .Where(each => !string.IsNullOrWhiteSpace(each.Root) && Directory.Exists(each.Root))
            .ToList();
        var holds = await AtMostAsync(present, SyncBounds.FetchesAtOnce,
            (each, token) => HoldsDaorisBranchAsync(each.Root!, each.Repository, record, token), ct).ConfigureAwait(false);
        return [.. present.Zip(holds, (each, held) =>
            new Checkout(each.Repository, each.Workspace, each.Root!, new SyncRepository(each.Repository, RemoteTarget.Workspace(each.Workspace), held)))];
    }

    /// <summary>A checkout's line, and how its fetch went: why it failed, when origin was last heard from here, and how it is reached.</summary>
    private sealed record Fetched(string? Line, string? Failed = null, DateTimeOffset? LastFetch = null, string? Reach = null)
    {
        /// <summary>The pull as judged, carrying what a failed fetch leaves it judged against (WSR7).</summary>
        public LinePull Onto(LinePull pull) => Failed is null ? pull : pull with { LastFetch = LastFetch, Reach = Reach };
    }

    /// <summary>
    /// Each checkout's line, and its fetch where asked (WSR7): the one network step, a few repositories at a time, so a
    /// look waits about one fetch's time rather than one per repository. A failed fetch is the reason, in git's words,
    /// with when this checkout last heard from origin and how origin is reached, for the words said once before the rows.
    /// </summary>
    private async Task<IReadOnlyList<Fetched>> FetchedAsync(List<Checkout> checkouts, bool fetch, CancellationToken ct) =>
        await AtMostAsync(checkouts, SyncBounds.FetchesAtOnce, async (each, token) =>
        {
            var line = (await context.LineAsync(each.Root, each.Repository, RemoteTarget.Workspace(each.Space), token).ConfigureAwait(false)).Branch;
            if (!fetch) return new Fetched(line);
            var (failed, url) = await FetchAsync(each.Root, line, token, each.Repository, RemoteTarget.Workspace(each.Space)).ConfigureAwait(false);
            return failed is null
                ? new Fetched(line)
                : new Fetched(line, failed, await LastFetchAsync(each.Root, line, token).ConfigureAwait(false), ReachOf(url));
        }, ct).ConfigureAwait(false);

    /// <summary>The checkouts a scope takes, and every other one, listed apart (D112).</summary>
    private async Task<(List<Checkout> Looked, List<SyncRepository> Apart)> ScopedAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, SyncScope scope, CancellationToken ct)
    {
        var checkouts = await CheckoutsAsync(repositories, ct).ConfigureAwait(false);
        return ([.. checkouts.Where(each => scope.Includes(each.Known))],
            [.. checkouts.Where(each => !scope.Includes(each.Known)).Select(each => each.Known)]);
    }

    /// <summary>
    /// Whether a repository holds a branch of Daoris's (D112): a session branch, or a branch a landing recorded that still
    /// stands — one list of its local branches, beside the landings record.
    /// </summary>
    private static async Task<bool> HoldsDaorisBranchAsync(string root, string repository, IReadOnlyList<LandedBranch> record, CancellationToken ct)
    {
        var (code, refs, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--format=%(refname)", "refs/heads/"], ct).ConfigureAwait(false);
        // 🔴 A list git could not give is not "nothing of Daoris's": the repository is looked at, and its row says git's words.
        if (code != 0) return true;
        var branches = refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => name.StartsWith("refs/heads/", StringComparison.Ordinal))
            .Select(name => name["refs/heads/".Length..])
            .ToHashSet(StringComparer.Ordinal);
        return branches.Any(branch => branch.StartsWith("daoris/", StringComparison.Ordinal))
               || LandedProofs.EntriesOf(record, repository).Any(entry => branches.Contains(entry.Branch));
    }

    /// <summary>
    /// The press (WSR6): each repository's line fast-forwarded, the branches still at work replayed onto it, and the
    /// landed branches whose work reached it deleted — in that order, each judged again right before it acts.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>This writes into repositories the person owns</b>, so every guard of <see cref="SessionTrees.MergeAsync"/> and
    /// of the clean-up is here, and each refuses rather than repairs. The line moves only forward and only where
    /// nothing is lost: in the repository's own checkout only while it is clean and on the line, checked again
    /// immediately before; elsewhere by moving the ref, and only from the commit it was judged at. No merge
    /// commit, no force, no push. A replay happens in a tree of Daoris's own — the session's, or one made for
    /// it under the trees home and removed after — never the person's checkout; a conflict aborts it and names the
    /// files, leaving the branch as it was, and a replay is kept only once its own work is proven intact. A branch
    /// on its remote is left, since replaying it would need a force push.</para>
    ///
    /// <para><b>Replays before deletions.</b> A session branch that grew from a landed branch shares its commits,
    /// and the landed branch's proof keeps it while one does (WSR5); once replayed past them it shares only the
    /// line's, and the landed branch can go.</para>
    ///
    /// <para><b>Branches that shared commits keep sharing them.</b> A landing's branch made at a session's tip is
    /// the same commit replayed once, and one inside another is replayed onto the other's new commits, so the
    /// clean-up's proof reads them together afterwards as it did before.</para>
    ///
    /// <para>🔴 <b>No session starts in a tree while it is replayed</b> (LEFT2). A repository's replays run with its
    /// trees held alone (<see cref="TreeLock"/>), which a driver or a conversation takes, shared, from choosing its
    /// tree until its record is open; the sessions in use are asked again inside the hold. A repository a session is
    /// starting in has its replays left, each said, for another press.</para>
    /// </remarks>
    /// <param name="only">The rows the person saw listed, as <c>repository:branch</c> (the line's by its branch); null for everything the proofs clear now.</param>
    /// <param name="fetch">Whether to fetch first — the terminal's one command does; the screen's press acts on what its list fetched.</param>
    /// <param name="inUseNow">
    /// The trees sessions running or waiting hold, asked again while a repository's trees are held for its replays
    /// (<see cref="TreeLock"/>, LEFT2): a session a driver opened since <paramref name="inUse"/> was read is then seen.
    /// Every door with a service hands it; without it the replays judge by <paramref name="inUse"/> alone.
    /// </param>
    /// <param name="scope">
    /// Which repositories to take (WSR7, D112); null for the default, those holding a branch of Daoris's. Each repository
    /// a row in <paramref name="only"/> names is taken too: the press acts on what the person was shown.
    /// </param>
    internal async Task<SyncDone> SyncAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, bool fetch = false, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUseNow = null, SyncScope? scope = null)
    {
        // One look (UIFREEZE1, PROC1d-i): only git's guess is kept; each judgement is still re-read right before it acts (D88).
        using var look = context.OneLook();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var pulls = new List<PullResult>();
        var rebases = new List<RebaseResult>();
        var deletes = new List<LandedResult>();
        bool Listed(string key) => only is null || only.Contains(key);
        var (looked, apart) = await ScopedAsync(repositories, (scope ?? SyncScope.Held).Listed(only), ct).ConfigureAwait(false);
        // The network first, a few repositories at a time (WSR7); each is then judged again right before it acts.
        var fetches = await FetchedAsync(looked, fetch, ct).ConfigureAwait(false);

        foreach (var ((repository, space, root, _), fetched) in looked.Zip(fetches))
        {
            var workspace = RemoteTarget.Workspace(space);
            var line = fetched.Line;

            // (a) Pull the line.
            var (asJudged, inRoot) = await LinePulls.JudgePullAsync(root, repository, workspace, line, fetched.Failed, ct).ConfigureAwait(false);
            var pull = fetched.Onto(asJudged);
            if (Listed($"{repository}:{line}"))
            {
                pulls.Add(pull.Moves
                    ? await LinePulls.PullAsync(root, pull, inRoot, ct).ConfigureAwait(false)
                    : new PullResult(pull, false, only is null ? SyncWords.Describe(pull) : $"{SyncWords.Describe(pull)} — {LeftSince}"));
                // A line Daoris moved is followed by the registry at the next look (WSSETUP5, D124 §3.1): under the `branch`
                // rule this fast-forward is when a merged set-up first reaches this machine.
                if (pulls[^1].Moved) RegistryFollowing.Moved(context.Home, repository, DateTimeOffset.UtcNow);
            }

            // (c) Replay what still works on it, onto the line as it now stands — with the repository's trees held
            // alone (LEFT2), so no session starts in one while it is rebased, and the sessions in use asked again
            // inside that hold, since one may have opened since the look `inUse` came from.
            var onto = await OntoAsync(root, line, ct).ConfigureAwait(false);
            using var held = TreeLock.TryReplaying(context.Home, workspace, repository);
            var busyNow = held is not null && inUseNow is not null
                ? new HashSet<string>((await inUseNow(ct).ConfigureAwait(false)).Select(Normal), StringComparer.OrdinalIgnoreCase)
                : busy;
            var (judged, _) = await judging.JudgeBranchesAsync(root, repository, workspace, line, onto, busyNow, ct).ConfigureAwait(false);
            var replayed = new List<BranchReplays.Replayed>();
            foreach (var each in await BranchReplays.DeepestLastAsync(root, judged, ct).ConfigureAwait(false))
            {
                var key = $"{repository}:{each.Item.Branch}";
                if (!Listed(key)) continue;
                if (!each.Item.Replays)
                {
                    rebases.Add(new(each.Item, false, only is null ? SyncWords.Describe(each.Item) : $"{SyncWords.Describe(each.Item)} — {LeftSince}"));
                    continue;
                }

                if (held is null)
                {
                    rebases.Add(new(each.Item, false, $"`{each.Item.Branch}`: {TreeLock.Starting(repository)}."));
                    continue;
                }

                rebases.Add(await replays.ReplayAsync(root, repository, workspace, line!, onto!, each, replayed, ct).ConfigureAwait(false));
            }

            // Let go once the replays are done: the deletions below touch no session's tree. (The `using` is for a throw.)
            held?.Dispose();

            // (b) Delete what merged: the landed half of the clean-up, after the replays, and never a branch just replayed.
            var replayedNow = judged.Where(each => each.Item.Replays).Select(each => $"{repository}:{each.Item.Branch}").ToHashSet(StringComparer.Ordinal);
            var deleteOnly = only is null ? null : only.Where(key => !replayedNow.Contains(key)).ToHashSet(StringComparer.Ordinal);
            var cleaned = await cleanups.CleanLandedAsync([(repository, space, root)], deleteOnly, ct).ConfigureAwait(false);
            deletes.AddRange(cleaned.Where(result => result.Removed || result.Message != "kept"
                || result.Item.Kind is LandedKind.OnLine or LandedKind.Merged or LandedKind.Inside or LandedKind.LeanedOn));

            await replays.ForgetGoneAsync(root, repository, ct).ConfigureAwait(false);
        }

        return new(pulls, rebases, deletes) { Apart = apart };
    }
}
