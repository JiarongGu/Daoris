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
public sealed class RemoteSyncSet(IReadOnlyList<RemoteSync> syncs) : IDisposable
{
    /// <summary>The machine's syncs, when it has any remote at all — null otherwise, silently (D21).
    /// The local key arrives from the caller, which has already read it for its own client — a second
    /// ambient environment read here would be a hidden input the caller cannot see or test.</summary>
    public static RemoteSyncSet? FromEnvironment(string localUrl, string? localKey)
    {
        var remotes = RemoteTarget.Load();
        return remotes.Count == 0
            ? null
            : new RemoteSyncSet(remotes
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new RemoteSync(localUrl, localKey, entry.Key, entry.Value))
                .ToList());
    }

    /// <summary>The circles this machine syncs — what the driver announces when it comes up.</summary>
    public IReadOnlyList<string> Workspaces => syncs.Select(sync => sync.Workspace).ToList();

    public async Task<SyncReport> RunOnceAsync(CancellationToken ct = default)
    {
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
        foreach (var sync in syncs) sync.Dispose();
    }
}

/// <summary>
/// One pass of ONE WORKSPACE's local↔remote sync (D47 §9, D48 §5): feed that circle's joined
/// registrations, this machine's session records, and each sharing repository's content UP; pull the
/// remote's registry (foreign rows only) and the quests touching this family DOWN into the local
/// mirror. Registrations go first, so the remote knows who is joined before their records arrive. Runs
/// on the driver's own tick — a server machine running `daoris-driver` with a key is just another
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
        try
        {
            // Read the registry UNSCOPED and filter here, deliberately. The joined half is this
            // workspace's alone (§5) — but the names half must span the whole machine, because it is
            // what stops a foreign row overwriting a local registration that happens to share a name
            // in another circle, root and all. A scoped read would make that guard blind by half.
            var registryJson = await DriverHttp.GetAsync(_local, $"{_localBase}/api/registry", ct).ConfigureAwait(false);
            var joined = RemoteSyncPayloads.Joined(registryJson, _workspace);
            if (joined.Count == 0) return SyncReport.Clean;

            var notes = await FeedUpAsync(joined, ct).ConfigureAwait(false);
            await MirrorDownAsync(
                RemoteSyncPayloads.Names(registryJson),
                joined.Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase),
                ct).ConfigureAwait(false);

            return notes.Count == 0 ? SyncReport.Clean : new(null, notes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Cancellation belongs to the caller, never converted into a sync problem.
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or DriverException or System.Text.Json.JsonException)
        {
            // Report and carry on: records sync eventually and the next tick retries — but a feed
            // dying quietly looks exactly like a family with nothing to say, so the wall is named.
            return new(error.Message);
        }
    }

    /// <summary>Registrations, then records, then content — the remote must know who is joined before
    /// their records arrive (D47 §9).</summary>
    /// <returns>What the remote deliberately did not take, in its own words.</returns>
    private async Task<IReadOnlyList<string>> FeedUpAsync(
        IReadOnlyList<RemoteSyncPayloads.JoinedRepository> joined, CancellationToken ct)
    {
        var notes = new List<string>();

        foreach (var repo in joined)
        {
            // The canonical line rides the registration, because the deployment cannot ask git and
            // this machine can (D48 §6). Read per tick rather than cached: a repository's default
            // branch changes about once in its life, and the tick that follows should know.
            var defaultBranch = await WorkingTree.DefaultBranchAsync(repo.Root, ct).ConfigureAwait(false);
            await DriverHttp.PostAsync(
                _remote, $"{_remoteBase}/api/registry",
                RemoteSyncPayloads.Registration(repo, defaultBranch), ct)
                .ConfigureAwait(false);
        }

        var names = joined.Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var records = RemoteSyncPayloads.Sessions(
            await DriverHttp.GetAsync(_local, $"{_localBase}/api/sessions?includeClosed=true", ct).ConfigureAwait(false),
            names);
        if (records is { } feed)
        {
            await DriverHttp.PostAsync(_remote, $"{_remoteBase}/api/feed/sessions", feed.Json, ct).ConfigureAwait(false);
        }

        foreach (var repo in joined.Where(r => r.SharesKnowledge))
        {
            // Where this checkout stands, asked of git at the moment of feeding — the claim the
            // deployment will compare against what it holds (D48 §6). A tree git cannot answer for
            // feeds nothing: the door refuses a feed that names no commit, and that refusal arrives
            // here as a note rather than as a wall.
            var provenance = await WorkingTree.ProvenanceAsync(repo.Root, ct).ConfigureAwait(false);
            if (provenance is null)
            {
                // The door would refuse this feed, and rightly — but only THIS side knows why, because
                // only this side has the tree. Saying it here turns "the deployment refused something"
                // into "that checkout has no history to speak from", which is the actionable sentence.
                notes.Add(
                    $"`{repo.Repository}` fed no knowledge: git could not say where this checkout stands, "
                    + "and a deployment takes knowledge only from a named commit. Its records still travel.");
                continue;
            }

            var content = RemoteSyncPayloads.Entries(repo.Repository, await DriverHttp.GetAsync(
                _local, $"{_localBase}/api/entries?repository={Uri.EscapeDataString(repo.Repository)}", ct)
                .ConfigureAwait(false), provenance);

            if (await DriverHttp.PostInformableAsync(
                    _remote, $"{_remoteBase}/api/feed/entries", content.Json, ct).ConfigureAwait(false)
                is { } note)
            {
                notes.Add(note);
            }
        }

        return notes;
    }

    /// <summary>The remote's registry comes down as foreign rows only — teammates' repositories become
    /// addressable here, while everything this machine holds keeps its own registration — and then the
    /// quests touching this machine's own joined repositories.</summary>
    /// <remarks>
    /// The mirrored rows are filed in THIS sync's workspace. That is not a feed naming its own circle
    /// (which WSP1 forbids, and still does — the remote's answer carries no workspace anyone reads):
    /// it is the receiving machine's own wiring deciding, since a row arriving from this workspace's
    /// deployment belongs to this workspace by construction (D48 §2/§5).
    /// </remarks>
    private async Task MirrorDownAsync(
        IReadOnlySet<string> localNames, IReadOnlySet<string> joinedNames, CancellationToken ct)
    {
        foreach (var (_, payload) in RemoteSyncPayloads.ForeignRegistrations(
            await DriverHttp.GetAsync(_remote, $"{_remoteBase}/api/registry", ct).ConfigureAwait(false),
            localNames,
            _workspace))
        {
            await DriverHttp.PostAsync(_local, $"{_localBase}/api/registry", payload, ct).ConfigureAwait(false);
        }

        var mirror = RemoteSyncPayloads.Quests(
            await DriverHttp.GetAsync(_remote, $"{_remoteBase}/api/quests?includeClosed=true", ct).ConfigureAwait(false),
            joinedNames);
        if (mirror is { } pull)
        {
            await DriverHttp.PostAsync(_local, $"{_localBase}/api/feed/quests", pull.Json, ct).ConfigureAwait(false);
        }
    }
}
