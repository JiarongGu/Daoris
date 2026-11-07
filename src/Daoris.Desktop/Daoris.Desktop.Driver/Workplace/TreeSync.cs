using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;

namespace Daoris.Driver;

/// <summary>
/// Bringing repositories up to date (out of <c>SessionTrees.Sync.cs</c>): which repositories a look or a
/// press takes, the fetch, the look, and the press over each line's pull, each branch's replay and the landed branches that go.
/// </summary>
/// <remarks>
/// It takes the two landing steps it reads through its constructor: the pull request's asks (<c>AskStatesAsync</c>, the
/// look's one occasion to ask) and the clean-up (<c>CleanLandedAsync</c>, the press's deletions), and names the landing's
/// <see cref="LandedProofs.EntriesOf"/> by its class.
/// </remarks>
internal sealed class TreeSync(
    TreeContext context, PullRequestAsks asks, LandedCleanups cleanups, BranchJudging judging, BranchReplays replays)
{
    /// <summary>What the press says of a row that is no longer what the person saw listed.</summary>
    internal const string LeftSince = "it changed since the list, and is left as it was";

    /// <summary>
    /// Bringing repositories up to date after a pull request merged, listed first: fetch each line from
    /// <c>origin</c> where asked, then say what the press would do — the line's fast-forward, each branch's replay
    /// onto it, and each landed branch whose work reached it. No branch of the person's moves.
    /// </summary>
    /// <param name="fetch">Whether to fetch each line first. A fetch moves only origin's own refs here.</param>
    /// <param name="scope">
    /// Which repositories to take; null for the default, those holding a branch of Daoris's. Every other one
    /// comes back in <see cref="SyncPlan.Apart"/>, not fetched and not judged.
    /// </param>
    internal async Task<SyncPlan> SyncPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        bool fetch = true, CancellationToken ct = default, SyncScope? scope = null)
    {
        // One look: git's guess at each checkout's line is asked once, not by each judgement.
        using var look = context.OneLook();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var lines = new List<LinePull>();
        var rebases = new List<RebaseItem>();
        var deletes = new List<LandedItem>();
        var (looked, apart) = await ScopedAsync(repositories, scope ?? SyncScope.Held, ct).ConfigureAwait(false);
        // The network first, a few repositories at a time; then each judged in turn, from what it fetched.
        var fetches = await FetchedAsync(looked, fetch, ct).ConfigureAwait(false);
        // After its fetch, which is where a merge commit first reaches this machine, the look asks where git cannot tell,
        // as one occasion over every repository it took. The press asks nothing, and a look
        // that fetched nothing has nothing new to ask about.
        IReadOnlyList<StateAnswer> asked = fetch
            ? await asks.AskStatesAsync(looked.Select(each => (each.Root, each.Repository, RemoteTarget.Workspace(each.Space))), landedMayGo: true, ct)
                .ConfigureAwait(false)
            : [];
        foreach (var ((repository, space, root, _), fetched) in looked.Zip(fetches))
        {
            var workspace = RemoteTarget.Workspace(space);
            var line = fetched.Line;
            // The repository's refs read once for its line and its branches, after its fetch; nothing here writes.
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
    /// Every repository with a checkout here, and whether each holds a branch of Daoris's: what a look takes
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
    private sealed record Fetched(string? Line, string? Failed = null, DateTimeOffset? LastFetch = null, string? Reach = null, string? Host = null)
    {
        /// <summary>The pull as judged, carrying what a failed fetch leaves it judged against.</summary>
        public LinePull Onto(LinePull pull) => Failed is null ? pull : pull with { LastFetch = LastFetch, Reach = Reach, Host = Host };
    }

    /// <summary>
    /// Each checkout's line, and its fetch where asked: the one network step, a few repositories at a time, so a
    /// look waits about one fetch's time rather than one per repository. A failed fetch is the reason, in git's words,
    /// with when this checkout last heard from origin and how origin is reached, for the words said once before the rows.
    /// </summary>
    private async Task<IReadOnlyList<Fetched>> FetchedAsync(List<Checkout> checkouts, bool fetch, CancellationToken ct) =>
        await AtMostAsync(checkouts, SyncBounds.FetchesAtOnce, async (each, token) =>
        {
            var line = (await context.LineAsync(each.Root, each.Repository, RemoteTarget.Workspace(each.Space), token).ConfigureAwait(false)).Branch;
            if (!fetch) return new Fetched(line);
            var (failed, url) = await FetchAsync(context.Home, each.Root, line, token, each.Repository, RemoteTarget.Workspace(each.Space)).ConfigureAwait(false);
            return failed is null
                ? new Fetched(line)
                : new Fetched(line, failed, await LastFetchAsync(each.Root, line, token).ConfigureAwait(false), ReachOf(url), url is null ? null : GitIdentityActs.GitAddressHost(url));
        }, ct).ConfigureAwait(false);

    /// <summary>The checkouts a scope takes, and every other one, listed apart.</summary>
    private async Task<(List<Checkout> Looked, List<SyncRepository> Apart)> ScopedAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, SyncScope scope, CancellationToken ct)
    {
        var checkouts = await CheckoutsAsync(repositories, ct).ConfigureAwait(false);
        return ([.. checkouts.Where(each => scope.Includes(each.Known))],
            [.. checkouts.Where(each => !scope.Includes(each.Known)).Select(each => each.Known)]);
    }

    /// <summary>
    /// Whether a repository holds a branch of Daoris's: a session branch, or a branch a landing recorded that still
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
    /// The press: each repository's line fast-forwarded, the branches still at work replayed onto it, and the
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
    /// and the landed branch's proof keeps it while one does; once replayed past them it shares only the
    /// line's, and the landed branch can go.</para>
    ///
    /// <para><b>Branches that shared commits keep sharing them.</b> A landing's branch made at a session's tip is
    /// the same commit replayed once, and one inside another is replayed onto the other's new commits, so the
    /// clean-up's proof reads them together afterwards as it did before.</para>
    ///
    /// <para>🔴 <b>No session starts in a tree while it is replayed</b>, <b>and nothing else writes the checkout while
    /// its line is pulled</b>. A repository's pull and replays run with its checkout held alone (<see cref="TreeLock"/>),
    /// which a driver or a conversation takes, shared, from choosing its tree until its record is open, and every other writer of
    /// the checkout alone; the sessions in use are asked again inside the hold. A repository held so has its pull and its replays
    /// left, each said with what held it, for another press.</para>
    /// </remarks>
    /// <param name="only">The rows the person saw listed, as <c>repository:branch</c> (the line's by its branch); null for everything the proofs clear now.</param>
    /// <param name="fetch">Whether to fetch first — the terminal's one command does; the screen's press acts on what its list fetched.</param>
    /// <param name="inUseNow">
    /// The trees sessions running or waiting hold, asked again while a repository's trees are held for its replays
    /// (<see cref="TreeLock"/>): a session opened by a driver since the read of <paramref name="inUse"/> is then seen.
    /// Every door with a service hands it; without it the replays judge by <paramref name="inUse"/> alone.
    /// </param>
    /// <param name="scope">
    /// Which repositories to take; null for the default, those holding a branch of Daoris's. Each repository
    /// a row in <paramref name="only"/> names is taken too: the press acts on what the person was shown.
    /// </param>
    /// <param name="beside">
    /// The ledger's sessions and their trees (<see cref="BranchTidying.HoldersAsync"/>), asked inside the checkout's hold, so a pull waits for
    /// a session working in the checkout. Every door with a service hands it; null asks nothing and pulls as before.
    /// </param>
    internal async Task<SyncDone> SyncAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, bool fetch = false, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUseNow = null, SyncScope? scope = null,
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null)
    {
        // One look: only git's guess is kept; each judgement is still re-read right before it acts.
        using var look = context.OneLook();
        var busy = new HashSet<string>(inUse.Select(Normal), StringComparer.OrdinalIgnoreCase);
        var pulls = new List<PullResult>();
        var rebases = new List<RebaseResult>();
        var deletes = new List<LandedResult>();
        bool Listed(string key) => only is null || only.Contains(key);
        var (looked, apart) = await ScopedAsync(repositories, (scope ?? SyncScope.Held).Listed(only), ct).ConfigureAwait(false);
        // The network first, a few repositories at a time; each is then judged again right before it acts.
        var fetches = await FetchedAsync(looked, fetch, ct).ConfigureAwait(false);

        foreach (var ((repository, space, root, _), fetched) in looked.Zip(fetches))
        {
            var workspace = RemoteTarget.Workspace(space);
            var line = fetched.Line;

            // The checkout held alone from the pull's own judgement through its move and the replays, as every writer
            // of it holds it; let go before the deletions. What held it otherwise is named on the line and on each replay it leaves.
            using var taken = TreeLock.TryWriting(context.Home, root, repository, TreeLock.Updating);
            // The checkout's open acts reconciled under the hold, before the pull's judgement and outside the look's memos; one that
            // still runs holds the pull and the replays for the next look, named.
            var acting = taken is null ? null : await ActReconciler.ForWriterAsync(context.Home, root, ct).ConfigureAwait(false);
            var held = acting is null ? taken : null;
            var holder = taken is null ? TreeLock.Held(repository, TreeLock.HolderOf(context.Home, root)) : acting?.Said;

            // (a) Pull the line.
            var (asJudged, inRoot) = await LinePulls.JudgePullAsync(root, repository, workspace, line, fetched.Failed, ct).ConfigureAwait(false);
            var pull = fetched.Onto(asJudged);
            if (Listed($"{repository}:{line}"))
            {
                // A pull that would move the checkout waits for a session the ledger holds in it, read inside the hold.
                var waits = held is not null && pull.Moves && inRoot
                    ? await CheckoutBeside.WaitsAsync(beside, root, repository, $"`{line}`'s pull", ct).ConfigureAwait(false)
                    : null;
                pulls.Add(!pull.Moves
                    ? new PullResult(pull, false, only is null ? SyncWords.Describe(pull) : $"{SyncWords.Describe(pull)} — {LeftSince}")
                    : held is null
                        ? new PullResult(pull, false, $"{holder}, so `{line}` was left for another look.")
                        : waits is not null
                            ? new PullResult(pull, false, $"{char.ToUpperInvariant(waits[0])}{waits[1..]}.")
                            : await LinePulls.PullAsync(root, pull, inRoot, held, ct).ConfigureAwait(false));
                // A line Daoris moved is followed by the registry at the next look: under the `branch`
                // rule this fast-forward is when a merged set-up first reaches this machine.
                if (pulls[^1].Moved) RegistryFollowing.Moved(context.Home, repository, DateTimeOffset.UtcNow);
            }

            // (c) Replay what still works on it, onto the line as it now stands, the sessions in use asked again inside the hold,
            // since one may have opened since the look `inUse` came from.
            var onto = await OntoAsync(root, line, ct).ConfigureAwait(false);
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
                    rebases.Add(new(each.Item, false, $"`{each.Item.Branch}`: {holder}, so it was left as it was for another look."));
                    continue;
                }

                // A branch bound into an operation bringing sessions together is left as it stands, its operation named.
                if (TogetherGuard.BranchRefusal(context.Home, repository, each.Item.Branch) is { } together)
                {
                    rebases.Add(new(each.Item, false, $"`{each.Item.Branch}`: {together}."));
                    continue;
                }

                rebases.Add(await replays.ReplayAsync(root, repository, workspace, line!, onto!, each, replayed, ct).ConfigureAwait(false));
            }

            // Let go once the replays are done: the deletions below touch no session's tree. (The `using` is for a throw.)
            taken?.Dispose();

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
