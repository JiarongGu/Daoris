using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The one-session-per-tree lock holds between hosts, not only within one (REV3 service F11). Every
/// host on a machine opens the same file — the desktop's, and a connector per session — and the lock
/// was a read followed by a write, so two opens at once could both read "free" and both write.
/// </summary>
public sealed class SessionLockAcrossHostsTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    private readonly string _file = Path.Combine(
        Path.GetTempPath(), "daoris-lock-" + Guid.NewGuid().ToString("N")[..8] + ".db");

    private SqliteConnection _mine = null!;
    private SqliteConnection _theirs = null!;

    public async Task InitializeAsync()
    {
        _mine = new SqliteConnection($"Data Source={_file}");
        await _mine.OpenAsync();
        _theirs = new SqliteConnection($"Data Source={_file}");
        await _theirs.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _mine.DisposeAsync();
        await _theirs.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    [Fact]
    public async Task An_open_waits_for_another_host_s_open_and_then_finds_the_tree_taken()
    {
        var quests = await QuestStore.OpenAsync(_mine);
        var mine = await SessionStore.OpenAsync(_mine);
        var theirs = await SessionStore.OpenAsync(_theirs);
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await service.RegisterAsync(
            new Registration("Owner", Adopted: true, "A repo.", [], [], [], 0, Root: "/trees/owner"), Now);
        var ledger = new SessionLedger(quests, mine, service);

        // Another host is mid-open: it holds the write lock and has written its session, uncommitted.
        await using var held = _theirs.BeginTransaction(deferred: false);
        await theirs.CreateAsync(null, "Owner", "stub", Now, kind: SessionKind.Chat, tree: "/trees/owner");

        var opening = Task.Run(() => ledger.OpenChatAsync("Owner", "stub", Now));
        await Task.Delay(300);
        await held.CommitAsync();

        var outcome = await opening;
        Assert.Equal(SessionOpenRefusal.RepositoryBusy, outcome.Refusal);
        Assert.Single(await mine.ListAsync("Owner"));
    }
}
