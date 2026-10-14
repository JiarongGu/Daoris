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

/// <summary>Where a circle stands on this machine (SYNC6a) — what the status bar and `daoris-driver sync status` read.</summary>
/// <param name="Ahead">Operations this machine made that the circle's remote has not numbered yet.</param>
/// <param name="Behind">
/// Quests the last pass could not bring level, because the remote moved them on every round. A pass
/// fetches and rebases in one step, so nothing is ever fetched and left unapplied: behind is what the
/// last pass could not finish, and <paramref name="Synced"/> says how old that knowledge is.
/// </param>
/// <param name="Conflicts">Quests in the circle carrying a conflict, newest first.</param>
/// <param name="Synced">When a pass last reached the remote; null before the first one did.</param>
/// <param name="Tried">When a pass last ran, reaching the remote or not; null before the first.</param>
/// <param name="Problem">The wall the last pass hit; null when it reached the remote.</param>
public sealed record QuestStanding(
    int Ahead, IReadOnlyList<string> Behind, IReadOnlyList<string> Conflicts,
    DateTimeOffset? Synced, DateTimeOffset? Tried, string? Problem);

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
        var circle = Workspaces.Normalize(workspace);
        return await RunAsync(store, await SharedAsync(service, circle, ct).ConfigureAwait(false), remote, circle, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Where a circle stands (SYNC6a), with what may leave read the way a pass reads it.</summary>
    public static async Task<QuestStanding> StandingAsync(
        QuestStore store, KnowledgeService service, string workspace, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        return await store.StandingAsync(circle, await SharedAsync(service, circle, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// What may leave follows the receiver: joined in this circle, whether its checkout is here or a
    /// teammate's row came down without one. Silence means local.
    /// </summary>
    private static async Task<Func<string, bool>> SharedAsync(KnowledgeService service, string circle, CancellationToken ct)
    {
        var shared = (await service.RegistryAsync(ct: ct).ConfigureAwait(false))
            .Where(r => r.Joined && Workspaces.Same(r.InWorkspace, circle))
            .Select(r => r.Repository)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return shared.Contains;
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
            var report = await RunLockedAsync(store, shared, remote, circle, ct).ConfigureAwait(false);

            // Recorded however it ended — a take's pass and a tick's alike — so where the circle stands
            // is read from the store rather than from whichever caller happened to see the pass.
            await store.RecordPassAsync(circle, report, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            return report;
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
