namespace Daoris.Driver;

/// <param name="Problem">
/// Null on a clean pass. Records sync eventually (D47 §2), so a problem here is reported and retried
/// next tick rather than failing the tick — but it is REPORTED, because a feed dying quietly (an
/// expired key, an unjoined repository) looks exactly like a family with nothing to say. What a clean
/// pass moved is deliberately not carried: a healthy sync moves something almost every tick, so
/// per-tick counts would be noise on every surface that renders events — they return with a surface
/// that reads them.
/// </param>
public sealed record SyncReport(string? Problem)
{
    public static readonly SyncReport Clean = new((string?)null);
}

/// <summary>
/// One pass of the local↔remote sync (D47 §9): feed the joined registrations, this machine's session
/// records, and each sharing repository's content UP; pull the remote's registry (foreign rows only)
/// and the quests touching this family DOWN into the local mirror. Registrations go first, so the
/// remote knows who is joined before their records arrive. Runs on the driver's own tick — a server
/// machine running `daoris-driver` with a key is just another machine, not a special deployment.
/// </summary>
public sealed class RemoteSync : IDisposable
{
    private readonly HttpClient _local;
    private readonly HttpClient _remote;
    private readonly string _localBase;
    private readonly string _remoteBase;

    /// <param name="handler">The test seam: a stub transport for both hosts, distinguished by URL.
    /// Production callers pass none. `ServiceClient` has the same seam for the same reason.</param>
    public RemoteSync(string localUrl, string? localKey, RemoteTarget target, HttpMessageHandler? handler = null)
    {
        _localBase = localUrl.TrimEnd('/');
        _remoteBase = target.Url;
        _local = DriverHttp.Client(localKey, handler);
        _remote = DriverHttp.Client(target.Key, handler);
    }

    /// <summary>The machine's sync, when the machine has a remote — null otherwise, silently (D21).
    /// The local key arrives from the caller, which has already read it for its own client — a second
    /// ambient environment read here would be a hidden input the caller cannot see or test.</summary>
    public static RemoteSync? FromEnvironment(string localUrl, string? localKey) =>
        RemoteTarget.Load() is { } target ? new RemoteSync(localUrl, localKey, target) : null;

    public void Dispose()
    {
        _local.Dispose();
        _remote.Dispose();
    }

    public async Task<SyncReport> RunOnceAsync(CancellationToken ct = default)
    {
        try
        {
            var registryJson = await DriverHttp.GetAsync(_local, $"{_localBase}/api/registry", ct).ConfigureAwait(false);
            var joined = RemoteSyncPayloads.Joined(registryJson);
            if (joined.Count == 0) return SyncReport.Clean;

            await FeedUpAsync(joined, ct).ConfigureAwait(false);
            await MirrorDownAsync(
                RemoteSyncPayloads.Names(registryJson),
                joined.Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase),
                ct).ConfigureAwait(false);

            return SyncReport.Clean;
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
    private async Task FeedUpAsync(
        IReadOnlyList<RemoteSyncPayloads.JoinedRepository> joined, CancellationToken ct)
    {
        foreach (var repo in joined)
        {
            await DriverHttp.PostAsync(
                _remote, $"{_remoteBase}/api/registry", RemoteSyncPayloads.Registration(repo), ct)
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
            var content = RemoteSyncPayloads.Entries(repo.Repository, await DriverHttp.GetAsync(
                _local, $"{_localBase}/api/entries?repository={Uri.EscapeDataString(repo.Repository)}", ct)
                .ConfigureAwait(false));
            await DriverHttp.PostAsync(_remote, $"{_remoteBase}/api/feed/entries", content.Json, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The remote's registry comes down as foreign rows only — teammates' repositories become
    /// addressable here, while everything this machine holds keeps its own registration — and then the
    /// quests touching this machine's own joined repositories.</summary>
    private async Task MirrorDownAsync(
        IReadOnlySet<string> localNames, IReadOnlySet<string> joinedNames, CancellationToken ct)
    {
        foreach (var (_, payload) in RemoteSyncPayloads.ForeignRegistrations(
            await DriverHttp.GetAsync(_remote, $"{_remoteBase}/api/registry", ct).ConfigureAwait(false),
            localNames))
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
