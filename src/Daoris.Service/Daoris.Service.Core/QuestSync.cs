using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace Daoris.Knowledge;

/// <summary>A workspace's remote as the quest sync speaks to it (D68 §3): fetch since a number, push on one.</summary>
public interface IQuestRemote
{
    /// <summary>What the remote accepted after <paramref name="since"/>, one page at a time.</summary>
    /// <exception cref="QuestRemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<QuestFetch> FetchAsync(long since, CancellationToken ct = default);

    /// <summary>A push of pending operations rebased on <paramref name="base"/>, judged quest by quest.</summary>
    /// <exception cref="QuestRemoteException">The remote could not be reached, or answered with a wall.</exception>
    Task<QuestPush> PushAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default);
}

/// <summary>The remote could not be reached, or refused the conversation outright — named, never swallowed.</summary>
public sealed class QuestRemoteException(string message) : Exception(message);

/// <summary>Which remote serves a workspace, on this machine — or none, which is the silent default (D21).</summary>
public interface IQuestRemotes
{
    IQuestRemote? For(string workspace);
}

/// <summary>
/// The machine's remotes map, read when asked (D48 §5): a circle wired a moment ago is wired now, and a
/// removed one is gone now — the lesson SYNC0d taught the driver, kept by the hosts.
/// </summary>
public sealed class ConfiguredQuestRemotes(Func<IReadOnlyDictionary<string, RemoteConfig>> load) : IQuestRemotes
{
    public ConfiguredQuestRemotes() : this(RemoteConfig.Load) { }

    public IQuestRemote? For(string workspace) =>
        load().TryGetValue(Workspaces.Normalize(workspace), out var config) ? new HttpQuestRemote(config) : null;
}

/// <summary>
/// The one HTTP client of a remote's quest doors. The service opens this socket and nothing else: it
/// still spawns nothing (D46 §7), and the knowledge feed, which needs git, stays in the driver (D69).
/// </summary>
public sealed class HttpQuestRemote(RemoteConfig config) : IQuestRemote
{
    // One client for the process: a take waits on it, so a remote that hangs costs a bounded wait,
    // after which the take stands on this machine, unconfirmed (D68 §4).
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<QuestFetch> FetchAsync(long since, CancellationToken ct = default) =>
        QuestWire.ReadPage(await SendAsync(HttpMethod.Get, $"/api/quests/operations?since={since}", null, ct).ConfigureAwait(false))
        ?? throw new QuestRemoteException("the remote answered a fetch with something that is not a page of operations");

    public async Task<QuestPush> PushAsync(long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default) =>
        QuestWire.ReadPushed(await SendAsync(HttpMethod.Post, "/api/quests/operations", QuestWire.Push(@base, operations), ct)
            .ConfigureAwait(false))
        ?? throw new QuestRemoteException("the remote answered a push with something that is not a push's answer");

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
            throw new QuestRemoteException($"the remote answered {(int)response.StatusCode}: {ErrorOf(payload)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Cancellation belongs to the caller, never converted into "unreachable".
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            throw new QuestRemoteException($"the remote could not be reached ({error.Message})");
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

/// <param name="Pushed">How many operations the remote numbered in this pass.</param>
/// <param name="Conflicts">This machine's moves the rebase turned into conflicts.</param>
/// <param name="Refused">Quests the remote would not keep, each in its own words.</param>
/// <param name="Behind">Quests still moving at the remote after every round — the next pass goes round again.</param>
/// <param name="Problem">The wall, when the remote could not be reached or refused the conversation; null otherwise.</param>
public sealed record QuestSyncReport(
    int Pushed, IReadOnlyList<QuestOperation> Conflicts, IReadOnlyList<QuestPushRefusal> Refused,
    IReadOnlyList<string> Behind, string? Problem);

/// <summary>
/// One pass of fetch, rebase, push for one workspace's quests (D68 §3, sync design §8) — the ONE
/// implementation, run by a take that claims by push and by the driver's tick alike (D69).
/// </summary>
/// <remarks>
/// A store's passes over one workspace are taken one at a time: two at once would each be correct —
/// every step is idempotent — but a take waiting behind a tick's pass is cheaper than the same
/// operations pushed twice.
/// </remarks>
public static class QuestSync
{
    /// <summary>How many times one pass goes round when the remote says a quest moved first.</summary>
    public const int Rounds = 3;

    private static readonly ConditionalWeakTable<QuestStore, ConcurrentDictionary<string, SemaphoreSlim>> Passes = new();

    /// <summary>A pass, pushing what the receiver's registration says may leave (sync design §8).</summary>
    public static async Task<QuestSyncReport> RunAsync(
        QuestStore store, KnowledgeService service, IQuestRemote remote, string workspace, CancellationToken ct = default)
    {
        // What may leave follows the receiver: joined in this circle, whether its checkout is here or a
        // teammate's row came down without one. Silence means local.
        var circle = Workspaces.Normalize(workspace);
        var shared = (await service.RegistryAsync(ct: ct).ConfigureAwait(false))
            .Where(r => r.Joined && Workspaces.Same(r.InWorkspace, circle))
            .Select(r => r.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return await RunAsync(store, shared.Contains, remote, circle, ct).ConfigureAwait(false);
    }

    /// <summary>A pass, pushing the pending operations of quests whose receiver <paramref name="shared"/> admits.</summary>
    public static async Task<QuestSyncReport> RunAsync(
        QuestStore store, Func<string, bool> shared, IQuestRemote remote, string workspace, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        var pass = Passes.GetValue(store, _ => new(StringComparer.OrdinalIgnoreCase))
            .GetOrAdd(circle, _ => new SemaphoreSlim(1, 1));
        await pass.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RunLockedAsync(store, shared, remote, circle, ct).ConfigureAwait(false);
        }
        finally
        {
            pass.Release();
        }
    }

    private static async Task<QuestSyncReport> RunLockedAsync(
        QuestStore store, Func<string, bool> shared, IQuestRemote remote, string circle, CancellationToken ct)
    {
        var pushed = 0;
        var conflicts = new List<QuestOperation>();
        var refused = new List<QuestPushRefusal>();
        IReadOnlyList<string> behind = [];
        try
        {
            for (var round = 1; round <= Rounds; round++)
            {
                var cursor = await store.CursorAsync(circle, ct).ConfigureAwait(false);
                var fetched = new List<QuestOperation>();
                var through = cursor;
                while (true)
                {
                    var page = await remote.FetchAsync(through, ct).ConfigureAwait(false);
                    fetched.AddRange(page.Operations);
                    through = Math.Max(through, page.Through);
                    if (!page.More) break;
                }

                var integrated = await store.IntegrateAsync(circle, fetched, through, ct).ConfigureAwait(false);
                conflicts.AddRange(integrated.Conflicts);

                var pending = await store.PendingAsync(circle, shared, ct).ConfigureAwait(false);
                if (pending.Count == 0) return new(pushed, conflicts, refused, [], null);

                var push = await remote.PushAsync(integrated.Cursor, pending, ct).ConfigureAwait(false);
                await store.AcceptedAsync(push.Accepted, ct).ConfigureAwait(false);
                pushed += push.Accepted.Count(a => pending.Any(p => p.Machine == a.Machine && p.Sequence == a.Sequence));
                refused.AddRange(push.Refused);
                behind = push.Behind;
                if (behind.Count == 0) return new(pushed, conflicts, refused, [], null);
            }

            return new(pushed, conflicts, refused, behind, null);
        }
        catch (QuestRemoteException wall)
        {
            return new(pushed, conflicts, refused, behind, wall.Message);
        }
    }
}
