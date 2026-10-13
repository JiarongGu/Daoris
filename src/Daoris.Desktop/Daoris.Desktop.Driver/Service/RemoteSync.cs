namespace Daoris.Driver;

/// <param name="Problem">
/// Null on a clean pass. Records sync eventually (D47 §2), so a problem here is reported and retried
/// next tick rather than failing the tick — but it is REPORTED, because a feed dying quietly (an
/// expired key, an unjoined repository) looks exactly like a family with nothing to say. What a clean
/// pass moved is deliberately not carried: a healthy sync moves something almost every tick, so
/// per-tick counts would be noise on every surface that renders events — they return with a surface
/// that reads them.
/// </param>
/// <param name="Notes">
/// What the remote understood and deliberately did not take (D48 §6) — a feed from a stale checkout,
/// or from a line that is not the repository's canonical one. Reported, never treated as a failure:
/// those are the rules working, and a loop that logged them as problems would teach the person to
/// ignore its problems.
/// </param>
public sealed record SyncReport(string? Problem, IReadOnlyList<string> Notes)
{
    public SyncReport(string? problem) : this(problem, []) { }

    public static readonly SyncReport Clean = new((string?)null, []);
}

/// <summary>
/// This machine's syncs — one per workspace that has a remote (D48 §5). What the driver holds and
/// ticks; a circle with no entry in the map syncs nowhere, silently (D21).
/// </summary>
/// <remarks>
/// One circle's wall does not stop another's pass: each runs, each reports, and the tick carries
/// whatever trouble there was with the workspace's name on it — "the sync is failing" is not a useful
/// sentence on a machine that holds two deployments' keys.
/// </remarks>
public sealed class RemoteSyncSet : IDisposable
{
    private readonly Func<IReadOnlyDictionary<string, RemoteTarget>>? _load;
    private readonly string _localUrl = "";
    private readonly string? _localKey;
    private readonly HttpMessageHandler? _handler;
    private readonly Dictionary<string, (RemoteTarget Target, RemoteSync Sync)> _watched =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<RemoteSync> _syncs;

    /// <summary>A fixed set — what a test composes. The machine's own set watches the map instead.</summary>
    public RemoteSyncSet(IReadOnlyList<RemoteSync> syncs) => _syncs = syncs;

    private RemoteSyncSet(
        string localUrl, string? localKey, Func<IReadOnlyDictionary<string, RemoteTarget>> load,
        HttpMessageHandler? handler)
    {
        _localUrl = localUrl;
        _localKey = localKey;
        _load = load;
        _handler = handler;
        _syncs = [];
        Refresh();
    }

    /// <summary>
    /// This machine's syncs, re-read from the remotes map on every pass (SYNC0d). An empty map is an
    /// empty set that syncs nowhere, silently (D21) — never a null the loop holds for good.
    /// </summary>
    /// <remarks>
    /// The local key arrives from the caller, which has already read it for its own client: a second
    /// ambient environment read here would be a hidden input the caller cannot see or test.
    /// </remarks>
    public static RemoteSyncSet FromEnvironment(string localUrl, string? localKey) =>
        Watching(localUrl, localKey, RemoteTarget.Load);

    /// <summary>
    /// A set that reads the map it is given on every pass: a circle wired since the last pass syncs
    /// on this one, a changed key is used, and a removed circle stops.
    /// </summary>
    /// <remarks>
    /// 🔴 Built once, the set synced nothing a person wired after the loop started until a restart,
    /// while the remotes editor told them the loop re-reads the map on its next pass. A circle whose
    /// url and key are unchanged keeps its sync, clients and all, so a steady map costs one file read.
    /// </remarks>
    /// <param name="handler">The test seam, as on <see cref="RemoteSync"/>.</param>
    public static RemoteSyncSet Watching(
        string localUrl, string? localKey, Func<IReadOnlyDictionary<string, RemoteTarget>> load,
        HttpMessageHandler? handler = null) =>
        new(localUrl, localKey, load, handler);

    /// <summary>The circles this machine syncs as of the last pass — what the driver announces.</summary>
    public IReadOnlyList<string> Workspaces => _syncs.Select(sync => sync.Workspace).ToList();

    /// <summary>Bring the set in line with the map: keep what is unchanged, rebuild what changed.</summary>
    private void Refresh()
    {
        if (_load is null) return;

        var map = _load();
        foreach (var gone in _watched.Keys.Where(name => !map.ContainsKey(name)).ToList())
        {
            _watched[gone].Sync.Dispose();
            _watched.Remove(gone);
        }

        foreach (var (name, target) in map)
        {
            if (_watched.TryGetValue(name, out var held) && held.Target == target) continue;
            if (held.Sync is not null) held.Sync.Dispose();
            _watched[name] = (target, new RemoteSync(_localUrl, _localKey, name, target, _handler));
        }

        _syncs = _watched
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Value.Sync)
            .ToList();
    }

    public Task<SyncReport> RunOnceAsync(CancellationToken ct = default) => RunOnceAsync(workspace: null, ct);

    /// <summary>
    /// One pass at a time through this set. The tick and *Sync now* share it (SYNC6b), and a pass
    /// re-reads the map into the set's own dictionary, which two passes at once would corrupt.
    /// </summary>
    private readonly SemaphoreSlim _pass = new(1, 1);

    /// <summary>One pass — of every circle, or of the one <paramref name="workspace"/> names (SYNC6a).</summary>
    /// <exception cref="DriverException">A circle was named that this machine has no remote for.</exception>
    public async Task<SyncReport> RunOnceAsync(string? workspace, CancellationToken ct = default)
    {
        await _pass.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RunLockedAsync(workspace, ct).ConfigureAwait(false);
        }
        finally
        {
            _pass.Release();
        }
    }

    private async Task<SyncReport> RunLockedAsync(string? workspace, CancellationToken ct)
    {
        Refresh();
        var syncs = workspace is null
            ? _syncs
            : _syncs.Where(sync => string.Equals(sync.Workspace, RemoteTarget.Workspace(workspace), StringComparison.OrdinalIgnoreCase)).ToList();
        if (workspace is not null && syncs.Count == 0)
        {
            throw new DriverException(
                $"this machine has no remote for `{RemoteTarget.Workspace(workspace)}` — `daoris remote add` wires one, "
                + "and a workspace with none syncs nowhere.");
        }

        var problems = new List<string>();
        var notes = new List<string>();
        foreach (var sync in syncs)
        {
            var report = await sync.RunOnceAsync(ct).ConfigureAwait(false);
            if (report.Problem is not null) problems.Add($"{sync.Workspace}: {report.Problem}");
            foreach (var note in report.Notes) notes.Add($"{sync.Workspace}: {note}");
        }

        return new(problems.Count == 0 ? null : string.Join(" · ", problems), notes);
    }

    public void Dispose()
    {
        foreach (var sync in _syncs) sync.Dispose();
    }
}

/// <summary>
/// One pass of ONE WORKSPACE's local↔remote sync (D47 §9, D48 §5): feed that circle's joined
/// registrations and each sharing repository's content UP, each ordered by the commit it speaks for;
/// carry the retires this machine owes the circle; keep the team's rows here current, updated and
/// removed (SYNC5b); then ask the host to fetch, rebase and push the circle's quests and records (D68).
/// Registrations go first, so the remote knows who is joined before their records and quests arrive.
/// Runs on the driver's own tick — a server machine running `daoris-driver` with a key is just another
/// machine, not a special deployment.
/// </summary>
/// <remarks>
/// One instance per workspace, because one shared deployment serves one workspace (D48 §5) — a
/// machine with two circles runs two of these, against two hosts, with two keys.
/// <see cref="RemoteSyncSet"/> is what the driver holds.
/// </remarks>
public sealed class RemoteSync : IDisposable
{
    private readonly HttpClient _local;
    private readonly HttpClient _remote;
    private readonly string _localBase;
    private readonly string _remoteBase;
    private readonly string _workspace;

    /// <param name="workspace">Which circle this sync serves — the only rows it may speak for.</param>
    /// <param name="handler">The test seam: a stub transport for both hosts, distinguished by URL.
    /// Production callers pass none. `ServiceClient` has the same seam for the same reason.</param>
    public RemoteSync(
        string localUrl, string? localKey, string workspace, RemoteTarget target,
        HttpMessageHandler? handler = null)
    {
        _localBase = localUrl.TrimEnd('/');
        _remoteBase = target.Url;
        _workspace = RemoteTarget.Workspace(workspace);
        _local = DriverHttp.Client(localKey, handler);
        _remote = DriverHttp.Client(target.Key, handler);
    }

    /// <summary>The circle this sync feeds and mirrors — what a report names when it has trouble.</summary>
    public string Workspace => _workspace;

    public void Dispose()
    {
        _local.Dispose();
        _remote.Dispose();
    }

    public async Task<SyncReport> RunOnceAsync(CancellationToken ct = default)
    {
        var notes = new List<string>();
        string? wall = null;
        try
        {
            // Read the registry UNSCOPED and filter here, deliberately. The joined half is this
            // workspace's alone (§5) — but the guard half must span the whole machine, because it is
            // what stops a teammate's row overwriting a local registration that happens to share a
            // name in another circle, root and all. A scoped read would make that guard blind by half.
            var registryJson = await DriverHttp.GetAsync(_local, $"{_localBase}/api/registry", ct).ConfigureAwait(false);
            var retired = RemoteSyncPayloads.Retired(await DriverHttp.GetAsync(
                _local, $"{_localBase}/api/registry/retired?workspace={Uri.EscapeDataString(_workspace)}", ct)
                .ConfigureAwait(false));

            // Every WIRED circle gets its pass (sync design §6), whether or not anything here joins it:
            // nothing leaves that the manifests do not declare, while the team's rows and quests still
            // come down — a machine that joined nothing still addresses its teammates, and *Sync now*
            // on such a circle must do what it says.
            var joined = RemoteSyncPayloads.Joined(registryJson, _workspace);
            notes.AddRange(await FeedUpAsync(joined, ct).ConfigureAwait(false));
            var remoteRegistryJson = await DriverHttp.GetAsync(_remote, $"{_remoteBase}/api/registry", ct).ConfigureAwait(false);
            var told = await CarryRetiresAsync(retired, remoteRegistryJson, joined, ct).ConfigureAwait(false);
            await MirrorRegistryAsync(remoteRegistryJson, registryJson, told, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Cancellation belongs to the caller, never converted into a sync problem.
        }
        catch (Exception error) when (IsWall(error))
        {
            // Report and carry on: records sync eventually and the next tick retries — but a feed
            // dying quietly looks exactly like a family with nothing to say, so the wall is named.
            wall = error.Message;
        }

        // 🔴 The host's pass runs whatever the feed met (SYNC6b). It is where a try is RECORDED, so a
        // feed that stopped the pass at a remote that was down left the circle's standing saying
        // "synced" while the sync had just failed. The quests stand on their own besides.
        try
        {
            notes.AddRange(await SyncQuestsAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (IsWall(error))
        {
            wall ??= error.Message;
        }

        return wall is not null ? new(wall, notes) : notes.Count == 0 ? SyncReport.Clean : new(null, notes);
    }

    private static bool IsWall(Exception error) =>
        error is HttpRequestException or TaskCanceledException or DriverException or System.Text.Json.JsonException;

    /// <summary>
    /// Each joined repository UP: its registration, then its content — the remote must know who is
    /// joined before their records and content arrive (D47 §9). Session records ride the host's pass
    /// with the quests (SYNC4).
    /// </summary>
    /// <returns>What the remote deliberately did not take, and what waits on a commit, in one sentence each.</returns>
    private async Task<IReadOnlyList<string>> FeedUpAsync(
        IReadOnlyList<RemoteSyncPayloads.JoinedRepository> joined, CancellationToken ct)
    {
        var notes = new List<string>();
        foreach (var repo in joined) notes.AddRange(await FeedAsync(repo, ct).ConfigureAwait(false));
        return notes;
    }

    /// <summary>The file a registration is read from: the declaration IS the manifest.</summary>
    private const string Manifest = "daoris.json";

    private async Task<IReadOnlyList<string>> FeedAsync(RemoteSyncPayloads.JoinedRepository repo, CancellationToken ct)
    {
        var notes = new List<string>();
        var name = Uri.EscapeDataString(repo.Repository);

        // The canonical line rides the registration, because the deployment cannot ask git and this
        // machine can (D48 §6). Read per tick rather than cached: a repository's default branch changes
        // about once in its life, and the tick that follows should know.
        // The line the person set wins over the checkout's guess here too (WSR2): the deployment orders
        // feeds by the line this machine says is canonical, and one answer for every door is the point.
        // No home is no choices, and the guess alone, as before; the file is the one every door resolves (CONFIGSEAM1).
        var configPath = DriverConfig.FindPath();
        var choices = configPath is null ? DriverConfig.Empty : DriverConfig.Load(configPath);
        var defaultBranch = (await CanonicalLine.ResolveAsync(repo.Root, repo.Repository, _workspace, choices, ct).ConfigureAwait(false)).Branch;

        // Where this checkout stands, asked of git at the moment of feeding — the claim the deployment
        // will compare against what it holds (D48 §6).
        var provenance = await WorkingTree.ProvenanceAsync(repo.Root, ct).ConfigureAwait(false);
        var clean = provenance is not null && (await WorkingTree.CleanAsync(repo.Root, ct).ConfigureAwait(false)).Clean;

        // The declaration is the manifest, so a registration speaks for the commit while the manifest is
        // as that commit has it — whatever else is in flight (SYNC5b). A modified manifest names none,
        // and the deployment keeps a declaration that named one over it.
        var declaredAt = provenance is not null
            && (clean || await WorkingTree.UnmodifiedAsync(repo.Root, Manifest, ct).ConfigureAwait(false))
                ? provenance
                : null;
        var held = declaredAt is null
            ? RemoteSyncPayloads.HeldCommits.None
            : RemoteSyncPayloads.Held(await DriverHttp.GetAsync(
                _remote, $"{_remoteBase}/api/feed/held?repository={name}", ct).ConfigureAwait(false));
        var ordering = declaredAt is null ? null : new Ordering(repo, declaredAt);

        var registration = ordering is null
            ? RemoteSyncPayloads.Registration(repo, defaultBranch)
            : await ordering.PlanAsync("registration", held.Registration, ct).ConfigureAwait(false) is { Feed: true } declared
                ? RemoteSyncPayloads.Registration(repo, defaultBranch, declaredAt, declared.Base)
                : null;
        if (registration is not null
            && await DriverHttp.PostInformableAsync(_remote, $"{_remoteBase}/api/registry", registration, ct)
                .ConfigureAwait(false) is { } registered)
        {
            notes.Add(registered);
        }

        if (!repo.SharesKnowledge) return [.. notes, .. ordering?.Waiting() ?? []];

        if (provenance is null)
        {
            // The door would refuse this feed, and rightly — but only THIS side knows why, because only
            // this side has the tree. Saying it here turns "the deployment refused something" into "that
            // checkout has no history to speak from", which is the actionable sentence.
            notes.Add(
                $"`{repo.Repository}` fed no knowledge: git could not say where this checkout stands, "
                + "and a deployment takes knowledge only from a named commit. Its records still travel.");
            return notes;
        }

        // A feed speaks for a commit, and the host's index and map are read from the working tree — so
        // a tree with work in flight would send that work under the commit's name (SYNC5a).
        if (!clean)
        {
            notes.Add(
                $"`{repo.Repository}` fed no knowledge and no code map: its checkout has uncommitted changes, "
                + $"and a feed speaks for a commit (`{provenance.ShortCommit}`) — what is uncommitted is not yet "
                + "that commit's. Its records still travel.");
            return [.. notes, .. ordering?.Waiting() ?? []];
        }

        var knowledge = await ordering!.PlanAsync("knowledge", held.Knowledge, ct).ConfigureAwait(false);
        if (knowledge.Feed)
        {
            var content = RemoteSyncPayloads.Entries(repo.Repository, await DriverHttp.GetAsync(
                _local, $"{_localBase}/api/entries?repository={name}", ct)
                .ConfigureAwait(false), provenance, knowledge.Base);

            if (await DriverHttp.PostInformableAsync(
                    _remote, $"{_remoteBase}/api/feed/entries", content.Json, ct).ConfigureAwait(false)
                is { } note)
            {
                notes.Add(note);
            }
        }

        var map = await ordering.PlanAsync("code map", held.CodeMap, ct).ConfigureAwait(false);
        if (map.Feed)
        {
            var feed = RemoteSyncPayloads.CodeMap(repo.Repository, await DriverHttp.GetAsync(
                _local, $"{_localBase}/api/code-map/{name}", ct).ConfigureAwait(false), provenance, map.Base);

            if (feed.Problem is { } problem)
            {
                notes.Add($"`{repo.Repository}`'s code map was not fed: {problem}");
            }
            else if (await DriverHttp.PostInformableAsync(
                    _remote, $"{_remoteBase}/api/feed/code-map", feed.Json!, ct).ConfigureAwait(false)
                is { } note)
            {
                notes.Add(note);
            }
        }

        return [.. notes, .. ordering.Waiting()];
    }

    /// <summary>
    /// One checkout's feeds, ordered against what the deployment holds (SYNC5a, SYNC5b): git is asked
    /// once per held commit, and what waits on one commit is said once, naming everything that does.
    /// </summary>
    private sealed class Ordering(RemoteSyncPayloads.JoinedRepository repo, TreeProvenance here)
    {
        private readonly Dictionary<string, TreeRelation?> _asked = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(string What, string Held)> _waiting = [];

        /// <summary>Ask git how this checkout stands to what is held — only when there is something to ask.</summary>
        public async Task<RemoteSyncPayloads.FeedPlan> PlanAsync(string what, string? held, CancellationToken ct)
        {
            TreeRelation? relation = null;
            if (held is not null
                && !string.Equals(held, here.Commit, StringComparison.OrdinalIgnoreCase)
                && !_asked.TryGetValue(held, out relation))
            {
                _asked[held] = relation = await WorkingTree.RelationAsync(repo.Root, held, here.Commit, ct).ConfigureAwait(false);
            }

            var plan = RemoteSyncPayloads.Order(repo.Repository, what, here, held, relation);
            if (!plan.Feed) _waiting.Add((what, held!));
            return plan;
        }

        /// <summary>What is not fed, one sentence per held commit.</summary>
        public IEnumerable<string> Waiting() => _waiting
            .GroupBy(wait => wait.Held, StringComparer.OrdinalIgnoreCase)
            .Select(group => RemoteSyncPayloads.Order(
                repo.Repository, RemoteSyncPayloads.Whats(group.Select(wait => wait.What).ToList()),
                here, group.Key, _asked[group.Key]).Note!);
    }

    /// <summary>
    /// The retires this machine owes the circle, carried to its deployment and then cleared (SYNC5b).
    /// </summary>
    /// <remarks>
    /// A retire goes only where the circle still lists the repository: a circle that never heard of it
    /// has nothing to take back, and telling it would name a repository to a deployment that was never
    /// told of it. One owed for a repository joined here again is void — the store clears those as the
    /// row rejoins, and this is the same rule said once more where it is spent.
    /// </remarks>
    /// <returns>What this pass retired at the circle — still in the list it read, and not to come back down.</returns>
    private async Task<IReadOnlyList<string>> CarryRetiresAsync(
        IReadOnlyList<string> retired, string remoteRegistryJson,
        IReadOnlyList<RemoteSyncPayloads.JoinedRepository> joined, CancellationToken ct)
    {
        if (retired.Count == 0) return [];

        var listed = RemoteSyncPayloads.Names(remoteRegistryJson);
        var joinedHere = joined.Select(repo => repo.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var told = new List<string>();
        foreach (var repository in retired)
        {
            var name = Uri.EscapeDataString(repository);
            if (listed.Contains(repository) && !joinedHere.Contains(repository))
            {
                await DriverHttp.DeleteAsync(_remote, $"{_remoteBase}/api/registry/{name}", ct).ConfigureAwait(false);
                told.Add(repository);
            }

            await DriverHttp.DeleteAsync(
                _local, $"{_localBase}/api/registry/retired/{name}?workspace={Uri.EscapeDataString(_workspace)}", ct)
                .ConfigureAwait(false);
        }

        return told;
    }

    /// <summary>
    /// The remote's registry kept current here (SYNC5b): the team's rows written when new or changed, and
    /// this circle's copies retired when the circle no longer lists them. Everything this machine holds
    /// with a root keeps its own registration.
    /// </summary>
    /// <remarks>
    /// The rows are filed in THIS sync's workspace. That is not a feed naming its own circle (which WSP1
    /// forbids, and still does — the remote's answer carries no workspace anyone reads): it is the
    /// receiving machine's own wiring deciding, since a row arriving from this workspace's deployment
    /// belongs to this workspace by construction (D48 §2/§5).
    /// </remarks>
    private async Task MirrorRegistryAsync(
        string remoteRegistryJson, string localRegistryJson, IReadOnlyList<string> retiredHere, CancellationToken ct)
    {
        var mirror = RemoteSyncPayloads.Mirror(remoteRegistryJson, localRegistryJson, _workspace, retiredHere);
        foreach (var (_, payload) in mirror.Write)
        {
            await DriverHttp.PostAsync(_local, $"{_localBase}/api/registry", payload, ct).ConfigureAwait(false);
        }

        foreach (var gone in mirror.Retire)
        {
            await DriverHttp.DeleteAsync(_local, $"{_localBase}/api/registry/{Uri.EscapeDataString(gone)}", ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fetch, rebase, push for this circle's quests, then its session records both ways — asked of this
    /// machine's HOST, which runs the pass (D69, SYNC4): a take claims by push from the door it was made
    /// at, and the tick runs the same code by asking for it, so there is one implementation of the sync
    /// and not two that drift.
    /// </summary>
    /// <returns>
    /// What a person should hear: a move of this machine's that became a conflict, a quest the remote
    /// would not take, a quest still behind. None is a failure of the sync, which worked.
    /// </returns>
    /// <exception cref="DriverException">The pass hit a wall — named, and reported as the sync's problem.</exception>
    private async Task<IReadOnlyList<string>> SyncQuestsAsync(CancellationToken ct)
    {
        var pass = RemoteSyncPayloads.Pass(await DriverHttp.PostAsync(
            _local, $"{_localBase}/api/sync?workspace={Uri.EscapeDataString(_workspace)}", "{}", ct)
            .ConfigureAwait(false));

        if (!pass.Wired)
        {
            // The driver has a remote for this circle and the host that holds the quests does not: two
            // readers of two different maps. Said, because a sync that silently skipped quests would
            // look exactly like a circle with nothing to share.
            return [$"this machine's host has no remote for `{_workspace}`, so its quests and records did not sync — "
                    + "the host and the driver are reading different remotes maps."];
        }

        return pass.Problem is { } wall ? throw new DriverException($"quests: {wall}") : pass.Notes;
    }
}
