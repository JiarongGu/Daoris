using Daoris.Knowledge;
using Microsoft.Data.Sqlite;

namespace Daoris.Service.Tests;

/// <summary>
/// A pushed registration must outlive the process that received it. `daoris connect` is how a
/// repository a remote service cannot scan makes itself addressable — and a registration held only in
/// memory turns every service restart into that repository silently dropping off the map, which is
/// SVC1's "nothing survives between sessions" in its most concrete form.
/// </summary>
public sealed class RegistrationStoreTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private RegistrationStore _store = null!;
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-18T10:00:00Z");

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = await RegistrationStore.OpenAsync(_connection);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static Registration Declared(string name = "Yumeora") => new(
        name, Adopted: true, "An anime life sandbox.",
        Owns: ["world simulation", "the mod runtime"],
        Accepts: ["a subsystem quest"],
        Packs: ["desktop-app"],
        Entries: 0);

    [Fact]
    public async Task A_registration_round_trips_with_every_field()
    {
        await _store.UpsertAsync(Declared(), Now);

        var read = Assert.Single(await _store.AllAsync());

        Assert.True(read.Adopted);
        Assert.Equal("An anime life sandbox.", read.Summary);
        Assert.Equal(["world simulation", "the mod runtime"], read.Owns);
        Assert.Equal(["a subsystem quest"], read.Accepts);
        Assert.Equal(["desktop-app"], read.Packs);
    }

    /// <summary>
    /// D91: what a repository says it uses is part of its declaration — kept, read back by the one
    /// rule, and replaced with the rest on the next registration (silence here says "nothing").
    /// </summary>
    [Fact]
    public async Task What_a_repository_says_it_uses_round_trips_and_is_replaced_with_the_declaration()
    {
        await _store.UpsertAsync(Declared() with { Uses = [" engine ", "ENGINE", "Yumeora", "tools"] }, Now);
        Assert.Equal(["engine", "tools"], Assert.Single(await _store.AllAsync()).DependsOn);

        await _store.UpsertAsync(Declared(), Now.AddDays(1));
        Assert.Empty(Assert.Single(await _store.AllAsync()).DependsOn);
    }

    /// <summary>
    /// The rule `uses` is read by (D91), as a table the CLI's `usesOf` keeps line for line (twins):
    /// absent is empty; trimmed; a blank dropped; a repeat in any case dropped, the first spelling kept;
    /// the repository's own name dropped.
    /// </summary>
    [Theory]
    [InlineData(null, "")]
    [InlineData(new[] { "engine" }, "engine")]
    [InlineData(new[] { " engine ", "" }, "engine")]
    [InlineData(new[] { "engine", "ENGINE", "tools" }, "engine,tools")]
    [InlineData(new[] { "game", "Game", "engine" }, "engine")]
    public void Uses_reads_by_one_rule(string[]? declared, string expected)
    {
        Assert.Equal(expected, string.Join(',', Knowledge.Declared.Uses(declared, "game")));
    }

    /// <summary>Re-registering is an update, not a duplicate: the repository is the identity.</summary>
    [Fact]
    public async Task Registering_again_replaces_the_declaration()
    {
        await _store.UpsertAsync(Declared(), Now);
        await _store.UpsertAsync(Declared() with { Summary = "Revised." }, Now.AddDays(1));

        var read = Assert.Single(await _store.AllAsync());

        Assert.Equal("Revised.", read.Summary);
    }

    /// <summary>
    /// The canonical line survives a restart, and an ordinary re-registration does not erase it
    /// (D48 §6) — the same shape the workspace has, for the same reason: `connect` says nothing about
    /// branches and runs on every tick. A restart is exactly when this matters, because that is when
    /// the in-memory registry is rebuilt from these rows.
    /// </summary>
    [Fact]
    public async Task The_declared_canonical_line_round_trips_and_silence_preserves_it()
    {
        await _store.UpsertAsync(Declared() with { DefaultBranch = "main" }, Now);
        Assert.Equal("main", Assert.Single(await _store.AllAsync()).DefaultBranch);

        await _store.UpsertAsync(Declared() with { DefaultBranch = null }, Now.AddDays(1));
        Assert.Equal("main", Assert.Single(await _store.AllAsync()).DefaultBranch);

        await _store.UpsertAsync(Declared() with { DefaultBranch = "trunk" }, Now.AddDays(2));
        Assert.Equal("trunk", Assert.Single(await _store.AllAsync()).DefaultBranch);
    }

    /// <summary>
    /// What commit this deployment's copy stands on, kept beside the registrations and gone with them.
    /// A leftover row would refuse the first feed after a repository re-joined, as though a commit it
    /// had dropped were still held.
    /// </summary>
    [Fact]
    public async Task Fed_provenance_round_trips_and_retires_with_its_registration()
    {
        await _store.UpsertAsync(Declared(), Now);
        await _store.RecordProvenanceAsync(
            "Yumeora", new("abc123def456", Now, "main", "person@machine-a"));

        var held = await _store.ProvenanceAsync("yumeora");
        Assert.Equal("abc123def456", held!.Commit);
        Assert.Equal("abc123de", held.ShortCommit);
        Assert.Equal("main", held.Branch);
        Assert.Equal("person@machine-a", held.Origin);
        Assert.Equal(Now, held.CommittedAt);
        Assert.Single(await _store.AllProvenanceAsync());

        await _store.DeleteAsync("Yumeora");

        Assert.Null(await _store.ProvenanceAsync("Yumeora"));
        Assert.Empty(await _store.AllProvenanceAsync());
    }

    /// <summary>
    /// The root is the one field spawning needs (D46): `connect` runs in the repository and knows it.
    /// Stored machine-locally — this store never leaves the machine it serves.
    /// </summary>
    [Fact]
    public async Task A_root_round_trips_and_its_absence_is_null_not_empty()
    {
        await _store.UpsertAsync(Declared() with { Root = "D:/repos/Yumeora" }, Now);
        await _store.UpsertAsync(Declared("Rootless"), Now);

        var all = await _store.AllAsync();

        Assert.Equal("D:/repos/Yumeora", all.Single(r => r.Repository == "Yumeora").Root);
        Assert.Null(all.Single(r => r.Repository == "Rootless").Root);
    }

    /// <summary>
    /// The remote declaration is the disclosure boundary's input (D47 §4): the sync loop feeds only
    /// repositories whose own reviewed manifest said so, and that answer must survive a restart.
    /// </summary>
    [Fact]
    public async Task The_remote_declaration_round_trips_and_silence_means_local()
    {
        await _store.UpsertAsync(Declared() with { Joined = true, SharesKnowledge = true }, Now);
        await _store.UpsertAsync(Declared("Silent"), Now);

        var all = await _store.AllAsync();
        var joined = all.Single(r => r.Repository == "Yumeora");
        var silent = all.Single(r => r.Repository == "Silent");

        Assert.True(joined.Joined);
        Assert.True(joined.SharesKnowledge);
        Assert.False(silent.Joined);
        Assert.False(silent.SharesKnowledge);
    }

    /// <summary>
    /// A store created before sessions existed has no root column, and its registrations must survive
    /// the upgrade — a schema that only works on a fresh database drops every connected repository.
    /// </summary>
    [Fact]
    public async Task An_existing_store_without_the_root_column_is_migrated_in_place()
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

        Assert.Equal("Was here first.", elder.Summary);
        Assert.Null(elder.Root);
        Assert.False(elder.Joined);
        Assert.False(elder.SharesKnowledge);

        await store.UpsertAsync(elder with { Root = "/srv/Elder", Joined = true }, Now);
        var upgraded = (await store.AllAsync()).Single();
        Assert.Equal("/srv/Elder", upgraded.Root);
        Assert.True(upgraded.Joined);
    }

    /// <summary>
    /// A commit held before digests existed survives the upgrade with none (SYNC5a) — which the
    /// ordering reads as "compare nothing, take the same commit once" — and records one from then on.
    /// </summary>
    [Fact]
    public async Task A_provenance_table_from_before_digests_gains_the_column_and_keeps_its_rows()
    {
        await using var old = new SqliteConnection("Data Source=:memory:");
        await old.OpenAsync();
        await using (var create = old.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE feed_provenance (
                  repository TEXT PRIMARY KEY, commit_id TEXT NOT NULL, committed_at TEXT NOT NULL,
                  branch TEXT NOT NULL, origin TEXT NULL
                );
                INSERT INTO feed_provenance VALUES ('Elder', 'aaaa1111', '2026-09-20T09:00:00.0000000+00:00', 'main', NULL);
                """;
            await create.ExecuteNonQueryAsync();
        }

        var store = await RegistrationStore.OpenAsync(old);
        var held = await store.ProvenanceAsync("Elder");

        Assert.Equal("aaaa1111", held!.Commit);
        Assert.Null(held.Digest);

        await store.RecordProvenanceAsync("Elder", held with { Digest = "d1g35t" });
        Assert.Equal("d1g35t", (await store.ProvenanceAsync("Elder"))!.Digest);
    }

    // ——— A retire is a tombstone that travels (SYNC5b). The store records it in the same statement
    // that ends the row, so no caller can retire a joined checkout and forget to tell the circle.

    private static Registration Held(string name = "Yumeora", string workspace = "aurora") =>
        Declared(name) with { Root = $"D:/repos/{name}", Joined = true, Workspace = workspace };

    [Fact]
    public async Task Retiring_a_joined_checkout_leaves_a_tombstone_for_its_circle()
    {
        await _store.UpsertAsync(Held(), Now);

        await _store.DeleteAsync("Yumeora");

        Assert.Equal(["Yumeora"], await _store.RetiredAsync("aurora"));
        Assert.Empty(await _store.RetiredAsync("default"));
    }

    /// <summary>
    /// Re-wired to another circle, or re-registered unjoined: either way the circle it left still
    /// lists it, and only this machine knows it went.
    /// </summary>
    [Fact]
    public async Task Leaving_a_circle_by_rewiring_or_unjoining_leaves_a_tombstone_too()
    {
        await _store.UpsertAsync(Held("Moved"), Now);
        await _store.UpsertAsync(Held("Quit"), Now);

        await _store.UpsertAsync(Held("Moved", "tools"), Now);
        await _store.UpsertAsync(Held("Quit") with { Joined = false }, Now);

        Assert.Equal(["Moved", "Quit"], (await _store.RetiredAsync("aurora")).Order(StringComparer.Ordinal));
        Assert.Empty(await _store.RetiredAsync("tools"));
    }

    /// <summary>
    /// Nothing leaves a circle it was never known in, and a teammate's copy is not this machine's to
    /// remove from the team: a row without a root records no tombstone, and neither does one never joined.
    /// </summary>
    [Fact]
    public async Task A_foreign_row_or_an_unjoined_one_retires_without_a_tombstone()
    {
        await _store.UpsertAsync(Held("Teammate") with { Root = null }, Now);
        await _store.UpsertAsync(Held("Homebody") with { Joined = false }, Now);

        await _store.DeleteAsync("Teammate");
        await _store.DeleteAsync("Homebody");

        Assert.Empty(await _store.RetiredAsync("aurora"));
    }

    /// <summary>Joining the same circle again takes the tombstone back before any pass could send it.</summary>
    [Fact]
    public async Task Joining_the_circle_again_clears_its_tombstone_and_a_cleared_one_stays_cleared()
    {
        await _store.UpsertAsync(Held(), Now);
        await _store.DeleteAsync("Yumeora");
        await _store.UpsertAsync(Held(), Now);
        Assert.Empty(await _store.RetiredAsync("aurora"));

        await _store.DeleteAsync("Yumeora");
        Assert.True(await _store.ClearRetiredAsync("yumeora", "AURORA"));
        Assert.False(await _store.ClearRetiredAsync("Yumeora", "aurora"));
        Assert.Empty(await _store.RetiredAsync("aurora"));
    }

    /// <summary>
    /// The commit a registration was declared at (SYNC5b), held like a feed's and gone with the row: a
    /// deployment keeps no tombstone of its own, so a repository that joins again is registered afresh.
    /// </summary>
    [Fact]
    public async Task A_registration_s_commit_round_trips_and_retires_with_it()
    {
        await _store.UpsertAsync(Declared(), Now);
        await _store.RecordRegistrationProvenanceAsync(
            "Yumeora", new("abc123def456", Now, "main", "person@machine-a") { Digest = "d1g35t" });

        var held = await _store.RegistrationProvenanceAsync("yumeora");
        Assert.Equal("abc123def456", held!.Commit);
        Assert.Equal("d1g35t", held.Digest);

        await _store.DeleteAsync("Yumeora");

        Assert.Null(await _store.RegistrationProvenanceAsync("Yumeora"));
    }
}

/// <summary>
/// The same property, proven where it matters: through the composition every host uses, across a
/// dispose and a reopen — the shape of a service restart.
/// </summary>
public sealed class RegistrationPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-connect-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        // Pooled connections outlive their store; without this the database file is still held when
        // the directory goes — the same cleanup SqliteStoreTests needs.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_pushed_registration_survives_a_service_restart()
    {
        var repositories = Path.Combine(_root, "repos");
        Directory.CreateDirectory(repositories);
        var options = new ServiceOptions(repositories, Path.Combine(_root, "knowledge.db"));
        var pushed = new Registration(
            "Yumeora", Adopted: true, "An anime life sandbox.",
            Owns: ["world simulation"], Accepts: ["a subsystem quest"], Packs: [], Entries: 0);

        await using (var first = await ServiceFactory.CreateAsync(options))
        {
            await first.Service.RegisterAsync(pushed, DateTimeOffset.UtcNow);
        }

        await using var second = await ServiceFactory.CreateAsync(options);
        var found = (await second.Service.RegistryAsync()).Single(r => r.Repository == "Yumeora");

        Assert.True(found.Registered);
        Assert.Equal("An anime life sandbox.", found.Summary);
    }
}
