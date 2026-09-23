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
