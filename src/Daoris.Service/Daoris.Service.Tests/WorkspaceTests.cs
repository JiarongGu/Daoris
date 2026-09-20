using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// The workspace is the unit of sharing (D48). These are its rules at the layer that decides them —
/// the name itself, the wiring that carries it, and the scoping every cross-repository answer obeys.
/// </summary>
/// <remarks>
/// Written as one suite deliberately: the workspace is one rule appearing in five stores, and the
/// failure mode worth guarding is exactly the one where four of them agree and the fifth does not.
/// </remarks>
public sealed class WorkspaceNameTests
{
    /// <summary>
    /// Silence is `default`, everywhere. A machine that never names a workspace runs exactly as it did
    /// before workspaces existed (design §2a) — which is what makes the local deployment still work
    /// alone, with no server, no key and no wiring.
    /// </summary>
    [Fact]
    public void Silence_is_the_default_workspace()
    {
        Assert.Equal("default", Workspaces.Default);
        Assert.Equal("default", Workspaces.Normalize(null));
        Assert.Equal("default", Workspaces.Normalize(""));
        Assert.Equal("default", Workspaces.Normalize("   "));
    }

    /// <summary>
    /// A name is compared the way a person types it — case-insensitively, trimmed. Two workspaces that
    /// differ only in case would be a boundary nobody could see, and an invisible boundary is worse
    /// than none: it is a refusal with no explanation.
    /// </summary>
    [Fact]
    public void A_name_is_trimmed_and_compared_case_insensitively()
    {
        Assert.Equal("aurora", Workspaces.Normalize("  aurora  "));
        Assert.True(Workspaces.Same("Aurora", "aurora"));
        Assert.True(Workspaces.Same(null, "DEFAULT"));
        Assert.False(Workspaces.Same("aurora", "tools"));
    }
}

/// <summary>
/// Membership is wiring, like a git remote — a row on this machine, never a tracked declaration
/// (D48 as amended). The store is where "preserved on upsert, defaulting to the existing row then
/// `default`" is actually true, because it is the only place both the old row and the new statement
/// are in hand at once.
/// </summary>
public sealed class WorkspaceWiringTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private RegistrationStore _store = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = await RegistrationStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static Registration Declared(string name = "engine", string? workspace = null) => new(
        name, Adopted: true, "Owns the engine.", ["rendering"], ["a bug"], [], Entries: 0,
        Workspace: workspace);

    [Fact]
    public async Task A_registration_with_no_workspace_lands_in_default()
    {
        await _store.UpsertAsync(Declared(), Now);

        Assert.Equal("default", Assert.Single(await _store.AllAsync()).Workspace);
    }

    [Fact]
    public async Task A_named_workspace_round_trips()
    {
        await _store.UpsertAsync(Declared(workspace: "aurora"), Now);

        Assert.Equal("aurora", Assert.Single(await _store.AllAsync()).Workspace);
    }

    /// <summary>
    /// The wiring survives a `connect` that says nothing about it. Every ordinary re-registration —
    /// every sync tick, every manifest edit — carries no workspace, and if silence overwrote the row
    /// a repository would fall back into `default` the first time anything re-registered it.
    /// </summary>
    [Fact]
    public async Task Re_registering_without_naming_a_workspace_preserves_the_row()
    {
        await _store.UpsertAsync(Declared(workspace: "aurora"), Now);
        await _store.UpsertAsync(Declared() with { Summary = "Revised." }, Now.AddDays(1));

        var read = Assert.Single(await _store.AllAsync());
        Assert.Equal("aurora", read.Workspace);
        Assert.Equal("Revised.", read.Summary);
    }

    /// <summary>Re-pointing is a statement, and a statement wins — `git remote set-url`, not a merge.</summary>
    [Fact]
    public async Task Naming_a_different_workspace_re_points_the_row()
    {
        await _store.UpsertAsync(Declared(workspace: "aurora"), Now);
        await _store.UpsertAsync(Declared(workspace: "tools"), Now.AddDays(1));

        Assert.Equal("tools", Assert.Single(await _store.AllAsync()).Workspace);
    }

    /// <summary>The upsert answers with the row as it now stands, so the caller never has to guess.</summary>
    [Fact]
    public async Task The_upsert_answers_the_effective_row()
    {
        await _store.UpsertAsync(Declared(workspace: "aurora"), Now);

        var effective = await _store.UpsertAsync(Declared(), Now.AddDays(1));

        Assert.Equal("aurora", effective.Workspace);
    }

    /// <summary>
    /// A store written before workspaces existed must survive the upgrade with its registrations
    /// intact and in `default` — the same property the root and remote columns already hold, for the
    /// same reason: a schema that only works on a fresh database drops every connected repository.
    /// </summary>
    [Fact]
    public async Task An_existing_store_without_the_workspace_column_is_migrated_in_place()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE registrations (
                  repository TEXT PRIMARY KEY, summary TEXT NULL, owns TEXT NOT NULL,
                  accepts TEXT NOT NULL, packs TEXT NOT NULL, updated TEXT NOT NULL
                );
                INSERT INTO registrations VALUES ('Elder', 'Was here first.', '[]', '[]', '[]', '2026-01-01');
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await RegistrationStore.OpenAsync(old);
        var elder = Assert.Single(await store.AllAsync());

        Assert.Equal("default", elder.Workspace);
    }
}

/// <summary>
/// The ambient scope: which circle a session is speaking for, resolved from where it is running
/// against the machine's registry (design §4). Nothing in a repository's tree says this — membership
/// is wiring — so the path match IS the resolution, and its edges are the whole risk.
/// </summary>
public sealed class AmbientWorkspaceTests
{
    private static readonly IReadOnlyList<Registration> Registry =
    [
        new("game", true, "g", [], [], [], 0, Root: "D:/repos/game", Workspace: "aurora"),
        new("game-tools", true, "t", [], [], [], 0, Root: "D:/repos/game-tools", Workspace: "tools"),
        new("vendored", true, "v", [], [], [], 0, Root: "D:/repos/game/vendor/thing", Workspace: "vendor"),
        new("pathless", true, "p", [], [], [], 0, Root: null, Workspace: "nowhere"),
    ];

    [Fact]
    public void A_session_in_a_repository_resolves_to_its_workspace()
    {
        Assert.Equal("game", AmbientWorkspace.Containing(Registry, "D:/repos/game")!.Repository);
        Assert.Equal("game", AmbientWorkspace.Containing(Registry, "D:/repos/game/src/deep")!.Repository);
    }

    /// <summary>
    /// A sibling whose name merely STARTS the same way is a different circle entirely. A prefix test
    /// would put a `game-tools` session in `game`'s workspace — and it would look like it worked.
    /// </summary>
    [Fact]
    public void A_sibling_sharing_a_prefix_is_not_inside()
    {
        Assert.Equal("game-tools", AmbientWorkspace.Containing(Registry, "D:/repos/game-tools/src")!.Repository);
    }

    /// <summary>A checkout inside a checkout is a real layout, and the inner one is where the session is.</summary>
    [Fact]
    public void The_innermost_registered_root_wins()
    {
        Assert.Equal("vendored", AmbientWorkspace.Containing(Registry, "D:/repos/game/vendor/thing/x")!.Repository);
    }

    /// <summary>Separators and case differ between a registration and a process's own directory.</summary>
    [Fact]
    public void Separators_and_case_do_not_decide_it()
    {
        Assert.Equal("game", AmbientWorkspace.Containing(Registry, @"D:\Repos\Game\src")!.Repository);
        Assert.Equal("game", AmbientWorkspace.Containing(Registry, "D:/repos/game/")!.Repository);
    }

    /// <summary>
    /// Somewhere unregistered has no circle to default to. Null is the answer, and the caller's job is
    /// to say it spanned everything rather than quietly pick one.
    /// </summary>
    [Fact]
    public void Somewhere_unregistered_resolves_to_nothing()
    {
        Assert.Null(AmbientWorkspace.Containing(Registry, "D:/elsewhere"));
        Assert.Null(AmbientWorkspace.Containing(Registry, ""));
    }
}

/// <summary>
/// What a workspace BOUNDS (design §4): every cross-repository answer is scoped to one. Everything
/// inside a single repository is unchanged, which is why none of these tests are about a repository.
/// </summary>
public sealed class WorkspaceScopeTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), $"daoris-workspace-{Guid.NewGuid():N}.db");

    private SqliteKnowledgeStore _store = null!;

    public async Task InitializeAsync()
    {
        _store = await SqliteKnowledgeStore.OpenAsync(_file);

        // Two circles on one machine: a game family and a tools family. The lesson is deliberately
        // the SAME in both, because the thing being proven is that the boundary holds even when the
        // text would have matched.
        await _store.ReplaceRepositoryAsync("engine", [
            new KnowledgeEntry("engine", EntryKind.Decision, Provenance.Local,
                "D1 — chunk hydration", "Cap hydration work per frame.", "docs/DECISIONS.md", "D1",
                Workspace: "aurora"),
        ]);
        await _store.ReplaceRepositoryAsync("ledger", [
            new KnowledgeEntry("ledger", EntryKind.Decision, Provenance.Local,
                "D1 — chunk hydration", "Cap hydration work per frame.", "docs/DECISIONS.md", "D1",
                Workspace: "tools"),
        ]);
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    [Fact]
    public async Task An_entry_round_trips_with_its_workspace()
    {
        var found = await _store.FindAsync("engine:docs/DECISIONS.md#D1");

        Assert.Equal("aurora", found!.Workspace);
    }

    /// <summary>
    /// An agent asking "has anyone solved this" means its own circle. Answering with another circle's
    /// material is the disclosure the boundary exists to prevent — and the search is where it would
    /// happen first, because search is what every session runs before anything else.
    /// </summary>
    [Fact]
    public async Task Search_scoped_to_a_workspace_sees_only_that_workspace()
    {
        var search = new SqliteKnowledgeSearch(_store);

        var aurora = await search.SearchAsync(new KnowledgeQuery("hydration") { Workspace = "aurora" });
        var tools = await search.SearchAsync(new KnowledgeQuery("hydration") { Workspace = "tools" });
        var unscoped = await search.SearchAsync(new KnowledgeQuery("hydration"));

        Assert.Equal("engine", Assert.Single(aurora).Entry.Repository);
        Assert.Equal("ledger", Assert.Single(tools).Entry.Repository);
        Assert.Equal(2, unscoped.Count);
    }

    /// <summary>A browse — no terms — takes the same filter, or the boundary has a hole in it.</summary>
    [Fact]
    public async Task A_browse_is_scoped_too()
    {
        var search = new SqliteKnowledgeSearch(_store);

        var browsed = await search.SearchAsync(new KnowledgeQuery("") { Workspace = "tools" });

        Assert.Equal("ledger", Assert.Single(browsed).Entry.Repository);
    }

    /// <summary>
    /// Convergence across workspaces is not convergence — it is two circles that never had to agree.
    /// Left unscoped, identical doctrine in unrelated families would be reported as a prompt to make
    /// shared canon out of material neither one shares.
    /// </summary>
    [Fact]
    public async Task Convergence_does_not_cross_the_boundary()
    {
        var detector = new ConvergenceDetector(_store);

        var crossing = await detector.FindAsync(new ConvergenceOptions(Workspace: "aurora"));
        var unscoped = await detector.FindAsync(new ConvergenceOptions());

        Assert.Empty(crossing);
        Assert.Single(unscoped);
    }
}

/// <summary>
/// The quest boundary (design §4): publishing checks that both sides share a workspace, and the
/// refusal names both — because "no" that does not say which side is where leaves the asker with the
/// one question the answer was supposed to settle.
/// </summary>
public sealed class WorkspaceQuestTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-wsquest-" + Guid.NewGuid().ToString("N")[..8]);

    private SqliteConnection _connection = null!;
    private QuestStore _quests = null!;
    private QuestExchange _exchange = null!;
    private KnowledgeService _service = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T10:00:00Z");

    public async Task InitializeAsync()
    {
        foreach (var name in new[] { "engine", "game", "ledger" })
        {
            var directory = Path.Combine(_root, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "daoris.json"),
                $$"""{ "source": "s", "packs": [], "domain": { "summary": "{{name}}.", "owns": ["x"], "accepts": ["y"] } }""");
        }

        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _quests = await QuestStore.OpenAsync(_connection);

        var store = new InMemoryKnowledgeStore();
        _service = new KnowledgeService(
            store, new LexicalKnowledgeSearch(store), new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly, registry: new Registry());

        // The wiring: two of them in one circle, one in another. Nothing about this is in any manifest.
        foreach (var (name, workspace) in new[] { ("engine", "aurora"), ("game", "aurora"), ("ledger", "tools") })
        {
            await _service.RegisterAsync(
                new Registration(name, Adopted: true, $"{name}.", ["x"], ["y"], [], Entries: 0,
                    Workspace: workspace), Now);
        }

        _exchange = new QuestExchange(_service, _quests);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_quest_within_one_workspace_is_published()
    {
        var outcome = await _exchange.PublishAsync(
            "game", "engine", "Expose a streaming budget", "Here is why.", Now);

        Assert.Equal(QuestPublishRefusal.None, outcome.Refusal);
        Assert.Equal("aurora", outcome.Quest!.Workspace);
    }

    [Fact]
    public async Task A_quest_across_workspaces_is_refused_naming_both_sides()
    {
        var outcome = await _exchange.PublishAsync(
            "game", "ledger", "Do the thing", "Here is why.", Now);

        Assert.Equal(QuestPublishRefusal.CrossWorkspace, outcome.Refusal);
        Assert.Null(outcome.Quest);
        Assert.Contains("aurora", outcome.Message);
        Assert.Contains("tools", outcome.Message);
        Assert.Contains("game", outcome.Message);
        Assert.Contains("ledger", outcome.Message);
    }

    /// <summary>
    /// The refusal is about the boundary, not about who exists — so it must not read as "there is
    /// nobody to ask". Addressability lists this asker's own circle, which is what it can act on.
    /// </summary>
    [Fact]
    public async Task The_cross_workspace_refusal_offers_the_askers_own_circle()
    {
        var outcome = await _exchange.PublishAsync(
            "game", "ledger", "Do the thing", "Here is why.", Now);

        Assert.Contains("engine", outcome.Addressable);
        Assert.DoesNotContain("ledger", outcome.Addressable);
    }

    /// <summary>A quest list scoped to a workspace answers that circle's work and no other's.</summary>
    [Fact]
    public async Task The_quest_list_is_scoped()
    {
        await _exchange.PublishAsync("game", "engine", "Within", "Here is why.", Now);
        await _quests.PublishAsync("ledger", "ledger-two", "Elsewhere", "b", Now, workspace: "tools");

        var aurora = await _quests.ListAsync(workspace: "aurora");
        var tools = await _quests.ListAsync(workspace: "tools");

        Assert.Equal("Within", Assert.Single(aurora).Title);
        Assert.Equal("Elsewhere", Assert.Single(tools).Title);
    }

    /// <summary>
    /// A session belongs to its quest's circle. It is derived rather than passed so the two can never
    /// disagree — a record filed under a workspace its quest does not belong to would be a leak the
    /// next scoped list performs on someone's behalf.
    /// </summary>
    [Fact]
    public async Task A_session_takes_its_workspace_from_its_quest()
    {
        var published = await _exchange.PublishAsync("game", "engine", "Within", "Here is why.", Now);
        var sessions = await SessionStore.OpenAsync(_connection);

        var opened = await new SessionLedger(_quests, sessions)
            .OpenAsync(published.Quest!.Id, "stub", Now);

        Assert.Equal("aurora", opened.Session!.Workspace);
        Assert.Single(await sessions.ListAsync(workspace: "aurora"));
        Assert.Empty(await sessions.ListAsync(workspace: "tools"));
    }
}
