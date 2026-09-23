using System.Text;

namespace Daoris.Knowledge;

/// <summary>
/// A workspace's remote as this machine's host speaks to it (D68 §3, D69): quest operations fetched
/// since a number and pushed on one, session records — this machine's own fed up, the team's fetched
/// since a number (SYNC4) — and the team's code maps, brought down when they move (MAP3e).
/// </summary>
public interface IRemote
{
    /// <summary>What the remote accepted after <paramref name="since"/>, one page at a time.</summary>
    /// <exception cref="RemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default);

    /// <summary>A push of pending operations rebased on <paramref name="base"/>, judged quest by quest.</summary>
    /// <exception cref="RemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default);

    /// <summary>
    /// This machine's own records, fed up — keyed at the remote by the key that carried them, and
    /// taken whole or refused whole (D47 §6).
    /// </summary>
    /// <exception cref="RemoteException">The remote could not be reached, or refused the feed.</exception>
    Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default);

    /// <summary>
    /// The team's records the remote holds after <paramref name="since"/>, one page at a time —
    /// every origin but the caller's own, which the caller already has.
    /// </summary>
    /// <exception cref="RemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default);

    /// <summary>
    /// The commit a repository's code map is held at there (MAP3e), or null where it holds none — asked
    /// before the map itself, so a map that has not moved is not sent again.
    /// </summary>
    /// <exception cref="RemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default);

    /// <summary>A repository's code map as the remote holds it, with where it came from; null when it holds none.</summary>
    /// <exception cref="RemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default);
}

/// <param name="Records">The team's records, each keyed `origin/id` and carrying its origin.</param>
/// <param name="Through">The last revision the page covers — where the fetching machine's cursor moves.</param>
/// <param name="More">Whether another page follows.</param>
public sealed record SessionFetch(IReadOnlyList<Session> Records, long Through, bool More);

/// <summary>The remote could not be reached, or refused the conversation outright — named, never swallowed.</summary>
public sealed class RemoteException(string message) : Exception(message);

/// <summary>Which remote serves a workspace, on this machine — or none, which is the silent default (D21).</summary>
public interface IRemotes
{
    IRemote? For(string workspace);
}

/// <summary>
/// The machine's remotes map, read when asked (D48 §5): a circle wired a moment ago is wired now, and a
/// removed one is gone now — the lesson SYNC0d taught the driver, kept by the hosts.
/// </summary>
public sealed class ConfiguredRemotes(Func<IReadOnlyDictionary<string, RemoteConfig>> load) : IRemotes
{
    public ConfiguredRemotes() : this(RemoteConfig.Load) { }

    public IRemote? For(string workspace) =>
        load().TryGetValue(Workspaces.Normalize(workspace), out var config) ? new HttpRemote(config) : null;
}

/// <summary>
/// The one HTTP client of a remote's doors. The service opens this socket and nothing else: it still
/// spawns nothing (D46 §7), and the knowledge feed, which needs git, stays in the driver (D69).
/// </summary>
public sealed class HttpRemote(RemoteConfig config) : IRemote
{
    // One client for the process: a take waits on it, so a remote that hangs costs a bounded wait,
    // after which the take stands on this machine, unconfirmed (D68 §4).
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default) =>
        QuestWire.ReadPage(await SendAsync(HttpMethod.Get, $"/api/quests/operations?since={since}", null, ct).ConfigureAwait(false))
        ?? throw new RemoteException("the remote answered a fetch with something that is not a page of operations");

    public async Task<QuestPush> PushQuestsAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default) =>
        QuestWire.ReadPushed(await SendAsync(HttpMethod.Post, "/api/quests/operations", QuestWire.Push(@base, operations), ct)
            .ConfigureAwait(false))
        ?? throw new RemoteException("the remote answered a push with something that is not a push's answer");

    public Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/api/feed/sessions", SessionWire.Feed(records), ct);

    public async Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default) =>
        SessionWire.ReadPage(await SendAsync(HttpMethod.Get, $"/api/sessions/since?since={since}", null, ct).ConfigureAwait(false))
        ?? throw new RemoteException("the remote answered a fetch of session records with something that is not a page of them");

    public async Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default)
    {
        var held = await SendAsync(
            HttpMethod.Get, $"/api/feed/held?repository={Uri.EscapeDataString(repository)}", null, ct).ConfigureAwait(false);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(held);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                   && document.RootElement.TryGetProperty("codeMap", out var commit)
                   && commit.ValueKind == System.Text.Json.JsonValueKind.String
                ? commit.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            throw new RemoteException("the remote answered what it holds with something that is not an answer");
        }
    }

    public async Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default) =>
        CodeMapWire.Read(await SendAsync(
            HttpMethod.Get, $"/api/code-map/{Uri.EscapeDataString(repository)}", null, ct).ConfigureAwait(false));

    private async Task<string> SendAsync(HttpMethod method, string path, string? json, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, config.Url + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + config.Key);
            if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) return payload;

            // The remote's own sentence — an expired key names itself — and never the key we sent.
            throw new RemoteException($"the remote answered {(int)response.StatusCode}: {ErrorOf(payload)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Cancellation belongs to the caller, never converted into "unreachable".
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            throw new RemoteException($"the remote could not be reached ({error.Message})");
        }
    }

    private static string ErrorOf(string payload)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                   && document.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == System.Text.Json.JsonValueKind.String
                ? error.GetString() ?? ""
                : Clip(payload);
        }
        catch (System.Text.Json.JsonException)
        {
            return Clip(payload);
        }
    }

    private static string Clip(string text) => text.Length <= 200 ? text : text[..200] + "…";
}
