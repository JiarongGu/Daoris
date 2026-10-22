using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// FABRIC-CLOSE1i (D173's CLOSE1b note, choice 4; the fabric events design §E5's cutover): the workspace's handshake through its remote.
/// Each push declares its machine knows a bound quest, the remote answers which of the machines it has heard from did not, and a quest of
/// a workspace synced through it binds to a closure graph only where every one did. Three machines' stores and the remote's, synced by the
/// real pass through the real wire.
/// </summary>
public sealed class QuestClosureHandshakeTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-handshake-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<SqliteConnection> _connections = [];
    private SqliteConnection _here = null!;
    private KnowledgeService _service = null!;
    private QuestStore _a = null!;
    private QuestStore _b = null!;
    private QuestStore _older = null!;
    private QuestStore _remote = null!;
    private QuestExchange _exchange = null!;

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T09:00:00Z");
    private const string Graph = "gph-0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";
    private const string Session = "ses-takes-its-quest";

    public async Task InitializeAsync()
    {
        Repo("reports", """{ "summary": "The reports.", "owns": ["report pages"], "accepts": ["a report"] }""");
        Repo("checker", """{ "summary": "Checks things in a browser.", "owns": ["verification"], "accepts": ["a check"] }""");
        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(), DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(Path.Combine(_root, "family"), Now);

        _here = await ConnectAsync();
        _a = await QuestStore.OpenAsync(_here);
        _b = await QuestStore.OpenAsync(await ConnectAsync());
        _older = await QuestStore.OpenAsync(await ConnectAsync());
        _remote = await QuestStore.OpenAsync(await ConnectAsync());
        _exchange = Exchange(_a);
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<SqliteConnection> ConnectAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        _connections.Add(connection);
        return connection;
    }

    private void Repo(string name, string domain)
    {
        var dir = Path.Combine(_root, "family", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), $$"""{ "source": "s", "packs": [], "domain": {{domain}} }""");
    }

    /// <summary>Machine A's exchange, its every circle wired to the remote.</summary>
    private QuestExchange Exchange(QuestStore store) =>
        new(_service, store, remotes: new OneRemote(new StoreRemote(_remote)));

    /// <summary>One machine's pass of this build: its pushes declare it.</summary>
    private Task<QuestSyncReport> SyncAsync(QuestStore machine, IRemote? through = null) =>
        QuestSync.RunAsync(machine, _ => true, through ?? new StoreRemote(_remote), Workspaces.Default);

    /// <summary>A machine of a build before FABRIC-CLOSE1i: the same pass, its pushes declaring nothing, as that build's wire writes them.</summary>
    private Task<QuestSyncReport> OlderSyncAsync(QuestStore machine) => SyncAsync(machine, new UndeclaringRemote(new StoreRemote(_remote)));

    private async Task<Quest> PublishAsync(string title) => (await _exchange.PublishAsync("checker", "reports", title, "Through the bridge.", Now)).Quest!;

    [Fact]
    public async Task Every_machine_declared_allows_the_bind_and_one_older_machine_refuses_it_naming_the_machine()
    {
        // A and B, both of this build, each push: the remote heard both declare, and A's pass says so.
        await PublishAsync("Seed A");
        await _b.PublishAsync("Asker", "reports", "Seed B", "why", Now);
        await SyncAsync(_a);
        await SyncAsync(_b);
        var heard = await SyncAsync(_a);
        Assert.True(heard.Closure!.All);
        Assert.True(_a.ClosureOf(Workspaces.Default)!.All);

        var allowed = await PublishAsync("Bound through the remote");
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(allowed.Id, Graph, Now, atTake: true, session: Session)).Refusal);
        var take = await _exchange.RespondAsync(allowed.Id, "take", null, Now.AddMinutes(1), session: Session);
        Assert.Equal(Graph, take.Quest!.Closure);
        Assert.Equal([QuestOperationKind.Taken, QuestOperationKind.Bound], (await _a.HistoryAsync(allowed.Id)).Select(op => op.Kind).TakeLast(2));

        // A graph named while every machine had declared, then an older machine pushes: the take, judged in its own write, binds nothing.
        var named = await PublishAsync("Named before the older machine");
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(named.Id, Graph, Now, atTake: true, session: Session)).Refusal);
        await _older.PublishAsync("Asker", "reports", "From an older build", "why", Now);
        await OlderSyncAsync(_older);
        Assert.Equal([_older.Machine], (await SyncAsync(_a)).Closure!.Undeclared);

        var unbound = await _exchange.RespondAsync(named.Id, "take", null, Now.AddMinutes(1), session: Session);
        Assert.Equal(QuestTakeBind.Remote, unbound.TakeBind);
        Assert.Null(unbound.Quest!.Closure);
        Assert.Contains("not every machine", unbound.Message, StringComparison.Ordinal);

        // A bind asked now is refused, naming the machine that did not declare.
        var refused = await _exchange.BindAsync((await PublishAsync("After the older machine")).Id, Graph, Now, atTake: true, session: Session);
        Assert.Equal(QuestRespondRefusal.NotInGraph, refused.Refusal);
        Assert.Contains($"machine `{_older.Machine}`", refused.Message, StringComparison.Ordinal);
        Assert.Contains("has not declared it knows a bound quest", refused.Message, StringComparison.Ordinal);
        Assert.Contains("closes by its done", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_machine_brought_up_to_this_build_declares_by_its_next_pass_with_nothing_to_push()
    {
        await PublishAsync("Seed A");
        await _older.PublishAsync("Asker", "reports", "From an older build", "why", Now);
        await SyncAsync(_a);
        await OlderSyncAsync(_older);
        Assert.False((await SyncAsync(_a)).Closure!.All);

        // Updated, with nothing pending: its pass hears itself undeclared and pushes nothing but its declaration, once.
        var through = new StoreRemote(_remote);
        var upgraded = await SyncAsync(_older, through);
        Assert.True(upgraded.Closure!.All);
        Assert.Contains(through.Pushes, push => push.Contains("\"operations\":[]", StringComparison.Ordinal));
        var again = new StoreRemote(_remote);
        await SyncAsync(_older, again);
        Assert.Empty(again.Pushes);

        Assert.True((await SyncAsync(_a)).Closure!.All);
        var quest = await PublishAsync("Bound once every machine declared");
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(quest.Id, Graph, Now, atTake: true, session: Session)).Refusal);
    }

    [Fact]
    public async Task A_remote_before_the_handshake_or_a_wall_binds_nothing_and_a_restart_keeps_what_the_last_pass_heard()
    {
        await PublishAsync("Seed A");
        Assert.True((await SyncAsync(_a)).Closure!.All);

        // A restart keeps it: the store reads it back from the pass it recorded.
        _a = await QuestStore.OpenAsync(_here);
        _exchange = Exchange(_a);
        Assert.True(_a.ClosureOf(Workspaces.Default)!.All);

        // A remote before FABRIC-CLOSE1i answers no handshake: the pass hears none, and no quest binds.
        Assert.Null((await SyncAsync(_a, new OlderRemote(_remote))).Closure);
        var refused = await _exchange.BindAsync((await PublishAsync("Through an older remote")).Id, Graph, Now, atTake: true, session: Session);
        Assert.Equal(QuestRespondRefusal.NotInGraph, refused.Refusal);
        Assert.Contains("has not said every machine", refused.Message, StringComparison.Ordinal);

        // A wall says nothing of the machines either.
        Assert.True((await SyncAsync(_a)).Closure!.All);
        Assert.Null((await SyncAsync(_a, new UnreachableRemote())).Closure);
        Assert.Null(_a.ClosureOf(Workspaces.Default));
    }

    [Fact]
    public void The_handshake_crosses_the_wire_only_where_said_and_a_half_made_one_reads_as_none()
    {
        // A push declares its machine only where it is named; one without crosses exactly as before.
        Assert.Equal("m1", QuestWire.Declares(QuestWire.Push(3, [], declares: "m1")));
        Assert.DoesNotContain("closure", QuestWire.Push(3, []), StringComparison.Ordinal);
        Assert.Null(QuestWire.Declares(QuestWire.Push(3, [])));
        Assert.Null(QuestWire.Declares("""{ "base": 3, "machine": "m1", "closure": "yes", "operations": [] }"""));
        Assert.NotNull(QuestWire.ReadPush(QuestWire.Push(3, [], declares: "m1")));

        var page = QuestWire.ReadPage(QuestWire.Page(new QuestFetch([], 4, More: false) { Closure = new(["m2"]) }))!;
        Assert.Equal(["m2"], page.Closure!.Undeclared);
        Assert.True(QuestWire.ReadPushed(QuestWire.Pushed(new QuestPush([], [], []) { Closure = new([]) }))!.Closure!.All);
        Assert.DoesNotContain("closure", QuestWire.Page(new QuestFetch([], 4, More: false)), StringComparison.Ordinal);

        Assert.Null(QuestWire.ReadPushed("""{ "accepted": [], "behind": [], "refused": [], "closure": { "all": true, "undeclared": ["m2"] } }""")!.Closure);
        Assert.Null(QuestWire.ReadPushed("""{ "accepted": [], "behind": [], "refused": [], "closure": { "all": true } }""")!.Closure);
        Assert.Null(QuestWire.ReadPushed("""{ "accepted": [], "behind": [], "refused": [], "closure": { "all": false, "undeclared": [7] } }""")!.Closure);
    }

    /// <summary>
    /// FABRIC-CLOSE1i2: a machine the remote still counts, retired by the person through their own machine. The bind it held off binds at
    /// once, before any pass; a pass hears the remote say every machine declared; and a push from the retired machine counts it again.
    /// </summary>
    [Fact]
    public async Task A_retired_machine_no_longer_refuses_a_bind_and_one_that_pushes_again_is_counted_again()
    {
        await PublishAsync("Seed A");
        await _older.PublishAsync("Asker", "reports", "From an older build", "why", Now);
        await SyncAsync(_a);
        await OlderSyncAsync(_older);
        Assert.Equal([_older.Machine], (await SyncAsync(_a)).Closure!.Undeclared);
        var held = await PublishAsync("Held off by the older machine");
        Assert.Equal(QuestRespondRefusal.NotInGraph, (await _exchange.BindAsync(held.Id, Graph, Now, atTake: true, session: Session)).Refusal);

        // Its store deleted: the person retires it at the remote, and what the remote answers is kept here at once.
        var retired = await QuestSync.RetireMachineAsync(_a, new StoreRemote(_remote), Workspaces.Default, _older.Machine);
        Assert.True(retired.Retired);
        Assert.True(retired.Closure.All);
        Assert.True(_a.ClosureOf(Workspaces.Default)!.All);
        Assert.Equal(QuestRespondRefusal.None, (await _exchange.BindAsync(held.Id, Graph, Now, atTake: true, session: Session)).Refusal);
        Assert.True((await SyncAsync(_a)).Closure!.All);

        // The machine pushes again, still of the older build: counted again, undeclared, and the next bind refused naming it.
        await _older.PublishAsync("Asker", "reports", "Back again", "why", Now);
        await OlderSyncAsync(_older);
        Assert.Equal([_older.Machine], (await SyncAsync(_a)).Closure!.Undeclared);
        var refused = await _exchange.BindAsync((await PublishAsync("After it came back")).Id, Graph, Now, atTake: true, session: Session);
        Assert.Equal(QuestRespondRefusal.NotInGraph, refused.Refusal);
        Assert.Contains($"machine `{_older.Machine}`", refused.Message, StringComparison.Ordinal);

        // Retired again, then pushing from this build: counted again as declared, so the binds go on.
        await QuestSync.RetireMachineAsync(_a, new StoreRemote(_remote), Workspaces.Default, _older.Machine);
        await SyncAsync(_older);
        Assert.True((await SyncAsync(_a)).Closure!.All);
    }

    [Fact]
    public async Task A_machine_the_remote_never_heard_is_not_retired_and_a_remote_before_the_door_retires_nothing()
    {
        await PublishAsync("Seed A");
        await _older.PublishAsync("Asker", "reports", "From an older build", "why", Now);
        await SyncAsync(_a);
        await OlderSyncAsync(_older);
        Assert.False((await SyncAsync(_a)).Closure!.All);

        // A machine it never heard from, and the remote itself: nothing kept, the handshake answered as it stands.
        foreach (var unknown in new[] { "never-pushed", _remote.Machine })
        {
            var answered = await QuestSync.RetireMachineAsync(_a, new StoreRemote(_remote), Workspaces.Default, unknown);
            Assert.False(answered.Retired);
            Assert.Equal([_older.Machine], answered.Closure.Undeclared);
        }

        // A remote before the door answers 404, and what this machine last heard stays.
        var wall = await Assert.ThrowsAsync<RemoteException>(() =>
            QuestSync.RetireMachineAsync(_a, new OlderRemote(_remote), Workspaces.Default, _older.Machine));
        Assert.Equal(404, wall.Status);
        Assert.Equal([_older.Machine], _a.ClosureOf(Workspaces.Default)!.Undeclared);
        Assert.Equal([_older.Machine], (await _remote.ClosureAsync()).Undeclared);

        // The answer's wire: whole, or none.
        var read = QuestWire.ReadRetired(QuestWire.Retired(true, new(["m2"])))!;
        Assert.True(read.Retired);
        Assert.Equal(["m2"], read.Closure.Undeclared);
        Assert.False(QuestWire.ReadRetired(QuestWire.Retired(false, new([])))!.Retired);
        Assert.Null(QuestWire.ReadRetired("""{ "retired": true }"""));
        Assert.Null(QuestWire.ReadRetired("""{ "retired": true, "closure": { "all": true, "undeclared": ["m2"] } }"""));
    }

    /// <summary>The wire of a build before FABRIC-CLOSE1i: its pushes name no machine and declare nothing.</summary>
    private sealed class UndeclaringRemote(IRemote remote) : IRemote
    {
        public Task<QuestFetch> FetchQuestsAsync(long since, CancellationToken ct = default) => remote.FetchQuestsAsync(since, ct);

        public Task<QuestPush> PushQuestsAsync(
            long @base, IReadOnlyList<QuestOperation> operations, CancellationToken ct = default, string? declares = null) =>
            remote.PushQuestsAsync(@base, operations, ct, declares: null);

        public Task PushSessionsAsync(IReadOnlyList<FedSessionRecord> records, CancellationToken ct = default) => throw QuestsOnly();

        public Task<SessionFetch> FetchSessionsAsync(long since, CancellationToken ct = default) => throw QuestsOnly();

        public Task PushChannelAsync(IReadOnlyList<ChannelMessage> messages, CancellationToken ct = default) => throw QuestsOnly();

        public Task<ChannelFetch> FetchChannelAsync(long since, CancellationToken ct = default) => throw QuestsOnly();

        public Task<string?> HeldCodeMapAsync(string repository, CancellationToken ct = default) => throw QuestsOnly();

        public Task<FedCodeMap?> FetchCodeMapAsync(string repository, CancellationToken ct = default) => throw QuestsOnly();

        private static NotSupportedException QuestsOnly() => new("an undeclaring remote here speaks quests only");
    }
}
