using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Daoris.Knowledge;

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
        QuestStore store, KnowledgeService service, IRemote remote, string workspace, CancellationToken ct = default)
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
        QuestStore store, Func<string, bool> shared, IRemote remote, string workspace, CancellationToken ct = default)
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
        QuestStore store, Func<string, bool> shared, IRemote remote, string circle, CancellationToken ct)
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
                    var page = await remote.FetchQuestsAsync(through, ct).ConfigureAwait(false);
                    fetched.AddRange(page.Operations);
                    through = Math.Max(through, page.Through);
                    if (!page.More) break;
                }

                var integrated = await store.IntegrateAsync(circle, fetched, through, ct).ConfigureAwait(false);
                conflicts.AddRange(integrated.Conflicts);

                var pending = await store.PendingAsync(circle, shared, ct).ConfigureAwait(false);
                if (pending.Count == 0) return new(pushed, conflicts, refused, [], null);

                var push = await remote.PushQuestsAsync(integrated.Cursor, pending, ct).ConfigureAwait(false);
                await store.AcceptedAsync(push.Accepted, ct).ConfigureAwait(false);
                pushed += push.Accepted.Count(a => pending.Any(p => p.Machine == a.Machine && p.Sequence == a.Sequence));
                refused.AddRange(push.Refused);
                behind = push.Behind;
                if (behind.Count == 0) return new(pushed, conflicts, refused, [], null);
            }

            return new(pushed, conflicts, refused, behind, null);
        }
        catch (RemoteException wall)
        {
            return new(pushed, conflicts, refused, behind, wall.Message);
        }
    }
}
