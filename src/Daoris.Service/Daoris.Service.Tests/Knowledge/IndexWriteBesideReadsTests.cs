using System.Diagnostics;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The index's re-read of a moved checkout holds the write lock of the index, and a read of the registry, the sessions or the
/// quests answers beside it: the ledger's stores never queue behind the index's connection.
/// </summary>
/// <remarks>
/// Every store once shared one connection and so one gate, which the re-read's write transaction held for its whole length; on the
/// install a merge in a watched checkout kept the registry, sessions and quests waiting for tens of seconds.
/// </remarks>
public sealed class IndexWriteBesideReadsTests : IDisposable
{
    /// <summary>What an interaction read answers within while the index writes: far under a second.</summary>
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "index-beside-" + Guid.NewGuid().ToString("N")[..8]);

    public IndexWriteBesideReadsTests() => Directory.CreateDirectory(_root);

    private string Database => Path.Combine(_root, "knowledge.db");

    public void Dispose()
    {
        SqlitePools.Release(Database);
        for (var attempt = 0; attempt < 20 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task Registry_sessions_and_quests_answer_while_the_index_holds_a_write()
    {
        await using var store = await SqliteKnowledgeStore.OpenAsync(Database);
        var registrations = await RegistrationStore.OpenAsync(store.Connection);
        var sessions = await SessionStore.OpenAsync(store.Connection);
        var quests = await QuestStore.OpenAsync(store.Connection);

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entries = Enumerable.Range(0, 50)
            .Select(i => new KnowledgeEntry("repo", EntryKind.Knowledge, Provenance.Local, $"t{i}", $"body {i}", $"f{i}.md"))
            .ToList();
        var write = store.ReplaceRepositoryAsync("repo", entries, ReadingWrite.Taken(DateTimeOffset.UtcNow), async _ =>
        {
            held.SetResult();
            await release.Task;
        });
        await held.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            var timer = Stopwatch.StartNew();
            await registrations.AllAsync().WaitAsync(Budget);
            var registry = timer.Elapsed;
            await sessions.ListAsync().WaitAsync(Budget);
            await quests.ListAsync().WaitAsync(Budget);
            Assert.True(timer.Elapsed < Budget, $"registry {registry}, all three {timer.Elapsed}");
        }
        finally
        {
            release.SetResult();
            await write;
        }
    }
}
