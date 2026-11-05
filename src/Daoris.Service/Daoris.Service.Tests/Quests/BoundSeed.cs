using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// A quest bound to a closure graph, for a test that needs one: a <see cref="QuestOperationKind.Bound"/> operation written as a sync
/// from an older build would bring it, since no door of this build binds. Judged by the replay's rule, so it moves only a taken quest
/// this machine holds and before its first submission. Shared by the service's test projects.
/// </summary>
internal static class BoundSeed
{
    public static Task<QuestMove> SeedBoundAsync(this QuestStore store, string id, string graph, DateTimeOffset now) =>
        store.WriteAsync(tx => tx.Of<QuestBindOperation>().TakersMoveAsync(
            tx, id.TrimStart('#'), new QuestOperation(id.TrimStart('#'), QuestOperationKind.Bound, tx.Machine, 0, now, graph), now));
}
