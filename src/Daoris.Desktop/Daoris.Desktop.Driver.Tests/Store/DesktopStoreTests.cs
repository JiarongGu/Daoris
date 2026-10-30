using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 z-i (D212, the design §13.2 to §13.4, §13.6's row z-i): the desktop store, read by nothing. Its file in the home's table,
/// its four tables, its version and writer stamp, its revisions and the wake file a commit touches, its runs, its imports, and the
/// guards that keep a build rolled back before its confirmation whole: a store stamped ahead refused for writes, the stamp read inside every
/// write's own transaction, a stamp-raising migration and every import held until the start's confirmation is settled, a migration
/// interrupted at each step leaving the store as it was.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment, <c>DAORIS_HOME</c> least.
/// "A newer build" is a second store opened with a higher writer stamp and a step that raises it, so two builds meet in one process the way
/// two processes' builds meet on a home. Nothing here starts a process; the git door's start is <c>GitDoorStartTests</c>'.
/// </remarks>
public sealed class DesktopStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "desktop-store", Guid.NewGuid().ToString("N")[..8]);

    private readonly List<IDisposable> _open = [];

    public DesktopStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var each in _open) each.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_root);
    }

    private string Home => Path.Combine(_root, "home");

    private string Db => Path.Combine(_root, "desktop.db");

    private DesktopStore Open(DesktopStoreOptions? options = null, string? at = null)
    {
        var store = DesktopStore.Open(at ?? Db, options);
        _open.Add(store);
        return store;
    }

    /// <summary>The schema version this build's own steps leave a store at: the steps a row adds move it, and these cases are of the mechanism, not of the count.</summary>
    private static readonly int Own = DesktopStore.Steps.Count;

    /// <summary>The step after the build's own, which raises the writer stamp to 2: what a newer build's first start would migrate to.</summary>
    private static readonly StoreMigration Raises = new(Own + 1, 2, write => write.Execute("CREATE TABLE raised (x TEXT NOT NULL)"));

    /// <summary>The step after the build's own that adds a table and leaves the stamp where it is.</summary>
    private static readonly StoreMigration Adds = new(Own + 1, 1, write => write.Execute("CREATE TABLE added (x TEXT NOT NULL)"));

    private static DesktopStoreOptions Newer(Func<StoreSettled>? settled = null, string kind = "shell") => new()
    {
        Kind = kind,
        Writer = 2,
        Steps = [.. DesktopStore.Steps, Raises],
        Settled = settled ?? (() => StoreSettled.Yes),
    };

    private static DesktopStoreOptions Older(string kind = "verb") => new() { Kind = kind, Settled = () => StoreSettled.Yes };

    private static long Count(DesktopStore store, string table) => store.Query($"SELECT COUNT(*) FROM {table}", reader => reader.GetInt64(0))[0];

    private static bool HasTable(DesktopStore store, string table) =>
        store.Query("SELECT name FROM sqlite_master WHERE type = 'table' AND name = $name", reader => reader.GetString(0), ("$name", table)).Count == 1;

    // ——— The file and its tables (§13.2, §13.3).

    [Fact]
    public void The_store_is_the_home_s_desktop_entry_and_is_made_with_its_four_tables_at_version_1()
    {
        var at = DesktopStore.LocationIn(Home);

        Assert.Equal(DaorisHome.Entry(Home, HomeEntry.DesktopDb), at);
        Assert.Equal("desktop.db", Path.GetFileName(at));
        Assert.Equal("data", Path.GetFileName(Path.GetDirectoryName(at)));
        var store = Open(at: at);

        Assert.True(File.Exists(at));
        Assert.Equal((Own, 1), store.Stamp);
        Assert.Null(store.Held);
        Assert.False(store.Newer);
        foreach (var table in new[] { "store_version", "store_revisions", "store_runs", "imports" }) Assert.True(HasTable(store, table), table);
        Assert.Empty(store.Revisions());
        Assert.Empty(store.Runs());
    }

    [Fact]
    public void The_connection_is_write_ahead_logged_and_synchronous_full()
    {
        var store = Open();

        Assert.Equal("wal", store.Query("PRAGMA journal_mode", reader => reader.GetString(0))[0]);
        Assert.Equal(2L, store.Query("PRAGMA synchronous", reader => reader.GetInt64(0))[0]);   // 2 is FULL
        Assert.Equal(5000L, store.Query("PRAGMA busy_timeout", reader => reader.GetInt64(0))[0]);
    }

    [Fact]
    public void Opening_again_migrates_nothing_and_keeps_what_the_first_wrote()
    {
        Open().Write("probe", write => write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('f', 'p', 1, 'x', 't', 'b', 0, 'left')"));

        var second = Open();

        Assert.Equal((Own, 1), second.Stamp);
        Assert.Equal(1, Count(second, "imports"));
    }

    [Fact]
    public void The_driver_pins_the_service_s_sqlite_versions()
    {
        var desktop = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Directory.Packages.props"));
        var service = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Service", "Directory.Packages.props"));

        foreach (var package in new[] { "Microsoft.Data.Sqlite", "SQLitePCLRaw.bundle_e_sqlite3" })
        {
            var pin = new Regex($"""<PackageVersion Include="{Regex.Escape(package)}" Version="([^"]+)" />""");
            Assert.Equal(pin.Match(service).Groups[1].Value, pin.Match(desktop).Groups[1].Value);
            Assert.NotEmpty(pin.Match(desktop).Groups[1].Value);
        }

        var driver = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Daoris.Desktop.Driver.csproj"));
        Assert.Contains("""<PackageReference Include="Microsoft.Data.Sqlite" />""", driver);
        Assert.DoesNotContain("ProjectReference", driver);
        Assert.DoesNotContain("PackageReference Include=\"Daoris", driver);
    }

    // ——— Step 4, the course's move record (FOUNDATION1f-i; §3.3, §13.3's family *The course*, §19.1).

    /// <summary>The columns of <c>moves</c> a writer always sets: every other is one a later phase, a later row or an effect sets.</summary>
    private static readonly string[] MoveRequired = ["record_left", "executor", "how", "from_why", "from_by", "phase", "claim_run", "claim_generation", "decided_at"];

    private static IReadOnlyList<(string Name, bool Required)> Columns(DesktopStore store, string table) =>
        store.Query($"PRAGMA table_info({table})", reader => (reader.GetString(1), reader.GetInt64(3) == 1));

    [Fact]
    public void Step_4_makes_the_moves_and_their_words_with_the_one_standing_index_and_raises_no_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == 4);
        Assert.Equal(1, step.Writer);
        Assert.Equal(1, DesktopStore.WriterStamp);

        var store = Open();

        Assert.Equal((Own, 1), store.Stamp);
        Assert.True(HasTable(store, "moves"));
        Assert.True(HasTable(store, "move_words"));
        var index = Assert.Single(store.Query("SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'moves_standing'", reader => reader.GetString(0)));
        Assert.Contains("CREATE UNIQUE INDEX moves_standing ON moves (record_left)", index);
        Assert.Contains("WHERE phase NOT IN ('delivered', 'fell-back', 'closed')", index);
    }

    [Fact]
    public void A_move_s_columns_a_writer_always_sets_are_required_and_every_later_one_is_nullable()
    {
        var store = Open();

        var moves = Columns(store, "moves");
        Assert.Equal(
            ["id", "record_left", "executor", "how", "from_why", "from_by", "phase", "claim_run", "claim_generation", "account", "recipe_why", "recipe_bound",
             "opens", "destination", "effect", "effect_begun", "effect_outcome", "effect_ended", "fell_back_code", "held_why", "closed_why",
             "decided_at", "quiescing_at", "old_ended_at", "opening_at", "opened_at", "copied_at", "verified_at", "delivering_at", "delivered_at",
             "fell_back_at", "held_at", "closed_at",
             // Step 5's, each nullable: a step-4 build writes moves as it did and leaves them empty.
             "agent", "refused_bound", "quiet_at"],
            moves.Select(column => column.Name));
        Assert.Equal(MoveRequired, moves.Where(column => column.Required).Select(column => column.Name));
        Assert.Equal(
            [("move", true), ("transfer", true), ("word", true), ("producer", false), ("position", true), ("disposition", false), ("disposed_at", false)],
            Columns(store, "move_words"));
    }

    [Fact]
    public void A_move_s_claim_names_a_run_the_store_holds_and_a_word_names_its_move()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        void Move(string run) => store.Write("moves", write => write.Execute(
            """
            INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, decided_at)
            VALUES ('s1', 'conversation', 'brief', 'limit', 'daoris', 'decided', $run, 1, '2026-10-29T00:00:00.0000000+00:00')
            """,
            ("$run", run)));

        // 787 is SQLite's foreign-key refusal: the claim names no run that ever wrote the store.
        Assert.Equal(787, Assert.Throws<SqliteException>(() => Move("no-such-run")).SqliteExtendedErrorCode);
        Assert.Equal(0, Count(store, "moves"));

        // The writer's own run is recorded inside the same write, before its work, so a claim it writes always names it.
        Move(store.Run);
        Assert.Equal(1, Count(store, "moves"));
        Assert.Equal(787, Assert.Throws<SqliteException>(() => store.Write("moves", write => write.Execute(
            "INSERT INTO move_words (move, transfer, word, position) VALUES (999, 't1', 'w1', 0)"))).SqliteExtendedErrorCode);
    }

    [Fact]
    public void A_store_of_the_three_steps_before_is_migrated_to_4_at_the_first_start_whatever_the_confirmation_says()
    {
        using (var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(3)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((3, 1), older.Stamp);
        }

        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("the swap journal is at `started`") });

        Assert.Null(store.Held);
        Assert.Equal((Own, 1), store.Stamp);
        Assert.True(HasTable(store, "moves"));
    }

    [Fact]
    public void A_build_of_the_three_steps_before_opens_the_migrated_store_still_writes_it_and_never_meets_a_move()
    {
        Open().Write("probe", _ => { });

        var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(3)], Settled = () => StoreSettled.Yes });

        Assert.False(older.Newer);
        Assert.Equal((Own, 1), older.Stamp);
        older.Write(BindingStore.Family, write => write.Execute(
            "INSERT INTO bindings (session, adapter, conversation, kept) VALUES ('s1', 'claude-code-acp', 'c-1', '2026-10-29T00:00:00.0000000Z')"));
        older.Write(BindingStore.Family, write => write.Execute(
            "INSERT INTO binding_picks (session, account, at) VALUES ('s1', 'work', '2026-10-29T00:00:00.0000000Z')"));
        Assert.Equal((1L, 1L), (Count(older, "bindings"), Count(older, "binding_picks")));
    }

    // ——— Step 5, what a box's move carries and a quiet move's telling (FOUNDATION1f-i part 2; §3.3, §13.5, §19.1).

    [Fact]
    public void Step_5_adds_the_agent_the_refused_turns_bound_and_the_quiet_s_moment_to_moves_nullable_and_raises_no_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == 5);
        Assert.Equal(1, step.Writer);
        using (var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(4)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((4, 1), older.Stamp);
        }

        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("the swap journal is at `started`") });

        Assert.Null(store.Held);
        Assert.Equal((Own, 1), store.Stamp);
        var added = Columns(store, "moves").Where(column => column.Name is "agent" or "refused_bound" or "quiet_at").ToList();
        Assert.Equal([("agent", false), ("refused_bound", false), ("quiet_at", false)], added);
    }

    [Fact]
    public void A_build_of_the_four_steps_before_still_writes_a_move_into_the_migrated_store_leaving_step_5_s_columns_empty()
    {
        Open().Write("probe", _ => { });

        var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(4)], Settled = () => StoreSettled.Yes });

        Assert.False(older.Newer);
        older.Write("moves", write => write.Execute(
            """
            INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, decided_at)
            VALUES ('s1', 'conversation', 'brief', 'limit', 'daoris', 'decided', $run, 1, '2026-10-30T00:00:00.0000000+00:00')
            """,
            ("$run", older.Run)));
        Assert.Equal(
            [(true, true, true)],
            older.Query("SELECT agent IS NULL, refused_bound IS NULL, quiet_at IS NULL FROM moves", reader => (reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2))));
    }

    // ——— Step 6, the integration's family (INTEGRATE1e part e2; the integration design §5.6, D212 §13).

    private static readonly string[] IntegrationTables = ["integration_requests", "integration_candidates", "integration_members", "integration_steps"];

    private static IReadOnlyList<string> Indexes(DesktopStore store, string table) =>
        store.Query(
            "SELECT sql FROM sqlite_master WHERE type = 'index' AND tbl_name = $table AND sql IS NOT NULL ORDER BY name",
            reader => reader.GetString(0), ("$table", table));

    [Fact]
    public void Step_6_makes_the_integration_s_four_tables_with_their_one_open_and_one_standing_indexes_and_raises_no_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == 6);
        Assert.Equal(1, step.Writer);

        var store = Open();

        Assert.Equal((Own, 1), store.Stamp);
        foreach (var table in IntegrationTables) Assert.True(HasTable(store, table), table);
        Assert.Contains(
            "CREATE UNIQUE INDEX integration_requests_open ON integration_requests (repository COLLATE NOCASE, branch) WHERE closed_at IS NULL",
            Indexes(store, "integration_requests"));
        Assert.Contains(
            "CREATE UNIQUE INDEX integration_candidates_standing ON integration_candidates (repository COLLATE NOCASE, line) WHERE ended_at IS NULL",
            Indexes(store, "integration_candidates"));
        Assert.Contains("CREATE UNIQUE INDEX integration_candidates_job ON integration_candidates (job)", Indexes(store, "integration_candidates"));
        Assert.Contains("CREATE UNIQUE INDEX integration_members_standing ON integration_members (request) WHERE ended_at IS NULL", Indexes(store, "integration_members"));
        Assert.Contains("CREATE UNIQUE INDEX integration_steps_open ON integration_steps (candidate) WHERE ended_at IS NULL", Indexes(store, "integration_steps"));
    }

    [Fact]
    public void The_integration_s_columns_every_writer_sets_are_required_and_a_route_s_or_a_later_step_s_own_are_nullable()
    {
        var store = Open();

        Assert.Equal(
            [
                ("id", true), ("repository", true), ("workspace", true), ("branch", true), ("tip", true), ("route", true), ("line", true),
                ("requester", true), ("door", true), ("act", false), ("generation", false),
                ("policy_request", true), ("policy_commit", true), ("policy_batch", true), ("policy_order", true), ("policy_gates", true),
                ("policy_unreached", true), ("policy_reached", true), ("policy_conflicts", true), ("policy_overlap", true), ("policy_line", false),
                ("rule_source", false), ("unit", false), ("session", false), ("quest", false), ("landed_at", false), ("proof", false),
                ("at", true), ("closed_at", false), ("closed_why", false),
            ],
            Columns(store, "integration_requests"));
        Assert.Equal(
            [
                ("id", true), ("repository", true), ("workspace", true), ("line", true), ("base", true), ("job", true), ("claim_run", true),
                ("claim_generation", true), ("formed_at", true), ("tip", false), ("promoted_at", false), ("ended_at", false), ("ended_why", false),
            ],
            Columns(store, "integration_candidates"));
        Assert.Equal(
            [
                ("candidate", true), ("request", true), ("position", true), ("state", true), ("joined_at", true), ("replayed", false),
                ("contained_by", false), ("said", false), ("ended_at", false),
            ],
            Columns(store, "integration_members"));
        Assert.Equal(
            [
                ("id", false), ("candidate", true), ("step", true), ("attempt", true), ("claim_run", true), ("claim_generation", true), ("began_at", true),
                ("request", false), ("line_commit", false), ("ended_at", false), ("outcome", false), ("said", false),
            ],
            Columns(store, "integration_steps"));
    }

    [Fact]
    public void A_candidate_s_claim_names_a_run_the_store_holds_and_its_members_and_steps_name_what_the_store_holds()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        const string At = "2026-10-30T00:00:00.0000000+00:00";
        void Candidate(string run) => store.Write("integration", write => write.Execute(
            """
            INSERT INTO integration_candidates (id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at)
            VALUES ('candidate-1', 'engine', 'aurora', 'main', 'abc', 'job-1', $run, 1, $at)
            """,
            ("$run", run), ("$at", At)));

        // 787 is SQLite's foreign-key refusal: the claim names no run that ever wrote the store.
        Assert.Equal(787, Assert.Throws<SqliteException>(() => Candidate("no-such-run")).SqliteExtendedErrorCode);
        Assert.Equal(0, Count(store, "integration_candidates"));
        Candidate(store.Run);

        Assert.Equal(787, Assert.Throws<SqliteException>(() => store.Write("integration", write => write.Execute(
            "INSERT INTO integration_members (candidate, request, position, state, joined_at) VALUES ('candidate-1', 'no-such-request', 0, 'formed', $at)",
            ("$at", At)))).SqliteExtendedErrorCode);
        Assert.Equal(787, Assert.Throws<SqliteException>(() => store.Write("integration", write => write.Execute(
            "INSERT INTO integration_steps (candidate, step, attempt, claim_run, claim_generation, began_at) VALUES ('no-such-candidate', 'form', 1, $run, 1, $at)",
            ("$run", store.Run), ("$at", At)))).SqliteExtendedErrorCode);
        Assert.Equal((0L, 0L), (Count(store, "integration_members"), Count(store, "integration_steps")));
    }

    [Fact]
    public void A_store_of_the_five_steps_before_is_migrated_to_6_at_the_first_start_whatever_the_confirmation_says()
    {
        using (var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(5)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((5, 1), older.Stamp);
        }

        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("the swap journal is at `started`") });

        Assert.Null(store.Held);
        Assert.Equal((Own, 1), store.Stamp);
        foreach (var table in IntegrationTables) Assert.True(HasTable(store, table), table);
    }

    [Fact]
    public void A_build_of_the_five_steps_before_opens_the_migrated_store_still_writes_a_move_and_never_meets_the_integration()
    {
        Open().Write("probe", _ => { });

        var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(5)], Settled = () => StoreSettled.Yes });

        Assert.False(older.Newer);
        Assert.Equal((Own, 1), older.Stamp);
        older.Write("moves", write => write.Execute(
            """
            INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, decided_at)
            VALUES ('s1', 'conversation', 'brief', 'limit', 'daoris', 'decided', $run, 1, '2026-10-30T00:00:00.0000000+00:00')
            """,
            ("$run", older.Run)));
        Assert.Equal(1, Count(older, "moves"));
        foreach (var table in IntegrationTables) Assert.Equal(0, Count(older, table));
    }

    // ——— Step 7, a superseded member's pending disposition (INTEGRATE1e part e4b; the integration design §5.7, D215's note).

    [Fact]
    public void Step_7_makes_the_pending_dispositions_table_keyed_by_request_and_commit_and_raises_no_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == 7);
        Assert.Equal(1, step.Writer);

        var store = Open();

        Assert.Equal((Own, 1), store.Stamp);
        Assert.True(HasTable(store, "integration_dispositions"));
        Assert.Equal(
            [
                ("request", true), ("commit_id", true), ("candidate", true), ("reference", true), ("by_commit", true), ("files", true),
                ("subject", true), ("at", true), ("answered_at", false),
            ],
            Columns(store, "integration_dispositions"));
        Assert.Contains(
            "CREATE INDEX integration_dispositions_pending ON integration_dispositions (at) WHERE answered_at IS NULL",
            Indexes(store, "integration_dispositions"));
    }

    [Fact]
    public void A_disposition_names_a_request_and_a_candidate_the_store_holds_and_is_one_row_for_each_request_and_commit()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        const string At = "2026-10-30T00:00:00.0000000+00:00";
        void Disposition(string request, string candidate, string commit = "c1") => store.Write("integration", write => write.Execute(
            """
            INSERT INTO integration_dispositions (request, commit_id, candidate, reference, by_commit, files, subject, at)
            VALUES ($request, $commit, $candidate, 'refs/daoris/discarded/feature/a', 'b1', '["a.txt"]', 'work', $at)
            """,
            ("$request", request), ("$commit", commit), ("$candidate", candidate), ("$at", At)));

        // 787 is SQLite's foreign-key refusal: no request, and no candidate, the store holds.
        Assert.Equal(787, Assert.Throws<SqliteException>(() => Disposition("no-such-request", "no-such-candidate")).SqliteExtendedErrorCode);
        Assert.Equal(0, Count(store, "integration_dispositions"));

        store.Write("integration", write => write.Execute(
            """
            INSERT INTO integration_requests (id, repository, workspace, branch, tip, route, line, requester, door, policy_request, policy_commit, policy_batch,
                policy_order, policy_gates, policy_unreached, policy_reached, policy_conflicts, policy_overlap, at)
            VALUES ('r1', 'engine', 'aurora', 'feature/a', 'abc', 'recorded', 'main', 'person', 'card', 'owner', 'merge', 1, 'arrival', 'declared', 'split', 'split',
                'session', 'hold', $at);
            INSERT INTO integration_candidates (id, repository, workspace, line, base, job, claim_run, claim_generation, formed_at)
            VALUES ('candidate-1', 'engine', 'aurora', 'main', 'abc', 'job-1', $run, 1, $at);
            """,
            ("$run", store.Run), ("$at", At)));
        Disposition("r1", "candidate-1");

        Assert.Equal(1, Count(store, "integration_dispositions"));
        // 1555 is SQLite's primary-key refusal: a request's commit has one disposition.
        Assert.Equal(1555, Assert.Throws<SqliteException>(() => Disposition("r1", "candidate-1")).SqliteExtendedErrorCode);
        Disposition("r1", "candidate-1", "c2");
        Assert.Equal(2, Count(store, "integration_dispositions"));
    }

    [Fact]
    public void A_store_of_the_six_steps_before_is_migrated_to_7_at_the_first_start_whatever_the_confirmation_says()
    {
        using (var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(6)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((6, 1), older.Stamp);
        }

        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("the swap journal is at `started`") });

        Assert.Null(store.Held);
        Assert.Equal((Own, 1), store.Stamp);
        Assert.True(HasTable(store, "integration_dispositions"));
        foreach (var table in IntegrationTables) Assert.True(HasTable(store, table), table);
    }

    [Fact]
    public void A_build_of_the_six_steps_before_opens_the_migrated_store_still_writes_a_move_and_never_meets_the_dispositions()
    {
        Open().Write("probe", _ => { });

        var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(6)], Settled = () => StoreSettled.Yes });

        Assert.False(older.Newer);
        Assert.Equal((Own, 1), older.Stamp);
        older.Write("moves", write => write.Execute(
            """
            INSERT INTO moves (record_left, executor, how, from_why, from_by, phase, claim_run, claim_generation, decided_at)
            VALUES ('s1', 'conversation', 'brief', 'limit', 'daoris', 'decided', $run, 1, '2026-10-30T00:00:00.0000000+00:00')
            """,
            ("$run", older.Run)));
        Assert.Equal(1, Count(older, "moves"));
        foreach (var table in IntegrationTables) Assert.Equal(0, Count(older, table));
        Assert.Equal(0, Count(older, "integration_dispositions"));
    }

    // ——— Step 10, the role catalogue (FOUNDATION1w; the foundation design §2.7, §13.3).

    [Fact]
    public void Step_10_makes_the_roles_and_their_revisions_with_required_columns_and_raises_no_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == 10);
        Assert.Equal(1, step.Writer);

        var store = Open();

        Assert.Equal((Own, 1), store.Stamp);
        Assert.Equal([("id", true), ("state", true), ("record", true), ("revision", true), ("at", true)], Columns(store, "roles"));
        Assert.Equal(
            [("id", true), ("revision", true), ("state", true), ("record", false), ("at", true)],
            Columns(store, "role_revisions"));
    }

    // ——— Step 8, the roles' staffing (FOUNDATION1w-s part 1; the foundation design §19.15, D212's note).

    [Fact]
    public void Step_8_makes_the_staffing_its_one_projection_row_and_the_found_rows_with_required_columns_and_raises_no_stamp()
    {
        var step = Assert.Single(DesktopStore.Steps, each => each.Version == 8);
        Assert.Equal(1, step.Writer);

        var store = Open();

        Assert.Equal((Own, 1), store.Stamp);
        Assert.Equal(
            [("role", true), ("state", true), ("choices", false), ("why", false), ("first_agent", false), ("at", true)],
            Columns(store, "role_staffing"));
        Assert.Equal(
            [("one", false), ("actors", false), ("adapter", false), ("intake_adapter", false), ("helper_adapter", false), ("at", true)],
            Columns(store, "role_staffing_projection"));
        Assert.Equal(
            [
                ("id", false), ("at", true), ("actors", false), ("adapter", false), ("intake_adapter", false), ("helper_adapter", false),
                ("written_actors", false), ("written_adapter", false), ("written_intake_adapter", false), ("written_helper_adapter", false),
                ("answer", false), ("answered_at", false),
            ],
            Columns(store, "role_staffing_found"));
        Assert.Contains(
            "CREATE INDEX role_staffing_found_pending ON role_staffing_found (id) WHERE answered_at IS NULL",
            Indexes(store, "role_staffing_found"));
    }

    [Fact]
    public void The_projection_is_one_row_whatever_the_writer_sets()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        void Project(int one) => store.Write("role_staffing", write => write.Execute(
            "INSERT INTO role_staffing_projection (one, at) VALUES ($one, '2026-10-30T00:00:00.0000000+00:00')", ("$one", one)));

        Project(1);

        Assert.Equal(275, Assert.Throws<SqliteException>(() => Project(2)).SqliteExtendedErrorCode);
        Assert.Equal(1555, Assert.Throws<SqliteException>(() => Project(1)).SqliteExtendedErrorCode);
        Assert.Equal(1, Count(store, "role_staffing_projection"));
    }

    [Fact]
    public void A_store_of_the_seven_steps_before_is_migrated_to_8_at_the_first_start_whatever_the_confirmation_says()
    {
        using (var older = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps.Take(7)], Settled = () => StoreSettled.Yes }))
        {
            Assert.Equal((7, 1), older.Stamp);
        }

        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("the swap journal is at `started`") });

        Assert.Null(store.Held);
        Assert.Equal((Own, 1), store.Stamp);
        foreach (var table in new[] { "role_staffing", "role_staffing_projection", "role_staffing_found" }) Assert.True(HasTable(store, table), table);
    }

    // ——— A run entered with no family's write (FOUNDATION1f-i part 2, §19.1 point 3).

    [Fact]
    public void Entering_records_the_process_s_run_once_moving_no_revision_and_touching_no_wake()
    {
        var store = Open(new DesktopStoreOptions { Kind = "shell", Settled = () => StoreSettled.Yes });

        store.Enter();
        store.Enter();

        var run = Assert.Single(store.Runs());
        Assert.Equal((store.Run, "shell", Environment.ProcessId), (run.Run, run.Kind, run.Pid));
        Assert.Empty(store.Revisions());
        Assert.False(File.Exists(store.Wake));
    }

    [Fact]
    public void Entering_a_store_a_newer_build_stamped_writes_nothing()
    {
        Open(Newer()).Write("probe", _ => { });

        var older = Open(Older("shell"));

        Assert.Throws<DesktopStoreNewerException>(older.Enter);
        Assert.DoesNotContain(older.Runs(), each => each.Run == older.Run);
    }

    // ——— Revisions and the wake file (§13.4).

    [Fact]
    public void A_write_moves_its_family_s_revision_and_touches_the_wake_file_once_it_has_committed()
    {
        var store = Open();
        Assert.False(File.Exists(store.Wake));
        Assert.Equal(store.Location + ".wake", store.Wake);

        store.Write("settings", write => write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('settings', 'a', 1, 'x', 't', 'b', 0, 'left')"));
        store.Write("settings", _ => { });
        store.Write("bindings", _ => { });

        Assert.Equal(new Dictionary<string, long> { ["settings"] = 2, ["bindings"] = 1 }, store.Revisions());
        Assert.True(File.Exists(store.Wake));
        Assert.Equal(0, new FileInfo(store.Wake).Length);
    }

    [Fact]
    public void A_wake_file_written_before_the_write_changes_when_the_write_commits()
    {
        var store = Open();
        store.Write("settings", _ => { });
        File.SetLastWriteTimeUtc(store.Wake, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        store.Write("settings", _ => { });

        Assert.True(File.GetLastWriteTimeUtc(store.Wake) > new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_write_that_throws_changes_nothing_moves_no_revision_and_wakes_nothing()
    {
        var store = Open();

        Assert.Throws<InvalidOperationException>(() => store.Write("settings", write =>
        {
            write.Execute("INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('settings', 'a', 1, 'x', 't', 'b', 0, 'left')");
            throw new InvalidOperationException("the work failed");
        }));

        Assert.Equal(0, Count(store, "imports"));
        Assert.Empty(store.Revisions());
        Assert.Empty(store.Runs());
        Assert.False(File.Exists(store.Wake));
    }

    // ——— Runs (§13.4: every process that writes keeps a row).

    [Fact]
    public void A_process_run_is_recorded_by_its_first_write_once_with_its_kind_pid_and_start()
    {
        var store = Open(new DesktopStoreOptions { Kind = "host" });
        Assert.Empty(store.Runs());

        store.Write("settings", _ => { });
        store.Write("settings", _ => { });

        var run = Assert.Single(store.Runs());
        Assert.Equal((store.Run, "host", Environment.ProcessId), (run.Run, run.Kind, run.Pid));
        Assert.NotNull(run.Started);
        Assert.True(DateTimeOffset.TryParse(run.Began, out _));
    }

    [Fact]
    public void Two_stores_on_one_file_are_two_runs()
    {
        var shell = Open(new DesktopStoreOptions { Kind = "shell" });
        var verb = Open(new DesktopStoreOptions { Kind = "verb" });

        shell.Write("a", _ => { });
        verb.Write("a", _ => { });

        Assert.Equal(["shell", "verb"], shell.Runs().Select(each => each.Kind).Order().ToArray());
        Assert.NotEqual(shell.Run, verb.Run);
    }

    // ——— One store, three openers (the brief's proof).

    [Fact]
    public async Task The_shell_a_headless_loop_and_a_verb_open_one_store_at_once_and_each_write_lands()
    {
        var start = new ManualResetEventSlim();
        var kinds = new[] { "shell", "host", "verb" };
        var runs = kinds.Select(kind => Task.Run(() =>
        {
            start.Wait();
            using var store = DesktopStore.Open(Db, new DesktopStoreOptions { Kind = kind, Settled = () => StoreSettled.Yes });
            for (var each = 0; each < 20; each++)
            {
                store.Write(kind, write => write.Execute(
                    "INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ($family, $path, 1, 'x', 't', 'b', 0, 'left')",
                    ("$family", kind), ("$path", each.ToString())));
            }

            return store.Run;
        })).ToArray();

        start.Set();
        var ids = await Task.WhenAll(runs);

        var read = Open();
        Assert.Equal((Own, 1), read.Stamp);
        Assert.Equal(60, Count(read, "imports"));
        Assert.Equal(kinds.ToDictionary(kind => kind, _ => 20L), read.Revisions());
        Assert.Equal(ids.Order().ToArray(), read.Runs().Select(each => each.Run).Order().ToArray());
        Assert.Equal(kinds.Order().ToArray(), read.Runs().Select(each => each.Kind).Order().ToArray());
    }

    // ——— The writer stamp (§13.3; X3-3.5).

    [Fact]
    public void A_store_stamped_ahead_is_read_as_it_stands_refused_for_writes_and_said()
    {
        using (var newer = DesktopStore.Open(Db, Newer())) newer.Write("settings", _ => { });

        var older = Open(Older());

        Assert.Equal((Own + 1, 2), older.Stamp);
        Assert.True(older.Newer);
        Assert.Equal(1, older.Revisions()["settings"]);
        var refused = Assert.Throws<DesktopStoreNewerException>(() => older.Write("settings", _ => { }));
        Assert.Equal((2, 1), (refused.Stamped, refused.Own));
        Assert.Contains("newer Daoris", refused.Message);
        Assert.Contains("Update Daoris", refused.Message);
        Assert.Equal(1, older.Revisions()["settings"]);
    }

    [Fact]
    public void A_writer_that_opened_the_store_before_another_process_s_migration_is_refused_when_it_resumes_the_stamp_read_inside_its_own_transaction()
    {
        var older = Open(Older());
        older.Write("settings", _ => { });
        Assert.False(older.Newer);

        // Another process, a newer build, opens the file after it and migrates it, raising the stamp.
        using (var newer = DesktopStore.Open(Db, Newer(kind: "host"))) Assert.Equal((Own + 1, 2), newer.Stamp);

        // The older process kept its connection and its idea of the file: its next write meets the stamp in its own transaction.
        Assert.Throws<DesktopStoreNewerException>(() => older.Write("settings", write => write.Execute(
            "INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('late', 'a', 1, 'x', 't', 'b', 0, 'left')")));
        Assert.Equal(0, Count(older, "imports"));
        Assert.Equal(1, older.Revisions()["settings"]);
    }

    // ——— Migrations (§13.3, §13.5).

    [Fact]
    public void A_migration_that_raises_the_stamp_waits_for_the_confirmation_and_runs_once_it_is_settled()
    {
        var settled = StoreSettled.Not("the swap journal is at `started`");
        var store = Open(Newer(() => settled));
        store.Write("settings", _ => { });

        // The start's confirmation is not settled: the stamp stays where it was and an older build can still write.
        Assert.False(store.Migrate());
        Assert.Equal((Own, 1), store.Stamp);
        Assert.Contains("raises the store's writer stamp from 1 to 2", store.Held);
        Assert.Contains("`started`", store.Held);
        Assert.False(HasTable(store, "raised"));
        using (var older = DesktopStore.Open(Db, Older())) older.Write("settings", _ => { });

        settled = StoreSettled.Yes;

        Assert.True(store.Migrate());
        Assert.Null(store.Held);
        Assert.Equal((Own + 1, 2), store.Stamp);
        Assert.True(HasTable(store, "raised"));
    }

    [Fact]
    public void A_migration_that_adds_a_table_and_leaves_the_stamp_runs_at_the_first_start_whatever_the_confirmation_says()
    {
        var store = Open(new DesktopStoreOptions { Steps = [.. DesktopStore.Steps, Adds], Settled = () => StoreSettled.Not("started") });

        Assert.Null(store.Held);
        Assert.Equal((Own + 1, 1), store.Stamp);
        Assert.True(HasTable(store, "added"));
    }

    [Fact]
    public void The_store_a_first_start_makes_is_no_raise_and_runs_before_the_confirmation_is_settled()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("started") });

        Assert.Null(store.Held);
        Assert.Equal((Own, 1), store.Stamp);
    }

    [Fact]
    public void A_store_handed_no_reading_of_the_confirmation_holds_a_raise_and_an_import()
    {
        Open().Write("a", _ => { });
        var unasked = Open(new DesktopStoreOptions { Writer = 2, Steps = [.. DesktopStore.Steps, Raises] });

        Assert.Contains("no reading", unasked.Held);
        Assert.Equal((Own, 1), unasked.Stamp);
        Assert.False(unasked.Import(new StoreImport("settings", "data/driver.json", 1, "x", 0, ImportClass.Left)).Done);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_migration_interrupted_at_each_step_leaves_the_store_as_it_was(int interrupted)
    {
        IReadOnlyList<StoreMigration> Steps(bool fail) =>
        [
            .. DesktopStore.Steps.Select(step => interrupted == 1 && fail ? step with { Apply = write => { step.Apply(write); throw new IOException("killed"); } } : step),
            new(Own + 1, 1, write =>
            {
                write.Execute("CREATE TABLE step_two (x TEXT)");
                if (interrupted == 2 && fail) throw new IOException("killed");
            }),
            new(Own + 2, 1, write =>
            {
                write.Execute("CREATE TABLE step_three (x TEXT)");
                if (interrupted == 3 && fail) throw new IOException("killed");
            }),
        ];

        Assert.Throws<IOException>(() => DesktopStore.Open(Db, new DesktopStoreOptions { Steps = Steps(fail: true) }));

        // The store is as the steps before the interrupted one left it, its tables and its version; the interrupted step's DDL is gone.
        // The first case interrupts the build's own first step, so the store is made by none; the others interrupt the two steps after the build's.
        var after = interrupted == 1 ? 0 : Own + interrupted - 2;
        using (var inspect = new SqliteConnection($"Data Source={Db};Pooling=False"))
        {
            inspect.Open();
            var tables = new List<string>();
            using var list = inspect.CreateCommand();
            list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
            using (var reader = list.ExecuteReader()) while (reader.Read()) tables.Add(reader.GetString(0));
            Assert.Equal(after >= 1, tables.Contains("store_version"));
            Assert.Equal(after >= Own + 1, tables.Contains("step_two"));
            Assert.False(tables.Contains("step_three"));
            if (after >= 1)
            {
                using var version = inspect.CreateCommand();
                version.CommandText = "SELECT version FROM store_version";
                Assert.Equal((long)after, Convert.ToInt64(version.ExecuteScalar()));
            }
        }

        // And the next start, whole, goes on from there.
        var store = Open(new DesktopStoreOptions { Steps = Steps(fail: false) });
        Assert.Equal((Own + 2, 1), store.Stamp);
        Assert.True(HasTable(store, "step_two") && HasTable(store, "step_three"));
    }

    // ——— Imports (§13.3's `imports`; §13.5; §19.7).

    private static StoreImport Driver() => new("settings", "data/driver.json", 12, "ab12", 3, ImportClass.Left);

    [Fact]
    public void An_import_while_the_confirmation_is_not_settled_imports_nothing_and_says_what_it_waits_on()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Not("the swap journal is at `started`") });
        var loaded = false;

        var outcome = store.Import(Driver(), _ => loaded = true);

        Assert.False(outcome.Done);
        Assert.Contains("`started`", outcome.Said);
        Assert.False(loaded);
        Assert.Equal(0, Count(store, "imports"));
        Assert.Empty(store.Revisions());
        Assert.False(File.Exists(store.Wake));
    }

    [Fact]
    public void An_import_once_settled_writes_its_rows_and_its_receipt_in_one_transaction_and_moves_the_family_s_revision()
    {
        var store = Open(new DesktopStoreOptions { Kind = "shell", Build = "9.9.9", Settled = () => StoreSettled.Yes });
        store.Write("probe", write => write.Execute("CREATE TABLE IF NOT EXISTS rows_of_settings (k TEXT)"));

        var outcome = store.Import(Driver(), write => write.Execute("CREATE TABLE IF NOT EXISTS rows_of_settings (k TEXT); INSERT INTO rows_of_settings VALUES ('drivable')"));

        Assert.True(outcome.Done);
        Assert.NotNull(outcome.Id);
        Assert.Equal(1, Count(store, "rows_of_settings"));
        Assert.Equal(1, store.Revisions()["settings"]);
        var kept = store.LastImport("settings", "data/driver.json")!;
        Assert.Equal(Driver(), kept.Import);
        Assert.Equal("9.9.9", kept.Build);
        Assert.Equal(outcome.Id, kept.Id);
        Assert.Null(store.LastImport("settings", "data/other.json"));
    }

    [Fact]
    public void An_import_whose_load_fails_leaves_no_receipt_and_no_revision()
    {
        var store = Open(new DesktopStoreOptions { Settled = () => StoreSettled.Yes });

        Assert.Throws<InvalidOperationException>(() => store.Import(Driver(), _ => throw new InvalidOperationException("a row did not read")));

        Assert.Equal(0, Count(store, "imports"));
        Assert.Empty(store.Revisions());
    }

    [Fact]
    public void An_import_of_a_file_records_its_size_and_sha256()
    {
        var file = Path.Combine(_root, "driver.json");
        File.WriteAllText(file, "abc");

        var import = StoreImport.Of("settings", "data/driver.json", file, 2, ImportClass.SetAside);

        Assert.Equal((3L, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ImportClass.SetAside), (import.Size, import.Sha256, import.Class));
    }

    // ——— The busy wait (§13.4).

    [Fact]
    public void A_write_refused_past_the_busy_wait_says_so_and_writes_nothing()
    {
        var store = Open();
        store.Write("settings", _ => { });
        using var holder = new SqliteConnection($"Data Source={Db};Pooling=False");
        holder.Open();
        using var hold = holder.CreateCommand();
        hold.CommandText = "BEGIN IMMEDIATE";
        hold.ExecuteNonQuery();

        var began = System.Diagnostics.Stopwatch.StartNew();
        var refused = Assert.Throws<DesktopStoreBusyException>(() => store.Write("settings", write => write.Execute(
            "INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ('busy', 'a', 1, 'x', 't', 'b', 0, 'left')")));

        // It waited the five seconds the design says before it refused (a lower bound only: a loaded machine waits longer, never less).
        Assert.True(began.Elapsed >= TimeSpan.FromSeconds(4.5), $"refused after {began.Elapsed}");

        Assert.Contains("busy", refused.Message);
        Assert.Contains("nothing was written", refused.Message);
        using var release = holder.CreateCommand();
        release.CommandText = "ROLLBACK";
        release.ExecuteNonQuery();
        Assert.Equal(0, Count(store, "imports"));
        Assert.Equal(1, store.Revisions()["settings"]);
    }
}
