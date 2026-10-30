using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// One machine's <c>knowledge.db</c> for the memory coordinator's record (KNOWCENTRAL2c): a file of its own under the repository's
/// gitignored <c>_fixtures/</c>, with the stores a host opens over it, and a restart that closes the connection and opens them again,
/// as a killed host's next start does.
/// </summary>
internal sealed class MemoryMachine : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-31T09:00:00Z");

    public const string Tree = "/work/trees/games/Owner/s-1";

    /// <summary>The repository's registered root, its main tree.</summary>
    public const string Root = "/trees/owner";

    private readonly string _root;

    private KnowledgeService _registry = null!;

    private MemoryMachine(string root) => _root = root;

    public string Path => System.IO.Path.Combine(_root, "service.db");

    public SqliteConnection Connection { get; private set; } = null!;

    public QuestStore Quests { get; private set; } = null!;

    public SessionStore Sessions { get; private set; } = null!;

    public WorkRecordStore Records { get; private set; } = null!;

    public SessionLedger Ledger { get; private set; } = null!;

    public MemoryOpStore Memory => Sessions.Memory;

    public static async Task<MemoryMachine> OpenAsync(string name)
    {
        var machine = new MemoryMachine(System.IO.Path.Combine(SourceFiles.WorkspaceFolder(), "_fixtures", name, Guid.NewGuid().ToString("N")[..8]));
        Directory.CreateDirectory(machine._root);
        var index = new InMemoryKnowledgeStore();
        machine._registry = new KnowledgeService(
            index, new LexicalKnowledgeSearch(index), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await machine._registry.RegisterAsync(new Registration("Owner", Adopted: true, "A repo.", [], [], [], Entries: 0, Root: Root), Now);
        await machine.OpenStoresAsync();
        return machine;
    }

    /// <summary>The host killed and started again: nothing held in memory survives, the file does.</summary>
    public async Task RestartAsync()
    {
        await Connection.DisposeAsync();
        SqlitePools.Release(Path);
        await OpenStoresAsync();
    }

    /// <summary>Another process's connection to the same file, as a connector opens one.</summary>
    public async Task<SqliteConnection> SecondAsync(int timeoutSeconds = 30)
    {
        var second = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false, DefaultTimeout = timeoutSeconds,
        }.ToString());
        await second.OpenAsync();
        return second;
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        SqlitePools.Release(Path);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file the scanner holds for a moment: the next prune of _fixtures takes it.
        }
    }

    /// <summary>A chat opened through the ledger, so in the registered root, and brought to working as the driver brings one.</summary>
    public async Task<Session> ChatAsync(DateTimeOffset at, string? from = null)
    {
        var opened = await Ledger.OpenChatAsync("Owner", "stub", at, from: from, fromWhy: from is null ? null : "press", fromBy: from is null ? null : "person");
        Assert.Equal(SessionOpenRefusal.None, opened.Refusal);
        Assert.Equal(Root, opened.Session!.Tree);
        foreach (var state in new[] { "starting", "working" })
        {
            Assert.Equal(SessionAdvanceRefusal.None, (await Ledger.AdvanceAsync(opened.Session.Id, state, null, null, null, at)).Refusal);
        }

        return (await Sessions.FindAsync(opened.Session.Id))!;
    }

    /// <summary>A driven session in <see cref="Tree"/>, working.</summary>
    public async Task<Session> InTreeAsync(DateTimeOffset at)
    {
        var quest = await Quests.PublishAsync("Asker", "Owner", "Do the thing", "Here is why.", at);
        var session = await Sessions.CreateAsync(quest.Id, "Owner", "stub", at, tree: Tree);
        return (await Sessions.SetStateAsync(session.Id, SessionState.Working, null, null, null, at))!;
    }

    /// <summary>An operation <paramref name="session"/> stages at <paramref name="at"/>, as its connector does.</summary>
    public Task<MemoryOp> StageAsync(Session session, MemoryChange change, DateTimeOffset at) =>
        Memory.StageAsync(session.Repository, change, new MemoryAuthor(session.Id, session.Workspace, session.Quest, session.Adapter, "2.1.0", "builder", "r3", at));

    /// <summary>The finished tree's record of <see cref="Tree"/>, naming a landing or not.</summary>
    public static WorkRecordPost Post(bool landing, string folder = "s-1-20261031T100000000Z") =>
        new("default", "Owner", folder, Tree, Now.AddHours(1), "unit-1", "daoris/s-1", "main",
            new WorkRecordHold("squash", "landing/s-1", "branch"), "tip9", "base0", null, [], null, new WorkRecordLeft([], 0, false), Whole: false)
        {
            Landing = landing ? new WorkRecordLanding("feature/s-1", "main", Now.AddHours(1), null) : null,
            LandingWhy = landing ? null : "no landing is recorded for its work unit",
        };

    /// <summary>A fault inside every move of an operation's state, until <see cref="HealAsync"/>.</summary>
    public Task FaultAsync() => ExecuteAsync(
        "CREATE TEMP TRIGGER memory_fault BEFORE UPDATE OF state ON memory_ops BEGIN SELECT RAISE(ABORT, 'a fault inside the commit point'); END;");

    public Task HealAsync() => ExecuteAsync("DROP TRIGGER IF EXISTS temp.memory_fault;");

    public async Task ExecuteAsync(string sql)
    {
        await using var command = Connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task OpenStoresAsync()
    {
        Connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        await Connection.OpenAsync();
        Quests = await QuestStore.OpenAsync(Connection);
        Sessions = await SessionStore.OpenAsync(Connection);
        Records = await WorkRecordStore.OpenAsync(Connection);
        Ledger = new SessionLedger(Quests, Sessions, _registry);
    }
}
