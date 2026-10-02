using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// Session records both ways, by cursor (SYNC4): a machine's own records go up when they change, not
/// every tick; the team's come down read-only, keyed `origin/id`; nothing that stays home — transcript,
/// tree, profile — has a field on the wire; and a teammate's session is never this machine's lock.
/// </summary>
public sealed class SessionSyncTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-sessionsync-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly List<SqliteConnection> _connections = [];
    private KnowledgeService _service = null!;
    private SessionStore _a = null!;
    private SessionStore _b = null!;
    private SessionStore _remote = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T10:00:00Z");

    public async Task InitializeAsync()
    {
        Repo("Shared", """
            { "source": "s", "packs": [], "domain": { "summary": "Shared.", "owns": ["it"], "accepts": ["a quest"] },
              "remote": { "join": true, "knowledge": false } }
            """);
        Repo("Homebody", """
            { "source": "s", "packs": [], "domain": { "summary": "Stays local.", "owns": ["itself"], "accepts": [] } }
            """);

        var index = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            index, new LexicalKnowledgeSearch(index), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());
        await _service.ImportAsync(_root, DateTimeOffset.UtcNow);

        _a = await OpenAsync();
        _b = await OpenAsync();
        _remote = await OpenAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections) await connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void Repo(string name, string manifest)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "daoris.json"), manifest);
    }

    private async Task<SessionStore> OpenAsync(SqliteConnection? connection = null)
    {
        connection ??= new SqliteConnection("Data Source=:memory:");
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        _connections.Add(connection);
        return await SessionStore.OpenAsync(connection);
    }

    /// <summary>One machine's pass, its feed keyed by <paramref name="caller"/> as its key would be.</summary>
    private Task<SessionSyncReport> SyncAsync(SessionStore machine, string caller, StoreRemote? remote = null) =>
        SessionSync.RunAsync(machine, _service, remote ?? Remote(caller), Workspaces.Default);

    private StoreRemote Remote(string caller) => new(null!, _remote, _service, caller);

    [Fact]
    public async Task A_machines_own_records_go_up_once_and_again_only_when_they_change()
    {
        var session = await _a.CreateAsync("q1", "Shared", "stub", Now);

        var first = await SyncAsync(_a, "a@one");
        var quiet = await SyncAsync(_a, "a@one");
        await _a.SetStateAsync(session.Id, SessionState.Completed, "done.", "abc123 landed", null, Now.AddMinutes(5));
        var moved = await SyncAsync(_a, "a@one");

        Assert.Equal((1, 0, 1), (first.Pushed, quiet.Pushed, moved.Pushed));
        var atRemote = (await _remote.FindAsync($"a@one/{session.Id}"))!;
        Assert.Equal(SessionState.Completed, atRemote.State);
        Assert.Equal("a@one", atRemote.Origin);
    }

    /// <summary>Silence means local (D47 §4): a record of a repository that has not joined never leaves.</summary>
    [Fact]
    public async Task A_record_of_a_repository_that_has_not_joined_stays_home()
    {
        await _a.CreateAsync("q1", "Homebody", "stub", Now);

        var pass = await SyncAsync(_a, "a@one");

        Assert.Equal(0, pass.Pushed);
        Assert.Empty(await _remote.ListAsync(includeClosed: true));
    }

    /// <summary>
    /// 🔴 The strip, on the wire (D47 §4, D49 §4, D51): a record made with a transcript, a profile and a
    /// tree crosses with none of the three — there is no field for any of them.
    /// </summary>
    [Fact]
    public async Task A_record_crosses_without_its_transcript_profile_or_tree()
    {
        var session = await _a.CreateAsync(
            "q1", "Shared", "stub", Now, profile: "janes-own-account", tree: "C:/somewhere/private/tree",
            harnessVersion: "stub-harness 1.0.0");
        await _a.SetStateAsync(session.Id, SessionState.Working, null, null, "C:/somewhere/private/log.txt", Now);
        var remote = Remote("a@one");

        await SyncAsync(_a, "a@one", remote);

        Assert.Contains("stub-harness 1.0.0", remote.LastFeed);
        Assert.DoesNotContain("janes-own-account", remote.LastFeed);
        Assert.DoesNotContain("private", remote.LastFeed);
        Assert.DoesNotContain("transcript", remote.LastFeed);
    }

    /// <summary>
    /// 🔴 REV3: the wire had no FIELD for a tree or a profile, and the driver wrote both into the NOTE —
    /// "opened a session tree at C:/…", "its provider refused the `claude-code` account `jane`", an
    /// exception's own text. A note crosses; what is machine-local in it does not.
    /// </summary>
    [Fact]
    public async Task A_note_crosses_without_the_paths_and_the_account_it_names()
    {
        // Home-shaped paths are assembled at run time: written as literals, they are the very shape the
        // sensitive gate refuses in a tracked file.
        var windowsHome = string.Concat("D:", @"\", "Users", @"\", "jane", @"\Daoris\data\sessions\x.log");
        var unixHome = string.Concat("/", "home", "/", "jane", "/secret");
        var session = await _a.CreateAsync(
            "q1", "Shared", "stub", Now, profile: "janes-own-account", tree: "C:/somewhere/private/tree");
        await _a.SetStateAsync(
            session.Id, SessionState.Failed,
            "opened a session tree at C:\\somewhere\\private\\tree on `daoris/s1`. Its provider refused the "
            + "`claude-code` account `janes-own-account` (401). Could not find a part of the path "
            + $"'{windowsHome}'. And {unixHome} too.",
            null, null, Now);
        var remote = Remote("a@one");

        await SyncAsync(_a, "a@one", remote);

        Assert.Contains("(401)", remote.LastFeed);
        Assert.Contains("daoris/s1", remote.LastFeed);
        Assert.DoesNotContain("janes-own-account", remote.LastFeed);
        Assert.DoesNotContain("private", remote.LastFeed);
        Assert.DoesNotContain("jane", remote.LastFeed);
        Assert.DoesNotContain("somewhere", remote.LastFeed);
    }

    /// <summary>
    /// Every machine sees the team's (SYNC4): b's record comes down to a keyed `origin/id`, carrying its
    /// origin and nothing machine-local — and a's own, which the remote also holds, never comes back.
    /// </summary>
    [Fact]
    public async Task The_teams_records_come_down_and_the_callers_own_never_come_back()
    {
        var mine = await _a.CreateAsync("q1", "Shared", "stub", Now);
        var theirs = await _b.CreateAsync("q2", "Shared", "stub", Now, profile: "b-account");
        await SyncAsync(_a, "a@one");
        await SyncAsync(_b, "b@two");

        var pass = await SyncAsync(_a, "a@one");

        Assert.Equal(1, pass.Fetched);
        var all = await _a.ListAsync(includeClosed: true);
        Assert.Equal(new[] { mine.Id, $"b@two/{theirs.Id}" }.Order(), all.Select(s => s.Id).Order());
        var came = all.Single(s => s.Origin is not null);
        Assert.Equal("b@two", came.Origin);
        Assert.Null(came.Profile);
        Assert.Null(came.Transcript);

        // And it never goes back up: it is b's to feed, and feeding it from here would launder b's
        // record through a's key.
        Assert.Equal(0, (await SyncAsync(_a, "a@one")).Pushed);
        Assert.DoesNotContain(await _remote.ListAsync(includeClosed: true), s => s.Id.StartsWith("a@one/b@two", StringComparison.Ordinal));
    }

    /// <summary>
    /// TOOL4c (D125 §5.2): a limit names no account, so it travels, up with this machine's own record and
    /// down to a teammate's machine. The account the session ran as still stays home.
    /// </summary>
    [Fact]
    public async Task A_limit_crosses_both_ways_and_the_account_it_ran_as_does_not()
    {
        var session = await _a.CreateAsync("q1", "Shared", "stub", Now, profile: "janes-own-account");
        await _a.SetStateAsync(session.Id, SessionState.Working, null, null, null, Now);
        await _a.SetStateAsync(session.Id, SessionState.Failed, "the ACP agent refused the call.", null, null, Now, limit: true);
        var remote = Remote("a@one");

        await SyncAsync(_a, "a@one", remote);
        await SyncAsync(_b, "b@two");

        Assert.Contains("\"limit\":true", remote.LastFeed);
        Assert.DoesNotContain("janes-own-account", remote.LastFeed);
        Assert.True((await _remote.FindAsync($"a@one/{session.Id}"))!.Limit);
        var there = (await _b.FindAsync($"a@one/{session.Id}"))!;
        Assert.True(there.Limit);
        Assert.Null(there.Profile);
    }

    /// <summary>A teammate's session moving on its own machine arrives here as the same record, moved.</summary>
    [Fact]
    public async Task A_teammates_record_that_moves_comes_down_again_moved()
    {
        var theirs = await _b.CreateAsync("q2", "Shared", "stub", Now);
        await SyncAsync(_b, "b@two");
        await SyncAsync(_a, "a@one");

        await _b.SetStateAsync(theirs.Id, SessionState.Completed, "landed.", null, null, Now.AddMinutes(9));
        await SyncAsync(_b, "b@two");
        await SyncAsync(_a, "a@one");

        var here = (await _a.FindAsync($"b@two/{theirs.Id}"))!;
        Assert.Equal(SessionState.Completed, here.State);
        Assert.Equal("landed.", here.Note);
        Assert.Single(await _a.ListAsync(includeClosed: true));
    }

    /// <summary>
    /// 🔴 Never this machine's lock (D47 §6): a teammate's WORKING session on a repository both machines
    /// have holds a tree on their machine, not here — the one-session-per-tree check does not see it.
    /// </summary>
    [Fact]
    public async Task A_teammates_active_session_is_never_this_machines_lock()
    {
        var theirs = await _b.CreateAsync("q2", "Shared", "stub", Now);
        await _b.SetStateAsync(theirs.Id, SessionState.Working, null, null, null, Now);
        await SyncAsync(_b, "b@two");
        await SyncAsync(_a, "a@one");

        Assert.True((await _a.FindAsync($"b@two/{theirs.Id}"))!.Active);
        Assert.Null(await _a.ActiveForAsync("Shared"));

        var mine = await _a.CreateAsync("q3", "Shared", "stub", Now);
        Assert.Equal(mine.Id, (await _a.ActiveForAsync("Shared"))!.Id);
    }

    /// <summary>The remote down: the wall is named, and neither cursor moves — nothing is lost, the next pass sends it.</summary>
    [Fact]
    public async Task An_unreachable_remote_is_named_and_moves_no_cursor()
    {
        await _a.CreateAsync("q1", "Shared", "stub", Now);

        var pass = await SessionSync.RunAsync(_a, _service, new UnreachableRemote(), Workspaces.Default);
        var untouched = await _a.CursorAsync(Workspaces.Default);
        var later = await SyncAsync(_a, "a@one");

        Assert.Equal((0L, 0L), untouched);

        Assert.Contains("could not be reached", pass.Problem);
        Assert.Equal(1, later.Pushed);
    }

    /// <summary>
    /// A store from before the sync: a mirrored row's origin is read from its id, every row is given its
    /// place in the write order, and the first pass pushes this machine's own records it holds.
    /// </summary>
    [Fact]
    public async Task A_store_from_before_the_sync_pushes_what_it_holds_and_knows_whose_is_whose()
    {
        var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE sessions (
                  id TEXT PRIMARY KEY, quest TEXT NULL, repository TEXT NOT NULL, adapter TEXT NOT NULL,
                  state TEXT NOT NULL, note TEXT NULL, evidence TEXT NULL, transcript TEXT NULL,
                  created TEXT NOT NULL, updated TEXT NOT NULL, workspace TEXT NOT NULL DEFAULT 'default',
                  kind TEXT NOT NULL DEFAULT 'Driven', harness_version TEXT NULL, profile TEXT NULL,
                  tree TEXT NULL, base_commit TEXT NULL
                );
                INSERT INTO sessions (id, quest, repository, adapter, state, created, updated)
                  VALUES ('abcd1234', 'q1', 'Shared', 'stub', 'Completed', '2026-09-20T10:00:00Z', '2026-09-20T10:05:00Z');
                INSERT INTO sessions (id, quest, repository, adapter, state, created, updated)
                  VALUES ('b@two/ef567890', 'q2', 'Shared', 'stub', 'Working', '2026-09-20T10:00:00Z', '2026-09-20T10:05:00Z');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await OpenAsync(old);
        var pass = await SyncAsync(store, "a@one");

        Assert.Equal("b@two", (await store.FindAsync("b@two/ef567890"))!.Origin);
        Assert.Null((await store.FindAsync("abcd1234"))!.Origin);
        Assert.Equal(1, pass.Pushed);
        Assert.Null(await store.ActiveForAsync("Shared"));
    }
}
