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
